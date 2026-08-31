using MailDeliveryTool.Core.Data.Repositories;

namespace MailDeliveryTool.Core.Backup;

/// <summary>
/// CSVバックアップの実行を担う（要件定義書5.5・10.2）。
/// 保存先は <see cref="AppSettingRepository.GetBackupFolderPath"/> の設定、
/// 未設定なら <see cref="MailDeliveryTool.Core.AppPaths.DefaultBackupDirectory"/> を使う。
/// </summary>
public sealed class BackupService
{
    private readonly ContactRepository _contactRepository;
    private readonly CategoryRepository _categoryRepository;
    private readonly AppSettingRepository _appSettingRepository;

    public BackupService(
        ContactRepository contactRepository,
        CategoryRepository categoryRepository,
        AppSettingRepository appSettingRepository)
    {
        _contactRepository = contactRepository;
        _categoryRepository = categoryRepository;
        _appSettingRepository = appSettingRepository;
    }

    /// <summary>バックアップを実行し、保存先ディレクトリとファイル名を返す。</summary>
    public BackupResult Run()
    {
        var folder = ResolveBackupDirectory();
        Directory.CreateDirectory(folder);

        var fileName = $"contacts_{DateTimeOffset.Now:yyyyMMdd_HHmmss}.csv";
        var filePath = Path.Combine(folder, fileName);

        var contacts = _contactRepository.GetAll();
        var axes = _categoryRepository.GetAxes();
        ContactCsvExporter.Export(filePath, contacts, axes);

        _appSettingRepository.SetLastBackupAt(DateTimeOffset.Now);

        return new BackupResult(folder, fileName);
    }

    /// <summary>
    /// D-004: 起動時に前回のバックアップから7日以上経過していれば自動実行が必要と判定する。
    /// 一度も実行していない場合も対象とする。
    /// </summary>
    public bool IsWeeklyBackupDue()
    {
        var lastBackupAt = _appSettingRepository.GetLastBackupAt();
        return lastBackupAt is null || DateTimeOffset.Now - lastBackupAt.Value >= TimeSpan.FromDays(7);
    }

    private string ResolveBackupDirectory()
    {
        var configured = _appSettingRepository.GetBackupFolderPath();
        return string.IsNullOrWhiteSpace(configured) ? AppPaths.DefaultBackupDirectory : configured;
    }
}

/// <summary>バックアップ実行結果。</summary>
public sealed record BackupResult(string FolderPath, string FileName)
{
    public string FullPath => Path.Combine(FolderPath, FileName);
}
