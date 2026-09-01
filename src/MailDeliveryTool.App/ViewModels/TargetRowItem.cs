using CommunityToolkit.Mvvm.ComponentModel;
using MailDeliveryTool.Core.Models;

namespace MailDeliveryTool.App.ViewModels;

/// <summary>
/// 「新しい配信」の宛先選択（要件定義書6章）で使う、選択チェック付きの宛先1件。
/// 「すべて」タブ・「メールリスト」タブの両方で使う共通の行モデル。
/// </summary>
public sealed partial class TargetRowItem : ObservableObject
{
    public Contact Contact { get; }

    /// <summary>「種別」軸の値の表示名（mock_prototype.htmlと同じく種別・技術領域は別列で表示する）。</summary>
    public string TypeCategoryText { get; }

    /// <summary>「技術領域」軸の値の表示名。</summary>
    public string TechFieldCategoryText { get; }

    [ObservableProperty]
    private bool _isChecked;

    public TargetRowItem(Contact contact, string typeCategoryText, string techFieldCategoryText, bool isChecked = true)
    {
        Contact = contact;
        TypeCategoryText = typeCategoryText;
        TechFieldCategoryText = techFieldCategoryText;
        _isChecked = isChecked;
    }
}
