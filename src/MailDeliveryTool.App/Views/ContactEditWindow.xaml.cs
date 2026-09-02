using System.Net.Mail;
using System.Windows;
using MailDeliveryTool.App.ViewModels;

namespace MailDeliveryTool.App.Views;

/// <summary>「宛先を追加」モーダル（要件定義書5.1）。</summary>
public partial class ContactEditWindow : Window
{
    private readonly ContactEditViewModel _viewModel;

    public ContactEditWindow(ContactEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        if (viewModel.IsEditMode)
        {
            Title = "宛先を編集";
            SaveButton.Content = "保存する";
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.CompanyName)
            || string.IsNullOrWhiteSpace(_viewModel.ContactName)
            || string.IsNullOrWhiteSpace(_viewModel.Email))
        {
            MessageBox.Show(
                "会社名・担当者名・メールアドレスは必須です。",
                "メール配信ツール",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!IsValidEmail(_viewModel.Email))
        {
            MessageBox.Show(
                "メールアドレスの形式が正しくありません。",
                "メール配信ツール",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email.Trim());
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
