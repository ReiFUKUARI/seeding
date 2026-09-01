using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MailDeliveryTool.App.Services;
using MailDeliveryTool.App.Views;
using MailDeliveryTool.Core.Data.Repositories;
using MailDeliveryTool.Core.Models;
using Microsoft.Win32;

namespace MailDeliveryTool.App.ViewModels;

/// <summary>「新しい配信」ウィザードの3ステップ（要件定義書6〜8章）。送信実行はフェーズ5⑥で実装する。</summary>
public enum ComposeStep
{
    TargetSelection,
    Compose,
    Confirmation,
}

/// <summary>宛先選択画面の2タブ（要件定義書6.1）。</summary>
public enum TargetTab
{
    Search,
    List,
}

/// <summary>
/// 「新しい配信」ウィザード全体のビューモデル（要件定義書6〜8章）。
/// mock_prototype.htmlのグローバル状態（contacts検索結果・addedList・確認画面）を
/// 1つのビューモデルにまとめたもの。宛先選択→メール作成→送信前の確認までを扱う。
/// 実際の送信（フェーズ5⑥：送信エンジン）は<see cref="StartSendingCommand"/>から呼ぶ想定で、
/// 現時点ではプレースホルダーになっている。
/// </summary>
public sealed partial class ComposeViewModel : ObservableObject
{
    /// <summary>添付ファイル合計サイズの上限（D-007：13MB）。</summary>
    private const long AttachmentSizeLimitBytes = 13L * 1024 * 1024;

    private static readonly string[] KnownTagNames = { "会社名", "担当者名", "メールアドレス" };

    private readonly ContactRepository _contactRepository;
    private readonly CategoryStore _categoryStore;
    private readonly AppSettingRepository _appSettingRepository;

    public ComposeViewModel(
        ContactRepository contactRepository, CategoryStore categoryStore, AppSettingRepository appSettingRepository)
    {
        _contactRepository = contactRepository;
        _categoryStore = categoryStore;
        _appSettingRepository = appSettingRepository;

        _categoryStore.Axes.CollectionChanged += (_, _) => RebuildFilterAxes();
        RebuildFilterAxes();
        RunSearch();
    }

    [ObservableProperty]
    private ComposeStep _currentStep = ComposeStep.TargetSelection;

    partial void OnCurrentStepChanged(ComposeStep value)
    {
        OnPropertyChanged(nameof(TargetStepVisibility));
        OnPropertyChanged(nameof(ComposeStepVisibility));
        OnPropertyChanged(nameof(ConfirmationStepVisibility));
    }

    public Visibility TargetStepVisibility =>
        CurrentStep == ComposeStep.TargetSelection ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ComposeStepVisibility =>
        CurrentStep == ComposeStep.Compose ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ConfirmationStepVisibility =>
        CurrentStep == ComposeStep.Confirmation ? Visibility.Visible : Visibility.Collapsed;

    // ================= ① 宛先選択（要件定義書6章） =================

    public ObservableCollection<TargetRowItem> SearchResults { get; } = new();
    public ObservableCollection<TargetRowItem> MailingList { get; } = new();
    public ObservableCollection<SelectableCategoryAxis> FilterAxes { get; } = new();

    [ObservableProperty]
    private string _companyKeyword = string.Empty;

    partial void OnCompanyKeywordChanged(string value) => RunSearch();

    [ObservableProperty]
    private TargetTab _activeTab = TargetTab.Search;

    partial void OnActiveTabChanged(TargetTab value)
    {
        OnPropertyChanged(nameof(SearchTabVisibility));
        OnPropertyChanged(nameof(ListTabVisibility));
    }

    public Visibility SearchTabVisibility => ActiveTab == TargetTab.Search ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ListTabVisibility => ActiveTab == TargetTab.List ? Visibility.Visible : Visibility.Collapsed;

    [RelayCommand]
    private void SwitchToSearchTab() => ActiveTab = TargetTab.Search;

    [RelayCommand]
    private void SwitchToListTab() => ActiveTab = TargetTab.List;

    [ObservableProperty]
    private string _searchCountText = "0";

    [ObservableProperty]
    private string _listCountText = "0";

    [ObservableProperty]
    private bool _isAllSearchChecked = true;

    partial void OnIsAllSearchCheckedChanged(bool value)
    {
        foreach (var row in SearchResults)
        {
            row.IsChecked = value;
        }
    }

    [ObservableProperty]
    private bool _isAllListChecked = true;

    partial void OnIsAllListCheckedChanged(bool value)
    {
        foreach (var row in MailingList)
        {
            row.IsChecked = value;
        }

        UpdateCanConfirmTarget();
    }

    [ObservableProperty]
    private bool _canConfirmTarget;

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
        CompanyKeyword = string.Empty;
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

        // 「新しい配信」は停止中を常に除外する（要件定義書5.2・6.1）
        var results = _contactRepository.Search(CompanyKeyword, axisFilters, includeSuspended: false);

