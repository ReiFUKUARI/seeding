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

    /// <summary>「種別」軸の値の表示名一覧（mock_prototype.htmlのタグチップ表示に合わせ、結合テキストではなく一覧で持つ）。</summary>
    public IReadOnlyList<string> TypeCategoryNames { get; }

    /// <summary>「技術領域」軸の値の表示名一覧。</summary>
    public IReadOnlyList<string> TechFieldCategoryNames { get; }

    [ObservableProperty]
    private bool _isChecked;

    public TargetRowItem(
        Contact contact,
        IReadOnlyList<string> typeCategoryNames,
        IReadOnlyList<string> techFieldCategoryNames,
        bool isChecked = true)
    {
        Contact = contact;
        TypeCategoryNames = typeCategoryNames;
        TechFieldCategoryNames = techFieldCategoryNames;
        _isChecked = isChecked;
    }
}
