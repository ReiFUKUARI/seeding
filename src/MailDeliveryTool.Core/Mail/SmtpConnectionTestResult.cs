namespace MailDeliveryTool.Core.Mail;

/// <summary>疎通検証の到達段階。</summary>
public enum SmtpTestStage
{
    /// <summary>TCP接続にも失敗した。</summary>
    NotConnected,

    /// <summary>接続はできたが認証に到達していない。</summary>
    Connected,

    /// <summary>接続・認証まで成功した。</summary>
    Authenticated,

    /// <summary>テストメールの送信まで成功した。</summary>
    Sent,
}

/// <summary>
/// SMTP疎通検証の結果。
/// 要件定義書 14章の未検証事項（STARTTLSが実際にネゴシエーションされるか／
/// サーバー側のメールサイズ上限）に、パケットキャプチャなしで答えることを目的とする。
/// </summary>
public sealed class SmtpConnectionTestResult
{
    public SmtpTestStage Stage { get; set; } = SmtpTestStage.NotConnected;

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }

    /// <summary>接続時に指定した SecureSocketOptions。</summary>
    public string RequestedOption { get; set; } = string.Empty;

    /// <summary>
    /// 実際に暗号化されたか。SecureSocketOptions.Auto を指定した場合、
    /// これが true なら STARTTLS が実際にネゴシエーションされたことを意味する。
    /// </summary>
    public bool IsEncrypted { get; set; }

    /// <summary>ネゴシエートされたTLSバージョン（例: Tls13）。平文なら null。</summary>
    public string? TlsVersion { get; set; }

    /// <summary>暗号スイート情報。平文なら null。</summary>
    public string? CipherInfo { get; set; }

    /// <summary>サーバーが STARTTLS を広告していたか。</summary>
    public bool ServerAdvertisedStartTls { get; set; }

    /// <summary>サーバーが広告した認証方式（AUTH の一覧）。</summary>
    public List<string> OfferedAuthMechanisms { get; set; } = new();

    /// <summary>認証に成功したか。</summary>
    public bool Authenticated { get; set; }

    /// <summary>
    /// サーバーが SIZE 拡張で広告した1通あたりの上限バイト数。0 は未広告。
    /// 要件定義書 14章「添付ファイルの安全な実容量上限値」の確定に使う。
    /// </summary>
    public long ServerMaxMessageSize { get; set; }

    /// <summary>テストメールを送信した場合 true。</summary>
    public bool TestMailSent { get; set; }

    /// <summary>失敗した場合のエラー内容。</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>例外の型名（切り分け用）。</summary>
    public string? ErrorType { get; set; }

    /// <summary>SMTPの生の応答ログ（認証情報はマスク済み）。</summary>
    public string ProtocolLog { get; set; } = string.Empty;

    public bool IsSuccess => Stage >= SmtpTestStage.Authenticated;
}