        SearchResults.Clear();
        foreach (var contact in results)
        {
            SearchResults.Add(new TargetRowItem(contact, DescribeCategories(contact), isChecked: true));
        }

        SearchCountText = SearchResults.Count.ToString();
        // 新しい検索結果は初期値ですべて選択状態にする（mock_prototype.htmlのapplyFilterと同じ方針）
        IsAllSearchChecked = true;
    }

    private string DescribeCategories(Contact contact)
    {
        var names = _categoryStore.Axes
            .SelectMany(axis => axis.Values)
            .Where(value => contact.CategoryValueIds.Contains(value.Id))
            .Select(value => value.Name);
        return string.Join(" / ", names);
    }

    [RelayCommand]
    private void AddCheckedToList()
    {
        var checkedRows = SearchResults.Where(r => r.IsChecked).ToList();
        var addedCount = 0;
        foreach (var row in checkedRows)
        {
            if (MailingList.Any(m => m.Contact.Email == row.Contact.Email))
            {
                continue;
            }

            var newRow = new TargetRowItem(row.Contact, row.CategoryText, isChecked: true);
            newRow.PropertyChanged += (_, _) => UpdateCanConfirmTarget();
            MailingList.Add(newRow);
            addedCount++;
        }

        ListCountText = MailingList.Count.ToString();
        UpdateCanConfirmTarget();
        if (addedCount > 0)
        {
            ActiveTab = TargetTab.List;
        }
    }

    [RelayCommand]
    private void RemoveFromList(TargetRowItem? item)
    {
        if (item is null)
        {
            return;
        }

        MailingList.Remove(item);
        ListCountText = MailingList.Count.ToString();
        UpdateCanConfirmTarget();
    }

    private void UpdateCanConfirmTarget() => CanConfirmTarget = MailingList.Any(m => m.IsChecked);

    [RelayCommand]
    private void ConfirmTarget()
    {
        var finalTargets = MailingList.Where(m => m.IsChecked).Select(m => m.Contact).ToList();
        if (finalTargets.Count == 0)
        {
            MessageBox.Show(
                "確定する宛先にチェックを入れてください。", "メール配信ツール", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ConfirmedTargets = finalTargets;
        TargetCountText = finalTargets.Count.ToString();
        UpdatePreview();
        CurrentStep = ComposeStep.Compose;
    }

    [RelayCommand]
    private void GoBackToTarget()
    {
        ActiveTab = TargetTab.List;
        CurrentStep = ComposeStep.TargetSelection;
    }

    // ================= ② メール作成（要件定義書7章） =================

    public IReadOnlyList<Contact> ConfirmedTargets { get; private set; } = new List<Contact>();

    [ObservableProperty]
    private string _targetCountText = "0";

    [ObservableProperty]
    private string _subject = string.Empty;

    [ObservableProperty]
    private string _body = string.Empty;

    partial void OnBodyChanged(string value)
    {
        UpdatePreview();
        BodyErrorText = string.Empty;
    }

    public ObservableCollection<AttachmentItem> Attachments { get; } = new();

    [ObservableProperty]
    private string _previewText = string.Empty;

    [ObservableProperty]
    private string _bodyErrorText = string.Empty;

    [ObservableProperty]
    private string _attachmentErrorText = string.Empty;

    [ObservableProperty]
    private string _targetErrorText = string.Empty;

    private string GetSignatureText()
    {
        var signature = _appSettingRepository.GetSignature();
        return string.IsNullOrWhiteSpace(signature) ? string.Empty : signature;
    }

    /// <summary>
    /// 本文の下に表示する署名の案内文。署名は設定画面で編集する（<see cref="RefreshSignature"/>参照）。
    /// </summary>
    public string SignatureNoteText
    {
        get
        {
            var signature = GetSignatureText();
            return string.IsNullOrEmpty(signature)
                ? "署名は未設定です。「設定」タブから登録できます。"
                : "-- （本文の末尾に自動挿入されます。編集は「設定」タブから）\n" + signature;
        }
    }

    /// <summary>
    /// 設定画面で署名を編集した後、この画面に戻ってきたときに反映させるための再読み込み。
    /// ComposeViewModelはMainWindowで使い回すシングルトンのため、署名変更が自動では伝わらない。
    /// </summary>
    public void RefreshSignature()
    {
        OnPropertyChanged(nameof(SignatureNoteText));
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        var sample = ConfirmedTargets.FirstOrDefault();
        if (sample is null)
        {
            PreviewText = string.Empty;
            return;
        }

        var substituted = Substitute(Body, sample);
        var signature = GetSignatureText();
        PreviewText = string.IsNullOrEmpty(signature) ? substituted : substituted + "\n\n-- \n" + signature;
    }

    private static string Substitute(string body, Contact contact) =>
        body.Replace("#会社名#", contact.CompanyName)
            .Replace("#担当者名#", contact.ContactName)
            .Replace("#メールアドレス#", contact.Email);

    [RelayCommand]
    private void InsertTag(string? tagName)
    {
        if (string.IsNullOrEmpty(tagName))
        {
            return;
        }

        Body += $"#{tagName}#";
    }

    [RelayCommand]
    private void AddAttachment()
    {
        var dialog = new OpenFileDialog { Title = "添付ファイルを選択", Multiselect = true };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        foreach (var filePath in dialog.FileNames)
        {
            var size = new FileInfo(filePath).Length;
            Attachments.Add(new AttachmentItem(filePath, size));
        }

        AttachmentErrorText = string.Empty;
    }

    [RelayCommand]
    private void RemoveAttachment(AttachmentItem? item)
    {
        if (item is null)
        {
            return;
        }

        Attachments.Remove(item);
        AttachmentErrorText = string.Empty;
    }

    [RelayCommand]
    private void OpenPreviewModal()
    {
        var window = new TextPreviewWindow("プレビュー（1件目）", PreviewText) { Owner = Application.Current.MainWindow };
        window.ShowDialog();
    }

    [RelayCommand]
    private void CheckAndProceed()
    {
        BodyErrorText = string.Empty;
        AttachmentErrorText = string.Empty;
        TargetErrorText = string.Empty;

        var usedTags = Regex.Matches(Body, "#([^#]+)#").Select(m => m.Groups[1].Value).Distinct().ToList();
        var unknownTags = usedTags.Where(t => !KnownTagNames.Contains(t)).ToList();

        var emptyTargetCount = ConfirmedTargets.Count(
            c => string.IsNullOrWhiteSpace(c.CompanyName) || string.IsNullOrWhiteSpace(c.ContactName));
        var totalAttachmentBytes = Attachments.Sum(a => a.SizeBytes);
        var overSize = totalAttachmentBytes > AttachmentSizeLimitBytes;

        var hasError = false;

        if (unknownTags.Count > 0)
        {
            hasError = true;
            BodyErrorText = $"⚠️ 本文中の {string.Join("、", unknownTags.Select(t => "#" + t + "#"))} は、現在のリストに存在しない項目です。";
        }

        if (emptyTargetCount > 0)
        {
            hasError = true;
            TargetErrorText = $"⚠️ {emptyTargetCount}件の宛先で会社名または担当者名が空欄です。リストを確認してください。";
        }

        if (overSize)
        {
            hasError = true;
            var totalMb = totalAttachmentBytes / 1024.0 / 1024.0;
            AttachmentErrorText = $"⚠️ 添付ファイルの合計サイズが上限（13MB）を超えています（現在：{totalMb:0.0}MB）。ファイルを減らしてください。";
        }

        if (hasError)
        {
            return;
        }

        RenderConfirmation();
        CurrentStep = ComposeStep.Confirmation;
    }

    // ================= ③ 送信前の確認（要件定義書8章） =================

    [ObservableProperty]
    private string _confirmSubjectText = string.Empty;

    [ObservableProperty]
    private string _confirmBodyText = string.Empty;

    [ObservableProperty]
    private string _confirmAttachmentsText = string.Empty;

    [ObservableProperty]
    private string _confirmDuplicateWarningText = string.Empty;

    [ObservableProperty]
    private bool _hasDuplicateWarning;

    public ObservableCollection<Contact> ConfirmTargetRows { get; } = new();

    private void RenderConfirmation()
    {
        ConfirmSubjectText = Subject;

        ConfirmTargetRows.Clear();
        foreach (var contact in ConfirmedTargets)
        {
            ConfirmTargetRows.Add(contact);
        }

        var signature = GetSignatureText();
        ConfirmBodyText = string.IsNullOrEmpty(signature) ? Body : Body + "\n\n-- \n" + signature;

        ConfirmAttachmentsText = Attachments.Count == 0
            ? "添付ファイルはありません"
            : string.Join("\n", Attachments.Select(a => $"{a.FileName}（{a.SizeText}）"));

        // 要件定義書4章：重複メールアドレスは送信を止めず、確認画面で軽く警告表示のみ
        var duplicateMailCount = ConfirmedTargets
            .GroupBy(c => c.Email)
            .Where(g => g.Count() > 1)
            .Sum(g => g.Count());

        HasDuplicateWarning = duplicateMailCount > 0;
        ConfirmDuplicateWarningText = duplicateMailCount > 0
            ? $"⚠️ 重複したメールアドレスがあります。{duplicateMailCount}件のメールアドレスが宛先内で重複しています。そのまま重複して送信されます。"
            : string.Empty;
    }

    [RelayCommand]
    private void BackToCompose() => CurrentStep = ComposeStep.Compose;

    /// <summary>実際の送信処理はフェーズ5⑥（送信エンジン）で実装する。</summary>
    [RelayCommand]
    private void StartSending()
    {
        MessageBox.Show(
            "送信エンジンはフェーズ5⑥で実装予定です。",
            "メール配信ツール",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
