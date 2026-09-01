using System.Windows;
using System.Windows.Controls;

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
}
