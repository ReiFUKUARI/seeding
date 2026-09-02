using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace MailDeliveryTool.App.Controls;

/// <summary>カテゴリ値の一覧をチップ表示する小さな再利用コントロール。</summary>
public partial class CategoryChipList : UserControl
{
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
        nameof(Items), typeof(IEnumerable), typeof(CategoryChipList));

    public IEnumerable? Items
    {
        get => (IEnumerable?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public CategoryChipList() => InitializeComponent();
}
