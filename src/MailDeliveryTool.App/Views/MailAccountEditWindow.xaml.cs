using System.Windows;
using MailDeliveryTool.App.ViewModels;

namespace MailDeliveryTool.App.Views;

/// <summary>「メールアカウントを変更」モーダル（要件定義書10.1）。</summary>
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
        if (string.IsNullOrWhiteSpace(_viewModel.Host)
            || string.IsNullOrWhiteSpace(_viewModel.UserName)
            || string.IsNullOrWhiteSpace(_viewModel.FromAddress))
        {
            MessageBox.Show(
                "サーバー名・アカウント名・送信元アドレスは必須です。",
                "メール配信ツール",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
