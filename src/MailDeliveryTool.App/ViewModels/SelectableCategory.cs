using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MailDeliveryTool.Core.Models;

namespace MailDeliveryTool.App.ViewModels;

/// <summary>
/// カテゴリ値1件をチェック状態付きで表示するためのラッパー。
/// パートナーリストの検索フィルタと、宛先編集モーダルのカテゴリ選択の両方で使う。
/// </summary>
/// <remarks>
/// <see cref="CategoryValue"/> 自体は <see cref="Services.CategoryStore"/> がアプリ全体で
/// 共有する単一のインスタンス（D-003）のため、選択状態をそちらに持たせてはいけない。
/// 画面ごとに一時的な選択状態だけをこちらで保持する。
/// </remarks>
public sealed partial class SelectableCategoryValue : ObservableObject
{
    public CategoryValue Value { get; }

    [ObservableProperty]
    private bool _isSelected;

    public SelectableCategoryValue(CategoryValue value, bool isSelected = false)
    {
        Value = value;
        _isSelected = isSelected;
    }
}

/// <summary>軸1つ分の選択可能なカテゴリ値一覧。</summary>
public sealed class SelectableCategoryAxis
{
    public long AxisId { get; }
    public string Name { get; }
    public ObservableCollection<SelectableCategoryValue> Values { get; } = new();

    public SelectableCategoryAxis(long axisId, string name)
    {
        AxisId = axisId;
        Name = name;
    }

    /// <summary>
    /// <see cref="Services.CategoryStore"/> の軸一覧から選択可能な軸一覧を組み立てる。
    /// </summary>
    /// <param name="axes">元になる軸一覧（<c>CategoryStore.Axes</c>）。</param>
    /// <param name="selectedValueIds">最初からチェック状態にする値ID（省略時はすべて未選択）。</param>
    public static List<SelectableCategoryAxis> BuildFrom(
        IEnumerable<CategoryAxis> axes, IReadOnlySet<long>? selectedValueIds = null)
    {
        var result = new List<SelectableCategoryAxis>();
        foreach (var axis in axes)
        {
            var selectableAxis = new SelectableCategoryAxis(axis.Id, axis.Name);
            foreach (var value in axis.Values)
            {
                selectableAxis.Values.Add(
                    new SelectableCategoryValue(value, selectedValueIds?.Contains(value.Id) ?? false));
            }

            result.Add(selectableAxis);
        }

        return result;
    }
}
