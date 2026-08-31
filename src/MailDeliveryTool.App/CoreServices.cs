using MailDeliveryTool.App.Services;
using MailDeliveryTool.Core.Backup;
using MailDeliveryTool.Core.Data;
using MailDeliveryTool.Core.Data.Repositories;

namespace MailDeliveryTool.App;

/// <summary>
/// アプリ全体で共有するリポジトリ・サービスをまとめたもの。
/// 本格的なDIコンテナを導入するほどの規模ではないため、起動時に一度だけ
/// 構築して各画面のViewModelへ配る簡易的な構成にしている（<see cref="App"/> 参照）。
/// </summary>
public sealed class CoreServices
{
    public ContactRepository ContactRepository { get; }
    public CategoryRepository CategoryRepository { get; }
    public MailAccountSettingRepository MailAccountSettingRepository { get; }
    public AppSettingRepository AppSettingRepository { get; }
    public CategoryStore CategoryStore { get; }
    public BackupService BackupService { get; }

    public CoreServices(SqliteConnectionFactory connectionFactory)
    {
        ContactRepository = new ContactRepository(connectionFactory);
        CategoryRepository = new CategoryRepository(connectionFactory);
        MailAccountSettingRepository = new MailAccountSettingRepository(connectionFactory);
        AppSettingRepository = new AppSettingRepository(connectionFactory);
        CategoryStore = new CategoryStore(CategoryRepository);
        BackupService = new BackupService(ContactRepository, CategoryRepository, AppSettingRepository);
    }
}
