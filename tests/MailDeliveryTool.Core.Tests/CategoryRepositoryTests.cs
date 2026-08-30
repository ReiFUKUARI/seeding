using MailDeliveryTool.Core.Data.Repositories;
using MailDeliveryTool.Core.Models;
using Xunit;

namespace MailDeliveryTool.Core.Tests;

public sealed class CategoryRepositoryTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly CategoryRepository _repo;

    public CategoryRepositoryTests() => _repo = new CategoryRepository(_db.Factory);

    public void Dispose() => _db.Dispose();

    [Fact]
    public void GetAxes_初期データの2軸5値が取得できる()
    {
        var axes = _repo.GetAxes();

        Assert.Equal(2, axes.Count);
        var type = axes.Single(a => a.Code == "Type");
        var tech = axes.Single(a => a.Code == "TechField");
        Assert.Equal(new[] { "案件", "人材" }, type.Values.Select(v => v.Name));
        Assert.Equal(new[] { "開発", "インフラ", "その他" }, tech.Values.Select(v => v.Name));
    }

    [Fact]
    public void AddValue_末尾に自動採番される()
    {
        var id = _repo.AddValue(CategoryAxis.TypeAxisId, "新種別");

        var type = _repo.GetAxes().Single(a => a.Id == CategoryAxis.TypeAxisId);
        var added = type.Values.Single(v => v.Id == id);
        Assert.Equal("新種別", added.Name);
        Assert.Equal(3, added.DisplayOrder);
    }

    [Fact]
    public void AddValue_同一軸内の重複は例外になる()
    {
        Assert.Throws<DuplicateCategoryValueException>(
            () => _repo.AddValue(CategoryAxis.TypeAxisId, "案件"));
    }

    [Fact]
    public void AddValue_別軸なら同名でも登録できる()
    {
        var id = _repo.AddValue(CategoryAxis.TechFieldAxisId, "案件");
        Assert.True(id > 0);
    }

    [Fact]
    public void AddValue_前後の空白はトリムされる()
    {
        var id = _repo.AddValue(CategoryAxis.TypeAxisId, "  新種別  ");

        var added = _repo.GetAxes().SelectMany(a => a.Values).Single(v => v.Id == id);
        Assert.Equal("新種別", added.Name);
    }

    [Fact]
    public void AddValue_空文字はArgumentExceptionになる()
    {
        Assert.Throws<ArgumentException>(() => _repo.AddValue(CategoryAxis.TypeAxisId, "   "));
    }

    [Fact]
    public void DeleteValue_使用中の宛先から紐付けが外れる()
    {
        var contactRepo = new ContactRepository(_db.Factory);
        var contact = new Contact
        {
            CompanyName = "A", ContactName = "B", Email = "a@x.jp", CategoryValueIds = { 1, 3 },
        };
        contactRepo.Add(contact);

        _repo.DeleteValue(1);

        var loaded = contactRepo.GetById(contact.Id);
        Assert.Equal(new[] { 3L }, loaded!.CategoryValueIds);
    }

    [Fact]
    public void DeleteValue_宛先自体は削除されない()
    {
        var contactRepo = new ContactRepository(_db.Factory);
        var contact = new Contact
        {
            CompanyName = "A", ContactName = "B", Email = "a@x.jp", CategoryValueIds = { 1 },
        };
        contactRepo.Add(contact);

        _repo.DeleteValue(1);

        Assert.NotNull(contactRepo.GetById(contact.Id));
    }

    [Fact]
    public void GetUsageCount_使用件数を取得できる()
    {
        var contactRepo = new ContactRepository(_db.Factory);
        contactRepo.Add(new Contact { CompanyName = "A", ContactName = "a", Email = "a@x.jp", CategoryValueIds = { 1 } });
        contactRepo.Add(new Contact { CompanyName = "B", ContactName = "b", Email = "b@x.jp", CategoryValueIds = { 1 } });
        contactRepo.Add(new Contact { CompanyName = "C", ContactName = "c", Email = "c@x.jp", CategoryValueIds = { 2 } });

        Assert.Equal(2, _repo.GetUsageCount(1));
        Assert.Equal(0, _repo.GetUsageCount(999));
    }

    [Fact]
    public void DeleteValue_存在しないIdはInvalidOperationExceptionになる()
    {
        Assert.Throws<InvalidOperationException>(() => _repo.DeleteValue(9999));
    }
}
