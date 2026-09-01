using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MailDeliveryTool.App.Services;
using MailDeliveryTool.App.Views;
using MailDeliveryTool.Core.Data.Repositories;
using MailDeliveryTool.Core.Models;

namespace MailDeliveryTool.App.ViewModels;

/// <summary>パートナーリスト画面のビューモデル（要件定義書5章：検索・一覧・登録・停止管理）。</summary>
public sealed partial class PartnersViewModel : ObservableObject
{
    private readonly ContactRepository _contactRepository;
    private readonly CategoryStore _categoryStore;

    public PartnersViewModel(ContactRepository contactRepository, CategoryStore categoryStore)
    {
        _contactRepository = contactRepository;
        _categoryStore = categoryStore;

        // カテゴリ値の追加・削除をこの画面の検索フィルタにも即時反映する（D-003）
        _categoryStore.Axes.CollectionChanged += (_, _) => RebuildFilterAxes();

        RebuildFilterAxes();
        RunSearch();
    }

    public ObservableCollection<PartnerListItem> Contacts { get; } = new();

    /// <summary>検索フィルタ用のカテゴリ軸・値（CategoryStoreをこの画面用に選択可能な形へ変換したもの）。</summary>
    public ObservableCollection<SelectableCategoryAxis> FilterAxes { get; } = new();

    /// <summary>会社名・担当者名・メモを横断した部分一致キーワード（要件定義書5章のパートナーリスト検索）。</summary>
    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    partial void OnSearchKeywordChanged(string value) => RunSearch();

    [ObservableProperty]
    private string _resultCountText = string.Empty;

    private void RebuildFilterAxes()
    {
        // 軸の再構築後も、既にチェックしていた値は可能な限り選択状態を保つ
        var previouslySelected = FilterAxes
            .SelectMany(axis => axis.Values.Where(v => v.IsSelected).Select(v => v.Value.Id))
            .ToHashSet();

        FilterAxes.Clear();
        foreach (var axis in SelectableCategoryAxis.BuildFrom(_categoryStore.Axes, previouslySelected))
        {
            foreach (var value in axis.Values)
            {
                value.PropertyChanged += (_, _) => RunSearch();
            }

            FilterAxes.Add(axis);
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchKeyword = string.Empty;
        foreach (var axis in FilterAxes)
        {
            foreach (var value in axis.Values)
            {
                value.IsSelected = false;
            }
        }

        RunSearch();
    }

    private void RunSearch()
    {
        var axisFilters = FilterAxes.ToDictionary(
            axis => axis.AxisId,
            axis => (IReadOnlyList<long>)axis.Values.Where(v => v.IsSelected).Select(v => v.Value.Id).ToList());

        // パートナーリストは停止中の管理そのものが目的の画面のため、停止中も含めて表示する。
        // 検索キーワードは会社名だけでなく担当者名・メモにも一致させる（mock_prototype.htmlの仕様）
        var results = _contactRepository.Search(
            SearchKeyword, axisFilters, includeSuspended: true, matchContactNameAndMemo: true);

        Contacts.Clear();
        foreach (var contact in results)
        {
            Contacts.Add(new PartnerListItem(
                contact,
                DescribeCategory(contact, CategoryAxis.TypeAxisId),
                DescribeCategory(contact, CategoryAxis.TechFieldAxisId)));
        }

        ResultCountText = $"{Contacts.Count} 件";
    }

    /// <summary>指定した軸についてのみ、この宛先が持つカテゴリ値の表示名を返す（mock_prototype.htmlの列構成に合わせ、種別/技術領域を別列にするため）。</summary>
    private string DescribeCategory(Contact contact, long axisId)
    {
        var names = _categoryStore.Axes
            .Where(axis => axis.Id == axisId)
            .SelectMany(axis => axis.Values)
            .Where(value => contact.CategoryValueIds.Contains(value.Id))
            .Select(value => value.Name);
        return string.Join(" / ", names);
    }

    /// <summary>停止／再開を切り替える（要件定義書5.2）。</summary>
    [RelayCommand]
    private void ToggleSuspend(PartnerListItem? item)
    {
        if (item is null)
        {
            return;
        }

        _contactRepository.SetSuspended(item.Contact.Id, !item.Contact.IsSuspended);
        RunSearch();
    }

    [RelayCommand]
    private void OpenAddContact()
    {
        var editViewModel = new ContactEditViewModel(_categoryStore.Axes);
        var window = new ContactEditWindow(editViewModel) { Owner = Application.Current.MainWindow };
        if (window.ShowDialog() == true)
        {
            _contactRepository.Add(editViewModel.ToNewContact());
            RunSearch();
        }
    }

    [RelayCommand]
    private void OpenCsvImport()
    {
        var importViewModel = new CsvImportViewModel(_contactRepository, _categoryStore);
        var window = new CsvImportWindow(importViewModel) { Owner = Application.Current.MainWindow };
        window.ShowDialog();
        // インポートの成否によらず、モーダルを閉じたら一覧を最新化する（未実施なら無害な再検索になるだけ）
        RunSearch();
    }
}

/// <summary>パートナーリスト1行分の表示用ラッパー。</summary>
public sealed class PartnerListItem
{
    public Contact Contact { get; }

    /// <summary>「種別」軸の値の表示名（mock_prototype.htmlと同じく種別・技術領域は別列で表示する）。</summary>
    public string TypeCategoryText { get; }

    /// <summary>「技術領域」軸の値の表示名。</summary>
    public string TechFieldCategoryText { get; }

    public string StatusText => Contact.IsSuspended ? "停止中" : "配信中";
    public string ToggleLabel => Contact.IsSuspended ? "再開" : "停止";

    public PartnerListItem(Contact contact, string typeCategoryText, string techFieldCategoryText)
    {
        Contact = contact;
        TypeCategoryText = typeCategoryText;
        TechFieldCategoryText = techFieldCategoryText;
    }
}
