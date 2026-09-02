using System.Windows;
using MailDeliveryTool.App.ViewModels;

namespace MailDeliveryTool.App.Views;

/// <summary>「今すぐバックアップする」の実行状況モーダル。</summary>
public partial class BackupProgressWindow : Window
{
    public BackupProgressWindow(BackupProgressViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
    }
}
