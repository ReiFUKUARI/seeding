using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MailDeliveryTool.App.ViewModels;

namespace MailDeliveryTool.App.Views;

/// <summary>「新しい配信」ウィザード（要件定義書6〜8章）。DataContextはMainWindowから設定される。</summary>
public partial class ComposeView : UserControl
{
    public ComposeView() => InitializeComponent();

    // ContextMenuは左クリックでは自動的に開かないため、コードビハインドで明示的に開く。
    // ContextMenuは論理ツリー上は独立しているため、XAML側でPlacementTarget経由のDataContext
    // バインドを設定している（Buttonから直接DataContextを継承しない）。
    private void OnInsertTagButtonClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        if (button.ContextMenu is { } menu)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    // タグはBodyTextBoxの現在のキャレット位置に挿入する（mock_prototype.htmlのinsertTag()相当）。
    // ContextMenuのDataContextはPlacementTarget.DataContext経由でComposeViewModelに
    // バインドされているため、MenuItem（sender）のDataContextからも同じものを取得できる。
    private void OnInsertTagMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ComposeViewModel viewModel, Tag: string tagName })
        {
            return;
        }

        var newCaretIndex = viewModel.InsertTagAt(tagName, BodyTextBox.CaretIndex);
        BodyTextBox.Focus();
        BodyTextBox.CaretIndex = newCaretIndex;
    }

    // クリックでの選択に加え、ドロップゾーンへのドラッグ＆ドロップでも添付ファイルを追加できるようにする。
    private void OnAttachmentDropZoneDragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        e.Effects = DragDropEffects.Copy;
        AttachmentDropZoneBorder.Background = (Brush)FindResource("AccentSoftBrush");
    }

    private void OnAttachmentDropZoneDragLeave(object sender, DragEventArgs e) =>
        AttachmentDropZoneBorder.Background = Brushes.Transparent;

    private void OnAttachmentDropZoneDrop(object sender, DragEventArgs e)
    {
        AttachmentDropZoneBorder.Background = Brushes.Transparent;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] filePaths && DataContext is ComposeViewModel viewModel)
        {
            viewModel.AddDroppedAttachments(filePaths.ToList());
        }
    }
}
