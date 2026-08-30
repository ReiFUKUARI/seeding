using System.Text;
using MailDeliveryTool.Core.Mail;
using MailDeliveryTool.Core.Models;

namespace MailDeliveryTool.SmtpProbe;

/// <summary>
/// SMTP疎通検証ツール（フェーズ4・成果物③）。
///
/// WebARENA の実アカウントに対して 587 + SecureSocketOptions.Auto で接続し、
/// STARTTLS が実際にネゴシエートされたか・どの認証方式が使われたか・
/// サーバーが広告するメールサイズ上限はいくつかを確認する。
///
/// パスワードは引数にもファイルにも書かず、対話入力のみで受け取る
/// （コマンド履歴やプロセス一覧に残さないため）。
///
/// 使い方:
///   smtp-probe --host mail.example.jp --user user@example.jp --from user@example.jp
///   smtp-probe --host ... --user ... --from ... --send-to me@example.jp
///   smtp-probe --host ... --user ... --from ... --option StartTls --log probe.log
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Contains("--help") || args.Contains("-h"))
        {
            PrintUsage();
            return 0;
        }

        var options = CommandLineOptions.Parse(args);
        if (options.Error is not null)
        {
            Console.Error.WriteLine($"エラー: {options.Error}");
            Console.Error.WriteLine();
            PrintUsage();
            return 2;
        }

        var password = ReadPassword("SMTPパスワード（入力は表示されません）: ");
        if (string.IsNullOrEmpty(password))
        {
            Console.Error.WriteLine("エラー: パスワードが入力されませんでした。");
            return 2;
        }

        var setting = new MailAccountSetting
        {
            Host = options.Host!,
            Port = options.Port,
            UserName = options.User!,
            Password = password,
            FromAddress = options.From ?? options.User!,
            SecureSocketOption = options.Option,
        };

        Console.WriteLine();
        Console.WriteLine($"接続先        : {setting.Host}:{setting.Port}");
        Console.WriteLine($"暗号化方式    : {SmtpConnectionTester.ParseOption(setting.SecureSocketOption)}");
        Console.WriteLine($"アカウント    : {setting.UserName}");
        Console.WriteLine($"テストメール  : {(options.SendTo is null ? "送信しない（認証まで）" : options.SendTo)}");
        Console.WriteLine();
        Console.WriteLine("検証中...");

        var result = await new SmtpConnectionTester()
            .TestAsync(setting, options.SendTo, TimeSpan.FromSeconds(options.TimeoutSeconds))
            .ConfigureAwait(false);

        PrintReport(result);

        if (options.LogPath is not null)
        {
            // ProtocolLog は認証情報マスク済み
            await File.WriteAllTextAsync(options.LogPath, result.ProtocolLog, new UTF8Encoding(false))
                .ConfigureAwait(false);
            Console.WriteLine($"プロトコルログを保存しました: {Path.GetFullPath(options.LogPath)}");
        }

        return result.IsSuccess ? 0 : 1;
    }

    private static void PrintReport(SmtpConnectionTestResult r)
    {
        Console.WriteLine();
        Console.WriteLine("================ 検証結果 ================");
        Console.WriteLine($"到達段階              : {StageLabel(r.Stage)}");
        Console.WriteLine($"要求した暗号化方式    : {r.RequestedOption}");
        Console.WriteLine();

        // 要件定義書 14章の検証事項その1
        Console.WriteLine("--- STARTTLS の実際の動作（要件定義書 14章） ---");
        Console.WriteLine($"サーバーのSTARTTLS広告: {YesNo(r.ServerAdvertisedStartTls)}");
        Console.WriteLine($"実際に暗号化されたか  : {YesNo(r.IsEncrypted)}");
        if (r.IsEncrypted)
        {
            Console.WriteLine($"  TLSバージョン       : {r.TlsVersion}");
            Console.WriteLine($"  暗号スイート        : {r.CipherInfo}");
            Console.WriteLine("  => Auto指定でSTARTTLSが実際にネゴシエートされました。");
        }
        else if (r.Stage >= SmtpTestStage.Connected)
        {
            Console.WriteLine("  => 平文で接続されました。認証情報が平文で流れるため、");
            Console.WriteLine("     サーバー側でSTARTTLSが利用可能かを提供元に確認してください。");
        }

        Console.WriteLine();
        Console.WriteLine("--- 認証（要件定義書 3章） ---");
        Console.WriteLine($"サーバー広告の認証方式: "
            + (r.OfferedAuthMechanisms.Count > 0 ? string.Join(", ", r.OfferedAuthMechanisms) : "(なし)"));
        Console.WriteLine($"認証結果              : {YesNo(r.Authenticated)}");

        Console.WriteLine();
        Console.WriteLine("--- メールサイズ上限（要件定義書 14章） ---");
        if (r.ServerMaxMessageSize > 0)
        {
            var mb = r.ServerMaxMessageSize / 1024d / 1024d;
            Console.WriteLine($"SIZE拡張の広告値      : {r.ServerMaxMessageSize:N0} バイト（約 {mb:F1} MB）");
            // Base64エンコードでおよそ 4/3 倍 + ヘッダ。要件定義書はマージンを見て1.4倍で計算している。
            var safeAttachmentMb = mb / 1.4d;
            Console.WriteLine($"添付ファイル合計の目安: 約 {safeAttachmentMb:F1} MB 以下");
            Console.WriteLine("  （Base64増量率1.4倍で逆算した値。本文・ヘッダ分を差し引いて実装値を確定すること）");
        }
        else
        {
            Console.WriteLine("SIZE拡張の広告値      : 広告なし（サーバー仕様書の25MBを前提に実装値を決めること）");
        }

        if (r.TestMailSent)
        {
            Console.WriteLine();
            Console.WriteLine("テストメールの送信に成功しました。受信箱で到達を確認してください。");
        }

        if (r.ErrorMessage is not null)
        {
            Console.WriteLine();
            Console.WriteLine("--- エラー ---");
            Console.WriteLine($"{r.ErrorType}: {r.ErrorMessage}");
        }

        Console.WriteLine("==========================================");
        Console.WriteLine();
        Console.WriteLine("--- SMTPプロトコルログ（認証情報はマスク済み） ---");
        Console.WriteLine(string.IsNullOrWhiteSpace(r.ProtocolLog) ? "(ログなし)" : r.ProtocolLog);
    }

    private static string StageLabel(SmtpTestStage stage) => stage switch
    {
        SmtpTestStage.NotConnected => "接続失敗",
        SmtpTestStage.Connected => "接続成功（認証未実施）",
        SmtpTestStage.Authenticated => "接続・認証成功",
        SmtpTestStage.Sent => "接続・認証・送信すべて成功",
        _ => stage.ToString(),
    };

    private static string YesNo(bool value) => value ? "はい" : "いいえ";

    /// <summary>エコーバックせずにパスワードを読み取る。</summary>
    private static string ReadPassword(string prompt)
    {
        Console.Write(prompt);

        // リダイレクト時（CI等）は ReadKey が使えないため標準入力から1行読む
        if (Console.IsInputRedirected)
        {
            var line = Console.ReadLine();
            Console.WriteLine();
            return line ?? string.Empty;
        }

        var builder = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (builder.Length > 0)
                {
                    builder.Length--;
                }
                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                builder.Append(key.KeyChar);
            }
        }

        return builder.ToString();
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            SMTP疎通検証ツール (smtp-probe)

            必須:
              --host <ホスト名>     SMTPサーバー名
              --user <アカウント>   SMTP認証のアカウント名

            任意:
              --from <アドレス>     送信元アドレス（既定: --user と同じ）
              --port <番号>         ポート番号（既定: 587）
              --option <方式>       None | Auto | SslOnConnect | StartTls | StartTlsWhenAvailable
                                    （既定: Auto。要件定義書3章の本番設定）
              --send-to <アドレス>  テストメールを実送信する宛先
              --log <パス>          プロトコルログの保存先ファイル
              --timeout <秒>        タイムアウト秒数（既定: 30）
              -h, --help            このヘルプ

            パスワードは引数では受け取らず、起動後に対話入力する。

            終了コード: 0 = 認証まで成功 / 1 = 検証失敗 / 2 = 引数エラー
            """);
    }
}
