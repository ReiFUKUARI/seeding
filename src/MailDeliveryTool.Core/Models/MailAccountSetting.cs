namespace MailDeliveryTool.Core.Models;

/// <summary>
/// 送信元メールアカウント設定（要件定義書 3章 / 10.1）。DB上は常に1行のみ。
/// </summary>
public sealed class MailAccountSetting
{
    /// <summary>SMTPサーバー名。空文字の場合は「未設定」とみなす。</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>ポート。WebARENA メールホスティングは 587（要件定義書 3章）。</summary>
    public int Port { get; set; } = 587;

    /// <summary>SMTP認証のアカウント名。</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 平文パスワード。DBには保存せず、DPAPI で暗号化した状態のみ永続化する。
    /// メモリ上での保持は送信処理の間だけにとどめる。
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// MailKit の SecureSocketOptions 名。既定は Auto。
    /// 要件定義書 3章により STARTTLS の必須化はせず自動判定とする。
    /// </summary>
    public string SecureSocketOption { get; set; } = "Auto";

    public string FromAddress { get; set; } = string.Empty;
    public string? FromDisplayName { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host)
                                && !string.IsNullOrWhiteSpace(UserName)
                                && !string.IsNullOrWhiteSpace(FromAddress);
}
