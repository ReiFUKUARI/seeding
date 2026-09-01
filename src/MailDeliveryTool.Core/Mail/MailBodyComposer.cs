using MailDeliveryTool.Core.Models;

namespace MailDeliveryTool.Core.Mail;

/// <summary>
/// 本文中のタグ置換と、末尾への署名自動挿入を担う（要件定義書7章）。
/// メール作成画面のプレビュー・確認画面・実際の送信の3箇所で同じ結果になる必要があるため、
/// ロジックをここに集約する（各画面が個別に同じ置換処理を持つと差異が生まれる温床になる）。
/// </summary>
public static class MailBodyComposer
{
    /// <summary>本文テンプレート中の#会社名# #担当者名# #メールアドレス#を宛先の値へ置換する。件名は置換対象外。</summary>
    public static string Substitute(string bodyTemplate, Contact contact) =>
        bodyTemplate.Replace("#会社名#", contact.CompanyName)
                    .Replace("#担当者名#", contact.ContactName)
                    .Replace("#メールアドレス#", contact.Email);

    /// <summary>本文の末尾に「-- 」区切りで署名を追加する。署名がnull/空文字なら何も追加しない。</summary>
    public static string AppendSignature(string body, string? signature) =>
        string.IsNullOrEmpty(signature) ? body : body + "\n\n-- \n" + signature;
}
