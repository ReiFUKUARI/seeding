using Microsoft.Data.Sqlite;

namespace MailDeliveryTool.Core.Data;

/// <summary>
/// SQLite 接続を生成する。
/// </summary>
/// <remarks>
/// SQLite の外部キー制約は既定で無効のため、接続文字列で必ず有効化する。
/// スキーマは ON DELETE CASCADE / RESTRICT に依存しているので、
/// この設定が漏れると仕様どおりに動作しない。
/// </remarks>
public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            // 1人1台・単一プロセス利用のため Private で十分（要件定義書 2章）
            Cache = SqliteCacheMode.Private,
            ForeignKeys = true,
        }.ToString();
    }

    public SqliteConnection Create()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // 書き込み中の読み取りをブロックしないようにする。
        // 週次バックアップと通常操作が重なっても待たされにくくなる。
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
        pragma.ExecuteNonQuery();

        return connection;
    }
}
