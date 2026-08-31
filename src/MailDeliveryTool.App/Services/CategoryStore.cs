using System.Collections.ObjectModel;
using MailDeliveryTool.Core.Data.Repositories;
using MailDeliveryTool.Core.Models;

namespace MailDeliveryTool.App.Services;

/// <summary>
/// カテゴリ軸・値をアプリ全体で共有する単一のストア（D-003）。
/// 「新しい配信」「パートナーリスト」「設定」の各画面・モーダルはこの
/// <see cref="Axes"/> を購読することで、値の追加・削除が即座に全画面へ反映される。
/// 各画面が個別にDBへ問い合わせる実装にはしないこと（反映漏れの温床になる）。
/// </summary>
/// <remarks>
/// CategoryAxis/CategoryValue はINotifyPropertyChangedを実装しない単純なクラスのため、
/// 更新のたびに Axes を Clear して丸ごと詰め直す。ObservableCollectionの
/// CollectionChanged通知だけで、ネストしたカテゴリ値一覧までWPF側が正しく再描画される
/// （バインド先のアイテムが毎回新しいオブジェクトに置き換わるため）。
/// </remarks>
public sealed class CategoryStore
{
    private readonly CategoryRepository _repository;

    public ObservableCollection<CategoryAxis> Axes { get; } = new();

    public CategoryStore(CategoryRepository repository)
    {
        _repository = repository;
        Reload();
    }

    /// <summary>DBから再読み込みし、共有コレクションを更新する。</summary>
    public void Reload()
    {
        var axes = _repository.GetAxes();
        Axes.Clear();
        foreach (var axis in axes)
        {
            Axes.Add(axis);
        }
    }

    /// <summary>値を追加し、ストアへ反映する。同一軸内で重複する場合は例外を投げる。</summary>
    public void AddValue(long axisId, string name)
    {
        _repository.AddValue(axisId, name);
        Reload();
    }

    /// <summary>値を削除し、ストアへ反映する。</summary>
    public void DeleteValue(long valueId)
    {
        _repository.DeleteValue(valueId);
        Reload();
    }

    /// <summary>このカテゴリ値を使用している宛先の件数（削除確認の警告表示用）。</summary>
    public int GetUsageCount(long valueId) => _repository.GetUsageCount(valueId);
}
