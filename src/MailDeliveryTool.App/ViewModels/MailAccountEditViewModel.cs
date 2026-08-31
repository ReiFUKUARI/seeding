using CommunityToolkit.Mvvm.ComponentModel;
using MailDeliveryTool.Core.Models;

namespace MailDeliveryTool.App.ViewModels;

/// <summary>「メールアカウントを変更」モーダルのビューモデル（要件定義書10.1）。</summary>
public sealed partial class MailAccountEditViewModel : ObservableObject
{
    [ObservableProperty]
    private string _host = string.Empty;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private int _port = 587;

    [ObservableProperty]
    private string _secureSocketOption = "Auto";

    [ObservableProperty]
    private string _fromAddress = string.Empty;

    [ObservableProperty]
    private string? _fromDisplayName;

    /// <summary>パスワードを変更する場合のみ入力する（要件定義書10.1）。</summary>
    [ObservableProperty]
    private string _newPassword = string.Empty;

    /// <summary>入力欄で選べる暗号化方式の一覧（MailKitのSecureSocketOptions名）。</summary>
    public IReadOnlyList<string> SecureSocketOptions { get; } =
        new[] { "Auto", "StartTls", "StartTlsWhenAvailable", "SslOnConnect", "None" };

    /// <summary>パスワード欄に入力があったか（true の場合のみ保存時に再暗号化する）。</summary>
    public bool PasswordChanged => !string.IsNullOrEmpty(NewPassword);

    public static MailAccountEditViewModel FromModel(MailAccountSetting setting) => new()
    {
        Host = setting.Host,
        UserName = setting.UserName,
        Port = setting.Port,
        SecureSocketOption = setting.SecureSocketOption,
        FromAddress = setting.FromAddress,
        FromDisplayName = setting.FromDisplayName,
    };

    /// <summary>
    /// 入力内容から更新後のモデルを組み立てる。パスワードを変更していない場合は
    /// <paramref name="existingPassword"/>（変更前のもの）をそのまま使う。
    /// </summary>
    public MailAccountSetting ToModel(string? existingPassword) => new()
    {
        Host = Host.Trim(),
        UserName = UserName.Trim(),
        Port = Port,
        SecureSocketOption = SecureSocketOption,
        FromAddress = FromAddress.Trim(),
        FromDisplayName = string.IsNullOrWhiteSpace(FromDisplayName) ? null : FromDisplayName.Trim(),
        Password = PasswordChanged ? NewPassword : existingPassword,
    };
}
