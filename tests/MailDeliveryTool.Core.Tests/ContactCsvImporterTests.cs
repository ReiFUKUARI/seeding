using MailDeliveryTool.Core.Csv;
using MailDeliveryTool.Core.Data.Repositories;
using Xunit;

namespace MailDeliveryTool.Core.Tests;

public sealed class ContactCsvImporterTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly string _csvPath;
    private readonly CategoryRepository _categoryRepo;

    public ContactCsvImporterTests()
    {
        _csvPath = Path.Combine(Path.GetTempPath(), $"import-test-{Guid.NewGuid():N}.csv");
        _categoryRepo = new CategoryRepository(_db.Factory);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_csvPath))
        {
            File.Delete(_csvPath);
        }
    }

    private void WriteCsv(string content) => File.WriteAllText(_csvPath, content, new System.Text.UTF8Encoding(true));

    [Fact]
    public void Import_正常なCSVから新規宛先を取り込める()
    {
        // 値ID: 1=案件 2=人材 3=開発 4=インフラ 5=その他（Seed_v1.sqlの初期値）
        WriteCsv("会社名,担当者名,メールアドレス,種別,技術領域,メモ\r\n"
            + "株式会社サンプル,山田 太郎,sample@example.jp,案件,開発,初回商談済み\r\n");

        var result = ContactCsvImporter.Import(_csvPath, _categoryRepo.GetAxes(), new HashSet<string>());

        Assert.Single(result.NewContacts);
        var contact = result.NewContacts[0];
        Assert.Equal("株式会社サンプル", contact.CompanyName);
        Assert.Equal("山田 太郎", contact.ContactName);
        Assert.Equal("sample@example.jp", contact.Email);
        Assert.Equal("初回商談済み", contact.Memo);
        Assert.Equal(new[] { 1L, 3L }, contact.CategoryValueIds.OrderBy(x => x));
        Assert.Equal(0, result.SkippedDuplicateCount);
    }

    [Fact]
    public void Import_複数値はカンマ区切りで複数カテゴリに紐づく()
    {
        WriteCsv("会社名,担当者名,メールアドレス,種別,技術領域,メモ\r\n"
            + "株式会社サンプル,山田 太郎,sample@example.jp,\"案件,人材\",\"開発,その他\",\r\n");

        var result = ContactCsvImporter.Import(_csvPath, _categoryRepo.GetAxes(), new HashSet<string>());

        Assert.Single(result.NewContacts);
        Assert.Equal(new[] { 1L, 2L, 3L, 5L }, result.NewContacts[0].CategoryValueIds.OrderBy(x => x));
    }

    [Fact]
    public void Import_既存のカテゴリ値にない値が含まれると例外になり取込全体が中止される()
    {
        WriteCsv("会社名,担当者名,メールアドレス,種別,技術領域,メモ\r\n"
            + "正常な行,A,ok@x.jp,案件,開発,\r\n"
            + "不正な行,B,ng@x.jp,存在しない値,開発,\r\n");

        var ex = Assert.Throws<ContactCsvImportException>(
            () => ContactCsvImporter.Import(_csvPath, _categoryRepo.GetAxes(), new HashSet<string>()));

        Assert.Contains(ex.UnknownValues, v => v.Contains("存在しない値"));
    }

    [Fact]
    public void Import_既存のメールアドレスと一致する行はスキップされる()
    {
        WriteCsv("会社名,担当者名,メールアドレス,種別,技術領域,メモ\r\n"
            + "既存,A,exists@x.jp,案件,開発,\r\n"
            + "新規,B,new@x.jp,案件,開発,\r\n");

        var result = ContactCsvImporter.Import(
            _csvPath, _categoryRepo.GetAxes(), new HashSet<string> { "exists@x.jp" });

        Assert.Single(result.NewContacts);
        Assert.Equal("new@x.jp", result.NewContacts[0].Email);
        Assert.Equal(1, result.SkippedDuplicateCount);
    }

    [Fact]
    public void Import_メールアドレスが空の行は取込対象外()
    {
        WriteCsv("会社名,担当者名,メールアドレス,種別,技術領域,メモ\r\n"
            + "会社,担当者,,案件,開発,\r\n");

        var result = ContactCsvImporter.Import(_csvPath, _categoryRepo.GetAxes(), new HashSet<string>());

        Assert.Empty(result.NewContacts);
    }
}
