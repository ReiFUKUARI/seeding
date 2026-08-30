using System.Reflection;
using Microsoft.Data.Sqlite;

namespace MailDeliveryTool.Core.Data;

/// <summary>
/// 初回起動時のスキーマ構築と、以降のバージョン管理を行う。
/// </summary>
public sealed class DatabaseInitializer
{
    /// <summary>現在のスキーマバージョン。DDL を変更したら必ずインクリメントする。</summary>
    public const int CurrentSchemaVersion = 1;

    private readonly SqliteConnectionFactory _factory;

    public DatabaseInitializer(SqliteConnectionFactory factory) => _factory = factory;

    /// <summary>
    /// DB が未作成なら作成し、スキーマと初期データを適用する。
    /// 既に最新であれば何もしない。何度呼んでも安全（冪等）。
    /// </summary>
    /// <returns>今回スキーマを適用した場合 true。</returns>
    public bool EnsureCreated()
    {
        AppPaths.EnsureDataDirectory();

        using var connection = _factory.Create();
        var installed = ReadSchemaVersion(connection);

        if (installed >= CurrentSchemaVersion)
        {
            return false;
        }

        using var transaction = connection.BeginTransaction();

        // Schema_v1.sql / Seed_v1.sql はいずれも IF NOT EXISTS / INSERT OR IGNORE で
        // 書かれているため、途中まで作られたDBに対して再実行しても壊れない。
        Execute(connection, transaction, ReadResource("Schema_v1.sql"));
        Execute(connection, transaction, ReadResource("Seed_v1.sql"));

        using (var stamp = connection.CreateCommand())
        {
            stamp.Transaction = transaction;
            stamp.CommandText =
                "INSERT OR REPLACE INTO SchemaVersion (Version, AppliedAt) VALUES ($v, $t)";
            stamp.Parameters.AddWithValue("$v", CurrentSchemaVersion);
            stamp.Parameters.AddWithValue("$t", DateTimeOffset.Now.ToString("O"));
            stamp.ExecuteNonQuery();
        }

        transaction.Commit();
        return true;
    }

    /// <summary>適用済みスキーマバージョン。未作成なら 0。</summary>
    private static int ReadSchemaVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'SchemaVersion'";
        if (Convert.ToInt32(command.ExecuteScalar()) == 0)
        {
            return 0;
        }

        command.CommandText = "SELECT IFNULL(MAX(Version), 0) FROM SchemaVersion";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string ReadResource(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = $"MailDeliveryTool.Core.Data.Schema.{fileName}";
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"埋め込みリソースが見つかりません: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
