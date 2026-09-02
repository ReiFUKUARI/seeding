using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MailDeliveryTool.App.ViewModels;

/// <summary>
/// 「今すぐバックアップする」の実行状況モーダル（mock_prototype.htmlのbackupModal相当）。
/// 実行中→完了／失敗の3状態を切り替える。
/// </summary>
public sealed partial class BackupProgressViewModel : ObservableObject
{
    public event EventHandler? CloseRequested;

    [ObservableProperty]
    private bool _isRunning = true;

    [ObservableProperty]
    private bool _isSuccess;

    [ObservableProperty]
    private bool _isFailure;

    [ObservableProperty]
    private string _folderText = string.Empty;

    [ObservableProperty]
    private string _fileNameText = string.Empty;

    [ObservableProperty]
    private string _errorText = string.Empty;

    public void ShowSuccess(string folderPath, string fileName)
    {
        FolderText = folderPath;
        FileNameText = fileName;
        IsRunning = false;
        IsSuccess = true;
    }

    public void ShowFailure(string errorText)
    {
        ErrorText = errorText;
        IsRunning = false;
        IsFailure = true;
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
