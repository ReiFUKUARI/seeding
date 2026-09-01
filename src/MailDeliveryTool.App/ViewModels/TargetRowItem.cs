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

    /// <summary>この宛先が持つカテゴリ値の表示名（軸をまたいで結合したもの）。一覧表示用。</summary>
    public string CategoryText { get; }

    [ObservableProperty]
    private bool _isChecked;

    public TargetRowItem(Contact contact, string categoryText, bool isChecked = true)
    {
        Contact = contact;
        CategoryText = categoryText;
        _isChecked = isChecked;
    }
}
