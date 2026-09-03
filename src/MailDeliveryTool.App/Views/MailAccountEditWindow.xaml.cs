using System.Windows;
using MailDeliveryTool.App.ViewModels;

namespace MailDeliveryTool.App.Views;

/// <summary>「メールアカウントを変更」モーダル（要件定義書10.1）。
/// 送信元アドレス・送信者名以外（サーバー名・アカウント名・パスワード・ポート・暗号化方法）を
/// 必須項目とする。</summary>
public partial class MailAccountEditWindow : Window
{
    private readonly MailAccountEditViewModel _viewModel;

    public MailAccountEditWindow(MailAccountEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    // PasswordBox.Password はセキュリティ上の理由でデータバインディングに対応していないため、
    // イベントでViewModelへ手動同期する。
    private void OnPasswordChanged(object sender, RoutedEventArgs e) =>
        _viewModel.NewPassword = PasswordInput.Password;

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var missingFields = new List<string>();

        if (string.IsNullOrWhiteSpace(_viewModel.Host))
        {
            missingFields.Add("サーバー名");
        }

        if (string.IsNullOrWhiteSpace(_viewModel.UserName))
        {
            missingFields.Add("アカウント名");
        }

        // 既にパスワードが保存済みの場合（編集時）は、空のままでも既存のパスワードを
        // 引き継ぐため必須にしない。初回設定時（未保存）のみ必須とする。
        if (!_viewModel.HasExistingPassword && !_viewModel.PasswordChanged)
        {
            missingFields.Add("パスワード");
        }

        if (_viewModel.Port <= 0)
        {
            missingFields.Add("ポート");
        }

        if (string.IsNullOrWhiteSpace(_viewModel.SecureSocketOption))
        {
            missingFields.Add("暗号化方法");
        }

        if (missingFields.Count > 0)
        {
            MessageBox.Show(
                $"{string.Join("・", missingFields)}は必須です。",
                "メール配信ツール",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
