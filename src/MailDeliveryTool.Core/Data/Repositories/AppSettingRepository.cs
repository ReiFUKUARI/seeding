namespace MailDeliveryTool.Core.Data.Repositories;

/// <summary>
/// キー・バリュー形式のアプリ設定（署名・バックアップ設定等）の読み書き。
/// AppSetting テーブルの薄いラッパー。
/// </summary>
public sealed class AppSettingRepository
{
    private readonly SqliteConnectionFactory _factory;

    public AppSettingRepository(SqliteConnectionFactory factory) => _factory = factory;

    /// <summary>キーが存在しない場合は null を返す。</summary>
    public string? Get(string key)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM AppSetting WHERE Key = $key";
        command.Parameters.AddWithValue("$key", key);
        var value = command.ExecuteScalar();
        return value is null || value is DBNull ? null : (string)value;
    }

    public void Set(string key, string? value)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO AppSetting (Key, Value, UpdatedAt) VALUES ($key, $value, $updatedAt)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value, UpdatedAt = excluded.UpdatedAt
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", (object?)value ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.Now.ToString("O"));
        command.ExecuteNonQuery();
    }

    /// <summary>週次バックアップの判定に使う最終実行日時（D-004）。未実行なら null。</summary>
    public DateTimeOffset? GetLastBackupAt()
    {
        var raw = Get("LastBackupAt");
        return string.IsNullOrEmpty(raw) ? null : DateTimeOffset.Parse(raw);
    }

    public void SetLastBackupAt(DateTimeOffset value) => Set("LastBackupAt", value.ToString("O"));

    /// <summary>署名（要件定義書7章。1つのみ登録・自動反映）。未設定なら空文字列。</summary>
    public string GetSignature() => Get("Signature") ?? string.Empty;

    public void SetSignature(string signature) => Set("Signature", signature);

    /// <summary>
    /// バックアップ保存先。空文字列の場合は既定のバックアップ先
    /// （<see cref="MailDeliveryTool.Core.AppPaths.DefaultBackupDirectory"/>）を使う。
    /// </summary>
    public string GetBackupFolderPath() => Get("BackupFolderPath") ?? string.Empty;

    public void SetBackupFolderPath(string path) => Set("BackupFolderPath", path);
}
