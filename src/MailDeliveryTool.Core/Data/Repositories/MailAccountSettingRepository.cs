using MailDeliveryTool.Core.Models;
using MailDeliveryTool.Core.Security;

namespace MailDeliveryTool.Core.Data.Repositories;

/// <summary>送信元メールアカウント設定の読み書き（要件定義書 3章・10.1）。DB上は常に1行のみ。</summary>
public sealed class MailAccountSettingRepository
{
    private readonly SqliteConnectionFactory _factory;

    public MailAccountSettingRepository(SqliteConnectionFactory factory) => _factory = factory;

    /// <summary>
    /// 設定を取得する。パスワードはDPAPIで復号できた場合のみ設定される。
    /// 別ユーザー・別PCへの持ち込み等で復号できない場合は null になる
    /// （<see cref="DpapiSecretProtector"/> 参照）。呼び出し側は null を
    /// 「パスワードの再設定が必要」として扱うこと。
    /// </summary>
    public MailAccountSetting Get()
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Host, Port, UserName, EncryptedPassword, SecureSocketOption, FromAddress, FromDisplayName
            FROM MailAccountSetting WHERE Id = 1
            """;
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            // Seed_v1.sql で必ず1行投入されているはずだが、念のため既定値を返す
            return new MailAccountSetting();
        }

        var encryptedPassword = reader.IsDBNull(3) ? null : (byte[])reader[3];
        return new MailAccountSetting
        {
            Host = reader.GetString(0),
            Port = reader.GetInt32(1),
            UserName = reader.GetString(2),
            Password = DpapiSecretProtector.Unprotect(encryptedPassword),
            SecureSocketOption = reader.GetString(4),
            FromAddress = reader.GetString(5),
            FromDisplayName = reader.IsDBNull(6) ? null : reader.GetString(6),
        };
    }

    /// <summary>
    /// 設定を保存する。<paramref name="passwordChanged"/> が true の場合のみ
    /// パスワードをDPAPIで再暗号化する（要件定義書10.1「パスワードは変更する場合のみ入力」）。
    /// false の場合は既存の暗号化済みパスワードをそのまま維持する。
    /// </summary>
    public void Save(MailAccountSetting setting, bool passwordChanged)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();

        if (passwordChanged)
        {
            command.CommandText = """
                UPDATE MailAccountSetting
                SET Host = $host, Port = $port, UserName = $userName, EncryptedPassword = $password,
                    SecureSocketOption = $option, FromAddress = $fromAddress, FromDisplayName = $fromDisplayName,
                    UpdatedAt = $updatedAt
                WHERE Id = 1
                """;
            var encrypted = DpapiSecretProtector.Protect(setting.Password);
            command.Parameters.AddWithValue("$password", (object?)encrypted ?? DBNull.Value);
        }
        else
        {
            command.CommandText = """
                UPDATE MailAccountSetting
                SET Host = $host, Port = $port, UserName = $userName,
                    SecureSocketOption = $option, FromAddress = $fromAddress, FromDisplayName = $fromDisplayName,
                    UpdatedAt = $updatedAt
                WHERE Id = 1
                """;
        }

        command.Parameters.AddWithValue("$host", setting.Host);
        command.Parameters.AddWithValue("$port", setting.Port);
        command.Parameters.AddWithValue("$userName", setting.UserName);
        command.Parameters.AddWithValue("$option", setting.SecureSocketOption);
        command.Parameters.AddWithValue("$fromAddress", setting.FromAddress);
        command.Parameters.AddWithValue("$fromDisplayName", (object?)setting.FromDisplayName ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.Now.ToString("O"));
        command.ExecuteNonQuery();
    }
}
