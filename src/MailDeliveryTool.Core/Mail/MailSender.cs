using MailKit.Net.Smtp;
using MimeKit;
using MailDeliveryTool.Core.Models;

namespace MailDeliveryTool.Core.Mail;

/// <summary>送信する1通の内容。宛先ごとの本文タグ置換・添付ファイルの付与はこのクラスが担う。</summary>
public sealed class MailSendRequest
{
    public required string Subject { get; init; }

    /// <summary>#会社名# #担当者名# #メールアドレス# のタグを含みうる本文テンプレート。件名は置換対象外。</summary>
    public required string BodyTemplate { get; init; }

    public required IReadOnlyList<Contact> Targets { get; init; }

    public IReadOnlyList<string> AttachmentFilePaths { get; init; } = Array.Empty<string>();

    /// <summary>本文の末尾に「-- 」区切りで自動挿入する署名。nullまたは空文字なら挿入しない。</summary>
    public string? SignatureText { get; init; }
}

/// <summary>1件分の送信結果。</summary>
public sealed record SendItemResult(Contact Contact, bool Success, string? ErrorMessage);

/// <summary>送信の進捗（要件定義書9章：送信中の画面表示用）。</summary>
public sealed record SendProgress(int SentCount, int TotalCount, SendItemResult LastResult);

/// <summary>
/// SMTP送信の実行を担う（要件定義書9章）。
/// 1つの接続を使い回して全件を送信し、75通/分のペースを守るために送信の間隔を空ける。
/// 1件の送信失敗で処理全体を止めず、要件定義書4章「送信中の中断機能は不要。
/// 開始したら最後まで自動完了」のとおり最後まで実行しきる。
/// </summary>
public sealed class MailSender
{
    /// <summary>要件定義書9章：75通/分のペース制限。1通あたりの最短間隔。</summary>
    private static readonly TimeSpan PacingInterval = TimeSpan.FromMilliseconds(60_000.0 / 75.0);

    public async Task<IReadOnlyList<SendItemResult>> SendAsync(
        MailAccountSetting account,
        MailSendRequest request,
        IProgress<SendProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SendItemResult>();
        if (request.Targets.Count == 0)
        {
            return results;
        }

        // 添付ファイルは宛先の件数分読み直すと無駄なディスクI/Oになるため、先に1回だけ読み込んでおく
        var attachments = ReadAttachments(request.AttachmentFilePaths);

        var option = SmtpConnectionTester.ParseOption(account.SecureSocketOption);
        using var client = new SmtpClient();
        await client.ConnectAsync(account.Host, account.Port, option, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(account.Password))
        {
            await client.AuthenticateAsync(account.UserName, account.Password, cancellationToken)
                .ConfigureAwait(false);
        }

        for (var i = 0; i < request.Targets.Count; i++)
        {
            var contact = request.Targets[i];
            SendItemResult itemResult;
            try
            {
                var message = BuildMessage(account, request, contact, attachments);
                await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
                itemResult = new SendItemResult(contact, true, null);
            }
            catch (Exception ex)
            {
                itemResult = new SendItemResult(contact, false, Describe(ex));
            }

            results.Add(itemResult);
            progress?.Report(new SendProgress(i + 1, request.Targets.Count, itemResult));

            var isLast = i == request.Targets.Count - 1;
            if (!isLast)
            {
                await Task.Delay(PacingInterval, cancellationToken).ConfigureAwait(false);
            }
        }

        await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
        return results;
    }

    private static List<AttachmentContent> ReadAttachments(IReadOnlyList<string> filePaths) =>
        filePaths.Select(path => new AttachmentContent(Path.GetFileName(path), File.ReadAllBytes(path))).ToList();

    private static MimeMessage BuildMessage(
        MailAccountSetting account, MailSendRequest request, Contact contact, IReadOnlyList<AttachmentContent> attachments)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(account.FromDisplayName ?? string.Empty, account.FromAddress));
        message.To.Add(new MailboxAddress(contact.ContactName, contact.Email));
        message.Subject = request.Subject;

        var body = MailBodyComposer.AppendSignature(
            MailBodyComposer.Substitute(request.BodyTemplate, contact), request.SignatureText);

        var builder = new BodyBuilder { TextBody = body };
        foreach (var attachment in attachments)
        {
            builder.Attachments.Add(attachment.FileName, attachment.Content);
        }

        message.Body = builder.ToMessageBody();
        return message;
    }

    private static string Describe(Exception ex) => ex switch
    {
        SmtpCommandException smtp => $"[{smtp.StatusCode}] {smtp.Message}",
        _ => ex.Message,
    };

    private sealed record AttachmentContent(string FileName, byte[] Content);
}
