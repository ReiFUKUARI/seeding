using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace MailDeliveryTool.App.Controls;

/// <summary>
/// カテゴリ値の一覧をチップ表示する小さな再利用コントロール。
/// 3件以上のときは個別チップの代わりに「+件数」の1チップにまとめ、
/// マウスオーバーで全件をツールチップ表示する。
/// </summary>
public partial class CategoryChipList : UserControl
{
    private const int OverflowThreshold = 3;

    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
        nameof(Items), typeof(IEnumerable), typeof(CategoryChipList),
        new PropertyMetadata(null, OnItemsChanged));

    public IEnumerable? Items
    {
        get => (IEnumerable?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public CategoryChipList() => InitializeComponent();

    private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((CategoryChipList)d).Refresh();

    private void Refresh()
    {
        var names = Items?.Cast<object>().Select(o => o?.ToString() ?? string.Empty).ToList()
            ?? new List<string>();

        if (names.Count >= OverflowThreshold)
        {
            NormalItemsControl.Visibility = Visibility.Collapsed;
            OverflowChip.Visibility = Visibility.Visible;
            OverflowLabelText.Text = $"+{names.Count}";
            ToolTipService.SetToolTip(OverflowChip, new TextBlock
            {
                Text = string.Join(Environment.NewLine, names),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 240,
            });
        }
        else
        {
            NormalItemsControl.Visibility = Visibility.Visible;
            OverflowChip.Visibility = Visibility.Collapsed;
            NormalItemsControl.ItemsSource = names;
        }
    }
}
