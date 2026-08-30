using System.Text;
using System.Text.RegularExpressions;
using MailKit.Net.Smtp;
using MailKit.Security;
using MailDeliveryTool.Core.Models;
using MimeKit;

namespace MailDeliveryTool.Core.Mail;

/// <summary>
/// SMTPサーバーへの疎通検証を行う。
/// </summary>
/// <remarks>
/// 要件定義書 3章のとおり接続は SecureSocketOptions.Auto（587番ポート）で行い、
/// STARTTLS を必須化しない。そのうえで「実際に暗号化されたか」を
/// <see cref="SmtpClient.IsSecure"/> から取得して結果に含めるため、
/// 要件定義書 14章の検証事項をパケットキャプチャなしで確認できる。
/// </remarks>
public sealed class SmtpConnectionTester
{
    /// <summary>
    /// 接続・認証（任意でテストメール送信）を行い、結果を返す。
    /// 例外は投げず、すべて結果オブジェクトに詰めて返す。
    /// </summary>
    /// <param name="setting">検証するアカウント設定。</param>
    /// <param name="testMailTo">テストメールの宛先。null なら認証までで終了する。</param>
    /// <param name="timeout">1操作あたりのタイムアウト。</param>
    public async Task<SmtpConnectionTestResult> TestAsync(
        MailAccountSetting setting,
        string? testMailTo = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var option = ParseOption(setting.SecureSocketOption);
        var result = new SmtpConnectionTestResult
        {
            Host = setting.Host,
            Port = setting.Port,
            RequestedOption = option.ToString(),
        };

        // ProtocolLogger にサーバーとの全やり取りを記録する。
        // AUTH 行にパスワードが載るため、返す前に必ずマスクする。
        //
        // log を client より先に宣言しているのは破棄順のため。using var は宣言の
        // 逆順に破棄されるので client -> log の順になり、client の破棄処理が
        // ログを書こうとしたときに解放済みストリームへ書き込む事故を防げる。
        using var log = new MemoryStream();
        using var client = new SmtpClient(new MailKit.ProtocolLogger(log, leaveOpen: true));
        client.Timeout = (int)(timeout ?? TimeSpan.FromSeconds(30)).TotalMilliseconds;

        try
        {
            await client.ConnectAsync(setting.Host, setting.Port, option, cancellationToken)
                .ConfigureAwait(false);

            result.Stage = SmtpTestStage.Connected;
            result.IsEncrypted = client.IsSecure;
            result.TlsVersion = client.IsSecure ? client.SslProtocol.ToString() : null;
            result.CipherInfo = client.IsSecure
                ? $"{client.SslCipherAlgorithm} ({client.SslCipherStrength} bit)"
                : null;
            result.ServerAdvertisedStartTls =
                client.Capabilities.HasFlag(SmtpCapabilities.StartTLS);
            result.OfferedAuthMechanisms = client.AuthenticationMechanisms.OrderBy(m => m).ToList();
            result.ServerMaxMessageSize = client.MaxSize;

            if (!string.IsNullOrEmpty(setting.Password))
            {
                // 認証方式は MailKit がサーバーの広告内容から自動ネゴシエートする
                // （要件定義書 3章：AUTH-LOGIN / PLAIN / CRAM-MD5 のいずれか）。
                await client.AuthenticateAsync(setting.UserName, setting.Password, cancellationToken)
                    .ConfigureAwait(false);
                result.Authenticated = true;
                result.Stage = SmtpTestStage.Authenticated;
            }

            if (result.Authenticated && !string.IsNullOrWhiteSpace(testMailTo))
            {
                await client.SendAsync(BuildTestMessage(setting, testMailTo!), cancellationToken)
                    .ConfigureAwait(false);
                result.TestMailSent = true;
                result.Stage = SmtpTestStage.Sent;
            }

            await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            result.ErrorType = ex.GetType().Name;
            result.ErrorMessage = Describe(ex);
        }
        finally
        {
            result.ProtocolLog = MaskCredentials(Encoding.UTF8.GetString(log.ToArray()));
        }

        return result;
    }

    private static MimeMessage BuildTestMessage(MailAccountSetting setting, string to)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(setting.FromDisplayName ?? string.Empty, setting.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = "[メール配信ツール] SMTP疎通検証テスト";
        message.Body = new TextPart("plain")
        {
            Text = "メール配信ツールのSMTP疎通検証で送信されたテストメールです。\r\n"
                   + $"送信日時: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}\r\n",
        };
        return message;
    }

    /// <summary>設定文字列を MailKit の列挙値へ変換する。未知の値は Auto にフォールバックする。</summary>
    public static SecureSocketOptions ParseOption(string? value) =>
        Enum.TryParse<SecureSocketOptions>(value, ignoreCase: true, out var parsed)
            ? parsed
            : SecureSocketOptions.Auto;

    /// <summary>SMTPの応答コードなど、原因の切り分けに役立つ情報まで含めて文字列化する。</summary>
    private static string Describe(Exception ex) => ex switch
    {
        AuthenticationException =>
            $"SMTP認証に失敗しました（アカウント名・パスワードを確認してください）: {ex.Message}",
        SmtpCommandException smtp =>
            $"SMTPコマンドエラー [StatusCode={smtp.StatusCode}, ErrorCode={smtp.ErrorCode}]: {smtp.Message}",
        SmtpProtocolException =>
            $"SMTPプロトコルエラー（ポート番号・暗号化方式の不一致の可能性）: {ex.Message}",
        SslHandshakeException =>
            $"TLSハンドシェイクに失敗しました（サーバー証明書を確認してください）: {ex.Message}",
        OperationCanceledException =>
            "タイムアウトまたはキャンセルされました。",
        _ => $"{ex.GetType().Name}: {ex.Message}",
    };

    /// <summary>
    /// プロトコルログから認証情報を取り除く。
    /// AUTH の引数と、その直後にクライアントが送る Base64 行がパスワードを含む。
    /// </summary>
    internal static string MaskCredentials(string log)
    {
        // 行末は $ ではなく [^\r\n]+ で消費する。
        // ProtocolLogger の出力は CRLF 改行であり、複数行モードの $ は \n の直前にしか
        // マッチしないため、\S+$ では末尾の \r で不一致となりマスクが外れる。
        // 例: "C: AUTH PLAIN AGFiYwBwYXNz" -> "C: AUTH PLAIN ****"
        log = Regex.Replace(
            log,
            @"(?im)^(C:[ \t]*AUTH[ \t]+[A-Za-z0-9\-]+)[ \t]+[^\r\n]+",
            "$1 ****");

        // AUTH LOGIN / CRAM-MD5 では、続く C: 行がそのまま Base64 の資格情報になる。
        // サーバーのチャレンジ（S: 334 ...）直後のクライアント行をマスクする。
        // AUTH LOGIN はユーザー名・パスワードで334が2回来るが、両方マッチする。
        log = Regex.Replace(
            log,
            @"(?im)^(S:[ \t]*334[^\r\n]*\r?\n)C:[ \t]*[^\r\n]+",
            "$1C: ****");

        return log;
    }
}
