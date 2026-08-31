using System.Windows;
using MailDeliveryTool.App.ViewModels;

namespace MailDeliveryTool.App.Views;

/// <summary>「CSVから取り込む」モーダル（パートナーリスト画面のCSV機能）。</summary>
public partial class CsvImportWindow : Window
{
    public CsvImportWindow(CsvImportViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
