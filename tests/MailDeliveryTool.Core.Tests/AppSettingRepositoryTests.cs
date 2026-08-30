using MailDeliveryTool.Core.Data.Repositories;
using Xunit;

namespace MailDeliveryTool.Core.Tests;

public sealed class AppSettingRepositoryTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly AppSettingRepository _repo;

    public AppSettingRepositoryTests() => _repo = new AppSettingRepository(_db.Factory);

    public void Dispose() => _db.Dispose();

    [Fact]
    public void GetSignature_初期値は空文字列()
    {
        Assert.Equal(string.Empty, _repo.GetSignature());
    }

    [Fact]
    public void SetSignature_保存した値が取得できる()
    {
        _repo.SetSignature("署名テキスト");
        Assert.Equal("署名テキスト", _repo.GetSignature());
    }

    [Fact]
    public void SetSignature_2回目は上書きされる()
    {
        _repo.SetSignature("署名A");
        _repo.SetSignature("署名B");
        Assert.Equal("署名B", _repo.GetSignature());
    }

    [Fact]
    public void GetLastBackupAt_未実行ならnull()
    {
        Assert.Null(_repo.GetLastBackupAt());
    }

    [Fact]
    public void SetLastBackupAt_保存した日時が取得できる()
    {
        var now = DateTimeOffset.Now;
        _repo.SetLastBackupAt(now);

        var loaded = _repo.GetLastBackupAt();
        Assert.NotNull(loaded);
        Assert.Equal(now.ToUnixTimeMilliseconds(), loaded!.Value.ToUnixTimeMilliseconds());
    }

    [Fact]
    public void Get_未知のキーはnull()
    {
        Assert.Null(_repo.Get("存在しないキー"));
    }

    [Fact]
    public void GetBackupFolderPath_初期値は空文字列()
    {
        Assert.Equal(string.Empty, _repo.GetBackupFolderPath());
    }

    [Fact]
    public void SetBackupFolderPath_保存した値が取得できる()
    {
        _repo.SetBackupFolderPath(@"D:\Backup");
        Assert.Equal(@"D:\Backup", _repo.GetBackupFolderPath());
    }
}
