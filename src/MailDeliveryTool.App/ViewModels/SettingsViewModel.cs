using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MailDeliveryTool.App.Services;
using MailDeliveryTool.App.Views;
using MailDeliveryTool.Core;
using MailDeliveryTool.Core.Backup;
using MailDeliveryTool.Core.Data.Repositories;
using MailDeliveryTool.Core.Models;
using Microsoft.Win32;

namespace MailDeliveryTool.App.ViewModels;

/// <summary>
/// 設定画面のビューモデル（要件定義書10章：メールアカウント／バックアップ／カテゴリ管理）。
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly MailAccountSettingRepository _mailAccountRepository;
    private readonly AppSettingRepository _appSettingRepository;
    private readonly BackupService _backupService;
    private readonly CategoryStore _categoryStore;

    private MailAccountSetting _currentSetting = new();

    public SettingsViewModel(
        MailAccountSettingRepository mailAccountRepository,
        AppSettingRepository appSettingRepository,
        BackupService backupService,
        CategoryStore categoryStore)
    {
        _mailAccountRepository = mailAccountRepository;
        _appSettingRepository = appSettingRepository;
        _backupService = backupService;
        _categoryStore = categoryStore;

        // Reload()はClear()+Add()で行われ、ObservableCollectionが内部的に
        // "Item[]" のPropertyChangedを発火するため、TypeAxis/TechFieldAxisも
        // それに追随させてWPF側の再描画を促す。
        _categoryStore.Axes.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(TypeAxis));
            OnPropertyChanged(nameof(TechFieldAxis));
        };

        LoadMailAccount();
        LoadBackupInfo();
        LoadSignature();
    }

    /// <summary>種別・技術領域の2軸。CategoryStoreを直接公開する（D-003：単一の共有ストア）。</summary>
    public ObservableCollection<CategoryAxis> Axes => _categoryStore.Axes;

    /// <summary>「種別」軸（ID固定・要件定義書5.3）。位置ではなくIDで参照する。</summary>
    public CategoryAxis? TypeAxis => Axes.FirstOrDefault(a => a.Id == CategoryAxis.TypeAxisId);

    /// <summary>「技術領域」軸（ID固定・要件定義書5.3）。</summary>
    public CategoryAxis? TechFieldAxis => Axes.FirstOrDefault(a => a.Id == CategoryAxis.TechFieldAxisId);

    // --- メールアカウント（要件定義書10.1） ---

    [ObservableProperty]
    private string _accountServerText = "未設定";

    [ObservableProperty]
    private string _accountNameText = "未設定";

    /// <summary>パスワードそのものは表示せず、設定済みかどうかだけをマスクして示す（mock_prototype.htmlと同じ）。</summary>
    [ObservableProperty]
    private string _accountPasswordText = "未設定";

    [ObservableProperty]
    private string _accountPortText = "-";

    [ObservableProperty]
    private string _accountEncryptionText = "-";

    private void LoadMailAccount()
    {
        _currentSetting = _mailAccountRepository.Get();
        AccountServerText = string.IsNullOrWhiteSpace(_currentSetting.Host) ? "未設定" : _currentSetting.Host;
        AccountNameText = string.IsNullOrWhiteSpace(_currentSetting.UserName) ? "未設定" : _currentSetting.UserName;
        AccountPasswordText = string.IsNullOrEmpty(_currentSetting.Password) ? "未設定" : "********";
        AccountPortText = _currentSetting.Port.ToString();
        AccountEncryptionText = DescribeEncryption(_currentSetting.SecureSocketOption);
    }

    private static string DescribeEncryption(string option) => option switch
    {
        "Auto" => "自動（STARTTLSが利用可能な場合は自動使用）",
        "StartTls" => "STARTTLSを必須にする",
        "StartTlsWhenAvailable" => "STARTTLSが利用可能な場合のみ使用",
        "SslOnConnect" => "接続時にSSL/TLSを使用",
        "None" => "暗号化なし",
        _ => option,
    };

    [RelayCommand]
    private void ChangeMailAccount()
    {
        var editViewModel = MailAccountEditViewModel.FromModel(_currentSetting);
        var window = new MailAccountEditWindow(editViewModel) { Owner = Application.Current.MainWindow };
        if (window.ShowDialog() == true)
        {
            var updated = editViewModel.ToModel(_currentSetting.Password);
            _mailAccountRepository.Save(updated, editViewModel.PasswordChanged);
            LoadMailAccount();
        }
    }

    // --- 署名（要件定義書7章：1つのみ登録・自動反映） ---

    [ObservableProperty]
    private string _signatureText = string.Empty;

    [ObservableProperty]
    private string _signatureStatusText = string.Empty;

    private void LoadSignature() => SignatureText = _appSettingRepository.GetSignature();

    [RelayCommand]
    private void SaveSignature()
    {
        _appSettingRepository.SetSignature(SignatureText);
        SignatureStatusText = "保存しました。";
    }

    // --- バックアップ（要件定義書5.5・10.2） ---

    [ObservableProperty]
    private string _backupFolderText = string.Empty;

    [ObservableProperty]
    private string _lastBackupText = "未実行";

    /// <summary>既定の保存先ではなく、ユーザーが変更している場合のみ「既定に戻す」を有効にする。</summary>
    [ObservableProperty]
    private bool _isBackupFolderCustom;

    private void LoadBackupInfo()
    {
        var configured = _appSettingRepository.GetBackupFolderPath();
        BackupFolderText = string.IsNullOrWhiteSpace(configured) ? AppPaths.DefaultBackupDirectory : configured;
        IsBackupFolderCustom = !string.IsNullOrWhiteSpace(configured);

        var lastBackupAt = _appSettingRepository.GetLastBackupAt();
        LastBackupText = lastBackupAt is null ? "未実行" : lastBackupAt.Value.ToString("yyyy年MM月dd日 HH:mm");
    }

    /// <summary>
    /// バックアップの保存先フォルダーを変更する（要件定義書10.2）。
    /// 企業ポリシー等でドキュメントフォルダーへの書き込みが拒否される環境向けの回避策も兼ねる。
    /// </summary>
    [RelayCommand]
    private void ChangeBackupFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "バックアップの保存先フォルダーを選択",
            InitialDirectory = Directory.Exists(BackupFolderText) ? BackupFolderText : null,
        };

        if (dialog.ShowDialog() == true)
        {
            _appSettingRepository.SetBackupFolderPath(dialog.FolderName);
            LoadBackupInfo();
        }
    }

    /// <summary>保存先を既定（ドキュメントフォルダー配下）に戻す。</summary>
    [RelayCommand]
    private void ResetBackupFolder()
    {
        _appSettingRepository.SetBackupFolderPath(string.Empty);
        LoadBackupInfo();
    }

    /// <summary>
    /// 「今すぐバックアップする」。実行状況はモーダル（mock_prototype.htmlのbackupModal相当）で表示する。
    /// バックアップ自体はバックグラウンドスレッドで実行し、完了後にUIスレッドへ結果を反映する。
    /// </summary>
    [RelayCommand]
    private void RunBackup()
    {
        var progressViewModel = new BackupProgressViewModel();
        var window = new BackupProgressWindow(progressViewModel) { Owner = Application.Current.MainWindow };

        Task.Run(() => _backupService.Run()).ContinueWith(task =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (task.IsFaulted)
                {
                    progressViewModel.ShowFailure(DescribeBackupError(task.Exception!.GetBaseException()));
                }
                else
                {
                    progressViewModel.ShowSuccess(task.Result.FolderPath, task.Result.FileName);
                    LoadBackupInfo();
                }
            });
        });

        window.ShowDialog();
    }

    private static string DescribeBackupError(Exception ex) => ex switch
    {
        // フォルダ自体は作成できているのにファイル書き込みだけ拒否される場合、
        // 多くは Windows の「コントロールされたフォルダー アクセス」（ランサムウェア対策）が
        // 未許可アプリとしてドキュメントフォルダーへの書き込みをブロックしている。
        UnauthorizedAccessException => "保存先フォルダーへのアクセスが拒否されました。\n"
            + "Windowsセキュリティ →「ウイルスと脅威の防止」→「ランサムウェアの防止」→"
            + "「コントロールされたフォルダー アクセス」で本アプリが許可されているか確認するか、"
            + "保存先を変更してください。\n"
            + $"詳細: {ex.Message}",
        _ => ex.Message,
    };

    // --- カテゴリ管理（要件定義書5.3・10.3） ---

    [ObservableProperty]
    private string _newTypeValueName = string.Empty;

    [ObservableProperty]
    private string _newTechFieldValueName = string.Empty;

    [RelayCommand]
    private void AddTypeValue()
    {
        AddCategoryValue(CategoryAxis.TypeAxisId, NewTypeValueName);
        NewTypeValueName = string.Empty;
    }

    [RelayCommand]
    private void AddTechFieldValue()
    {
        AddCategoryValue(CategoryAxis.TechFieldAxisId, NewTechFieldValueName);
        NewTechFieldValueName = string.Empty;
    }

    private void AddCategoryValue(long axisId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            _categoryStore.AddValue(axisId, name);
        }
        catch (DuplicateCategoryValueException ex)
        {
            MessageBox.Show(ex.Message, "メール配信ツール", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>カテゴリ値を削除する。使用件数を示したうえで確認を取る（要件定義書10.3）。</summary>
    [RelayCommand]
    private void DeleteCategoryValue(CategoryValue? value)
    {
        if (value is null)
        {
            return;
        }

        var usageCount = _categoryStore.GetUsageCount(value.Id);
        var message = usageCount > 0
            ? $"「{value.Name}」は {usageCount} 件の宛先で使用されています。\n削除すると、該当する宛先からこの値の紐付けが自動的に外れます。削除しますか？"
            : $"「{value.Name}」を削除しますか？";

        var result = MessageBox.Show(message, "カテゴリ値の削除", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            _categoryStore.DeleteValue(value.Id);
        }
    }
}
