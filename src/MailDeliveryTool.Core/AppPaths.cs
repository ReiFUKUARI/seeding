namespace MailDeliveryTool.Core;

/// <summary>
/// アプリが使用するファイル・フォルダのパスを一元管理する。
/// </summary>
/// <remarks>
/// MSIX でパッケージ化した場合、LocalApplicationData への書き込みは
/// パッケージごとの LocalCache 配下にリダイレクトされることがある。
/// いずれの場合もユーザー単位で分離されるため要件（13章：個人完結）は満たすが、
/// アンインストールでDBごと消える点に注意（docs/msix-packaging.md 参照）。
/// バックアップ先だけは意図的にリダイレクト対象外のドキュメントフォルダを使う。
/// </remarks>
public static class AppPaths
{
    /// <summary>アプリ名。フォルダ名にも使用する。</summary>
    public const string AppName = "メール配信ツール";

    /// <summary>DBやログを置くユーザー固有のデータフォルダ。</summary>
    public static string DataDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppName);

    /// <summary>SQLite データベースファイルのフルパス。</summary>
    public static string DatabasePath => Path.Combine(DataDirectory, "maildelivery.db");

    /// <summary>
    /// 既定のバックアップ保存先（要件定義書 5.5 / 13章）。
    /// ユーザーが設定画面で任意のフォルダに変更できる。
    /// </summary>
    public static string DefaultBackupDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            AppName,
            "Backup");

    /// <summary>必要なフォルダを作成する（既に存在する場合は何もしない）。</summary>
    public static void EnsureDataDirectory() => Directory.CreateDirectory(DataDirectory);
}
