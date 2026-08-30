using MailDeliveryTool.Core.Data.Repositories;
using MailDeliveryTool.Core.Models;
using Xunit;

namespace MailDeliveryTool.Core.Tests;

/// <summary>
/// DPAPI（Windows Data Protection API）を使うため、パスワードを実際に
/// 暗号化・復号するテストは Windows 上でしか意味のある検証にならない。
/// 非Windows環境（Linux Dev Container等）では
/// <c>System.Security.Cryptography.ProtectedData</c> が
/// PlatformNotSupportedException を投げるため、該当テストは
/// <see cref="OperatingSystem.IsWindows"/> で早期リターンし、
/// 非Windowsでの実行を「失敗」ではなく「検証スキップ」として扱う。
/// </summary>
public sealed class MailAccountSettingRepositoryTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly MailAccountSettingRepository _repo;

    public MailAccountSettingRepositoryTests() => _repo = new MailAccountSettingRepository(_db.Factory);

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Get_初期状態は未設定()
    {
        // Password は暗号文がNULLの場合そもそもDPAPIを呼ばないため、全OSで検証できる
        var setting = _repo.Get();

        Assert.False(setting.IsConfigured);
        Assert.Equal(587, setting.Port);
        Assert.Equal("Auto", setting.SecureSocketOption);
        Assert.Null(setting.Password);
    }

    [Fact]
    public void Save_パスワード変更ありなら暗号化されて保存され復号できる()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var setting = new MailAccountSetting
        {
            Host = "mail.example.jp",
            Port = 587,
            UserName = "user@example.jp",
            Password = "P@ssw0rd!",
            SecureSocketOption = "Auto",
            FromAddress = "user@example.jp",
        };
        _repo.Save(setting, passwordChanged: true);

        var loaded = _repo.Get();
        Assert.Equal("mail.example.jp", loaded.Host);
        Assert.Equal("P@ssw0rd!", loaded.Password);
        Assert.True(loaded.IsConfigured);
    }

    [Fact]
    public void Save_パスワード変更なしなら既存の暗号化パスワードを維持する()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var setting = new MailAccountSetting
        {
            Host = "mail.example.jp",
            Port = 587,
            UserName = "user@example.jp",
            Password = "P@ssw0rd!",
            SecureSocketOption = "Auto",
            FromAddress = "user@example.jp",
        };
        _repo.Save(setting, passwordChanged: true);

        var updated = _repo.Get();
        updated.Host = "mail2.example.jp";
        _repo.Save(updated, passwordChanged: false);

        var loaded = _repo.Get();
        Assert.Equal("mail2.example.jp", loaded.Host);
        Assert.Equal("P@ssw0rd!", loaded.Password); // 変更していないので維持されている
    }
}
