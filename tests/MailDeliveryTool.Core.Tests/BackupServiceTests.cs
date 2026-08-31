using MailDeliveryTool.Core.Backup;
using MailDeliveryTool.Core.Data.Repositories;
using MailDeliveryTool.Core.Models;
using Xunit;

namespace MailDeliveryTool.Core.Tests;

public sealed class BackupServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly string _backupDir;
    private readonly BackupService _service;
    private readonly AppSettingRepository _appSettingRepository;

    public BackupServiceTests()
    {
        _backupDir = Path.Combine(Path.GetTempPath(), $"backup-test-{Guid.NewGuid():N}");

        var contactRepo = new ContactRepository(_db.Factory);
        var categoryRepo = new CategoryRepository(_db.Factory);
        _appSettingRepository = new AppSettingRepository(_db.Factory);

        // 実際の既定バックアップ先（ドキュメントフォルダ）には書き込まず、
        // テスト専用の一時フォルダを明示的に設定する
        _appSettingRepository.SetBackupFolderPath(_backupDir);

        _service = new BackupService(contactRepo, categoryRepo, _appSettingRepository);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_backupDir))
        {
            Directory.Delete(_backupDir, recursive: true);
        }
    }

    [Fact]
    public void Run_CSVファイルが生成されLastBackupAtが更新される()
    {
        Assert.Null(_appSettingRepository.GetLastBackupAt());

        var result = _service.Run();

        Assert.True(File.Exists(result.FullPath));
        Assert.NotNull(_appSettingRepository.GetLastBackupAt());
    }

    [Fact]
    public void Run_フォルダが存在しなければ作成する()
    {
        Assert.False(Directory.Exists(_backupDir));

        _service.Run();

        Assert.True(Directory.Exists(_backupDir));
    }

    [Fact]
    public void IsWeeklyBackupDue_未実行ならtrue()
    {
        Assert.True(_service.IsWeeklyBackupDue());
    }

    [Fact]
    public void IsWeeklyBackupDue_直後はfalse()
    {
        _service.Run();
        Assert.False(_service.IsWeeklyBackupDue());
    }

    [Fact]
    public void IsWeeklyBackupDue_7日以上前ならtrue()
    {
        _appSettingRepository.SetLastBackupAt(DateTimeOffset.Now.AddDays(-8));
        Assert.True(_service.IsWeeklyBackupDue());
    }

    [Fact]
    public void IsWeeklyBackupDue_6日前ならfalse()
    {
        _appSettingRepository.SetLastBackupAt(DateTimeOffset.Now.AddDays(-6));
        Assert.False(_service.IsWeeklyBackupDue());
    }
}
