using System.Security.Cryptography;
using System.Text;

namespace MailDeliveryTool.Core.Security;

/// <summary>
/// DPAPI（DataProtectionScope.CurrentUser）で文字列を暗号化・復号する。
/// 要件定義書 12章 / 13章：追加ライブラリなしで Windows ログインユーザーに紐づける。
/// </summary>
/// <remarks>
/// CurrentUser スコープのため、別のWindowsユーザーや別PCでは復号できない。
/// これは仕様（1人1台・個人完結）どおりの挙動だが、
/// PC入れ替え時はパスワードの再入力が必要になる点を運用で周知すること。
/// </remarks>
public static class DpapiSecretProtector
{
    // 暗号文の取り違えを防ぐための追加エントロピー（固定値でよい）
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MailDeliveryTool.MailAccount.v1");

    public static byte[]? Protect(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return null;
        }

        var bytes = Encoding.UTF8.GetBytes(plainText);
        return ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
    }

    /// <summary>
    /// 復号する。復号できない場合（別ユーザー・別PCへの持ち込み等）は null を返し、
    /// 呼び出し側で「パスワードの再設定が必要」として扱う。
    /// </summary>
    public static string? Unprotect(byte[]? cipherText)
    {
        if (cipherText is null || cipherText.Length == 0)
        {
            return null;
        }

        try
        {
            var bytes = ProtectedData.Unprotect(cipherText, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
