using CommunityToolkit.Mvvm.ComponentModel;
using MailDeliveryTool.Core.Models;

namespace MailDeliveryTool.App.ViewModels;

/// <summary>「宛先を追加」モーダルのビューモデル（要件定義書5.1）。</summary>
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

    public ContactEditViewModel(IEnumerable<CategoryAxis> axes)
    {
        CategoryAxes = SelectableCategoryAxis.BuildFrom(axes);
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
}
