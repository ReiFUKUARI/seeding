namespace MailDeliveryTool.SmtpProbe;

/// <summary>smtp-probe のコマンドライン引数。</summary>
internal sealed class CommandLineOptions
{
    public string? Host { get; private set; }
    public string? User { get; private set; }
    public string? From { get; private set; }
    public int Port { get; private set; } = 587;
    public string Option { get; private set; } = "Auto";
    public string? SendTo { get; private set; }
    public string? LogPath { get; private set; }
    public int TimeoutSeconds { get; private set; } = 30;

    /// <summary>解析エラーの内容。null なら正常。</summary>
    public string? Error { get; private set; }

    private static readonly string[] ValidOptions =
        ["None", "Auto", "SslOnConnect", "StartTls", "StartTlsWhenAvailable"];

    public static CommandLineOptions Parse(string[] args)
    {
        var result = new CommandLineOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var name = args[i];
            // 値を伴う引数は次の要素を読む
            string? Next()
            {
                if (i + 1 >= args.Length)
                {
                    result.Error = $"{name} に値が指定されていません。";
                    return null;
                }
                return args[++i];
            }

            switch (name)
            {
                case "--host":
                    result.Host = Next();
                    break;
                case "--user":
                    result.User = Next();
                    break;
                case "--from":
                    result.From = Next();
                    break;
                case "--send-to":
                    result.SendTo = Next();
                    break;
                case "--log":
                    result.LogPath = Next();
                    break;
                case "--option":
                    var option = Next();
                    if (option is not null)
                    {
                        var matched = ValidOptions.FirstOrDefault(
                            v => string.Equals(v, option, StringComparison.OrdinalIgnoreCase));
                        if (matched is null)
                        {
                            result.Error =
                                $"--option の値が不正です: {option}（指定可能: {string.Join(" | ", ValidOptions)}）";
                        }
                        else
                        {
                            result.Option = matched;
                        }
                    }
                    break;
                case "--port":
                    var port = Next();
                    if (port is not null)
                    {
                        if (!int.TryParse(port, out var parsedPort) || parsedPort is < 1 or > 65535)
                        {
                            result.Error = $"--port の値が不正です: {port}";
                        }
                        else
                        {
                            result.Port = parsedPort;
                        }
                    }
                    break;
                case "--timeout":
                    var timeout = Next();
                    if (timeout is not null)
                    {
                        if (!int.TryParse(timeout, out var parsedTimeout) || parsedTimeout <= 0)
                        {
                            result.Error = $"--timeout の値が不正です: {timeout}";
                        }
                        else
                        {
                            result.TimeoutSeconds = parsedTimeout;
                        }
                    }
                    break;
                default:
                    result.Error = $"不明な引数です: {name}";
                    break;
            }

            if (result.Error is not null)
            {
                return result;
            }
        }

        if (string.IsNullOrWhiteSpace(result.Host))
        {
            result.Error = "--host は必須です。";
        }
        else if (string.IsNullOrWhiteSpace(result.User))
        {
            result.Error = "--user は必須です。";
        }

        return result;
    }
}
