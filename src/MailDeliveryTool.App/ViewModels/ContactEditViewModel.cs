using CommunityToolkit.Mvvm.ComponentModel;
using MailDeliveryTool.Core.Models;

namespace MailDeliveryTool.App.ViewModels;

/// <summary>「宛先を追加」／「宛先を編集」モーダルのビューモデル（要件定義書5.1）。</summary>
public sealed partial class ContactEditViewModel : ObservableObject
{
    [ObservableProperty]
    private string _companyName = string.Empty;

    [ObservableProperty]
    private string _contactName = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    /// <summary>任意項目。上限100文字（DBのCK_Contact_Memo_Lengthと一致させる）。</summary>
    [ObservableProperty]
    private string _memo = string.Empty;

    public List<SelectableCategoryAxis> CategoryAxes { get; }

    /// <summary>既存の宛先を編集中かどうか（ウィンドウのタイトル・ボタン文言の出し分けに使う）。</summary>
    public bool IsEditMode { get; }

    /// <summary>「宛先を追加」用。</summary>
    public ContactEditViewModel(IEnumerable<CategoryAxis> axes)
    {
        CategoryAxes = SelectableCategoryAxis.BuildFrom(axes);
        IsEditMode = false;
    }

    /// <summary>「宛先を編集」用。既存の値で初期表示する。</summary>
    public ContactEditViewModel(IEnumerable<CategoryAxis> axes, Contact existing)
    {
        CategoryAxes = SelectableCategoryAxis.BuildFrom(axes, existing.CategoryValueIds.ToHashSet());
        IsEditMode = true;
        CompanyName = existing.CompanyName;
        ContactName = existing.ContactName;
        Email = existing.Email;
        Memo = existing.Memo ?? string.Empty;
    }

    public Contact ToNewContact() => new()
    {
        CompanyName = CompanyName.Trim(),
        ContactName = ContactName.Trim(),
        Email = Email.Trim(),
        Memo = string.IsNullOrWhiteSpace(Memo) ? null : Memo.Trim(),
        CategoryValueIds = CategoryAxes
            .SelectMany(axis => axis.Values.Where(v => v.IsSelected).Select(v => v.Value.Id))
            .ToList(),
    };

    /// <summary>編集内容を反映した更新後の宛先を返す（Id・停止状態は<paramref name="original"/>から引き継ぐ）。</summary>
    public Contact ToUpdatedContact(Contact original) => new()
    {
        Id = original.Id,
        CompanyName = CompanyName.Trim(),
        ContactName = ContactName.Trim(),
        Email = Email.Trim(),
        Memo = string.IsNullOrWhiteSpace(Memo) ? null : Memo.Trim(),
        IsSuspended = original.IsSuspended,
        CategoryValueIds = CategoryAxes
            .SelectMany(axis => axis.Values.Where(v => v.IsSelected).Select(v => v.Value.Id))
            .ToList(),
    };
}
