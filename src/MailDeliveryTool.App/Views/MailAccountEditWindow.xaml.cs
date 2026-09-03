using System.Windows;
using MailDeliveryTool.App.ViewModels;

namespace MailDeliveryTool.App.Views;

/// <summary>「メールアカウントを変更」モーダル（要件定義書10.1）。
/// 送信者名以外（サーバー名・アカウント名・パスワード・ポート・暗号化方法・送信元アドレス）を
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

        if (string.IsNullOrWhiteSpace(_viewModel.FromAddress))
        {
            missingFields.Add("送信元アドレス");
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

        // DBのCHECK制約（Port BETWEEN 1 AND 65535）と同じ範囲を保存前に検証する。
        // ここで弾かないと、保存時にSqliteExceptionが未処理のままダイアログに出て、
        // 「ポートは必須です」等の分かりやすいメッセージにならない。
        if (_viewModel.Port is < 1 or > 65535)
        {
            MessageBox.Show(
                "ポートは1〜65535の範囲で入力してください。",
                "メール配信ツール",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
