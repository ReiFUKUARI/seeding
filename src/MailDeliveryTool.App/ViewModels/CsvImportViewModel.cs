using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MailDeliveryTool.App.Services;
using MailDeliveryTool.Core.Csv;
using MailDeliveryTool.Core.Data.Repositories;
using Microsoft.Win32;

namespace MailDeliveryTool.App.ViewModels;

/// <summary>「CSVから取り込む」モーダルのビューモデル（mock_prototype.htmlのcsvImportModal相当）。</summary>
public sealed partial class CsvImportViewModel : ObservableObject
{
    private readonly ContactRepository _contactRepository;
    private readonly CategoryStore _categoryStore;

    public CsvImportViewModel(ContactRepository contactRepository, CategoryStore categoryStore)
    {
        _contactRepository = contactRepository;
        _categoryStore = categoryStore;
    }

    private string? _selectedFilePath;

    [ObservableProperty]
    private string _selectedFileText = string.Empty;

    [ObservableProperty]
    private string _resultText = string.Empty;

    [ObservableProperty]
    private bool _isImporting;

    [ObservableProperty]
    private bool _canImport;

    [RelayCommand]
    private void DownloadTemplate()
    {
        var dialog = new SaveFileDialog
        {
            Title = "CSVテンプレートを保存",
            FileName = "宛先リスト_テンプレート.csv",
            Filter = "CSVファイル (*.csv)|*.csv",
            DefaultExt = ".csv",
        };

        if (dialog.ShowDialog() == true)
        {
            ContactCsvTemplateWriter.Write(dialog.FileName, _categoryStore.Axes);
        }
    }

    [RelayCommand]
    private void SelectFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "取り込むCSVファイルを選択",
            Filter = "CSVファイル (*.csv)|*.csv",
        };

        if (dialog.ShowDialog() == true)
        {
            _selectedFilePath = dialog.FileName;
            SelectedFileText = $"選択中のファイル：{Path.GetFileName(dialog.FileName)}";
            ResultText = string.Empty;
            CanImport = true;
        }
    }

    [RelayCommand]
    private async Task RunImportAsync()
    {
        if (_selectedFilePath is null)
        {
            return;
        }

        CanImport = false;
        IsImporting = true;
        ResultText = "取り込んでいます…";

        try
        {
            var filePath = _selectedFilePath;
            var axes = _categoryStore.Axes.ToList();
            var existingEmails = _contactRepository.GetAll().Select(c => c.Email).ToHashSet();

            var result = await Task.Run(() => ContactCsvImporter.Import(filePath, axes, existingEmails));

            foreach (var contact in result.NewContacts)
            {
                _contactRepository.Add(contact);
            }

            ResultText = result.SkippedDuplicateCount > 0
                ? $"{result.NewContacts.Count}件を新規登録しました（{result.SkippedDuplicateCount}件は既存のメールアドレスのためスキップしました）。"
                : $"{result.NewContacts.Count}件を新規登録しました。";
        }
        catch (ContactCsvImportException ex)
        {
            ResultText = $"取込を中止しました。{ex.Message}";
        }
        catch (Exception ex)
        {
            ResultText = $"取込に失敗しました: {ex.Message}";
        }
        finally
        {
            IsImporting = false;
        }
    }
}
