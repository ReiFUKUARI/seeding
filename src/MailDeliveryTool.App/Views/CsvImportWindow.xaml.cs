using System.Linq;
using System.Windows;
using System.Windows.Media;
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

    // クリックでの選択に加え、ドロップゾーンへのドラッグ＆ドロップでもCSVファイルを選択できるようにする。
    private void OnDropZoneDragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        e.Effects = DragDropEffects.Copy;
        DropZoneBorder.Background = (Brush)FindResource("AccentSoftBrush");
    }

    private void OnDropZoneDragLeave(object sender, DragEventArgs e) => DropZoneBorder.Background = Brushes.Transparent;

    private void OnDropZoneDrop(object sender, DragEventArgs e)
    {
        DropZoneBorder.Background = Brushes.Transparent;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] filePaths && DataContext is CsvImportViewModel viewModel)
        {
            viewModel.HandleDroppedFiles(filePaths.ToList());
        }
    }
}
