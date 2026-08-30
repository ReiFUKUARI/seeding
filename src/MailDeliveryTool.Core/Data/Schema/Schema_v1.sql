------------------------------------------------------------------------------
-- メール配信ツール SQLite スキーマ v1
--
-- 対応する要件定義書の章：
--   5.4 データモデル（宛先 / カテゴリ軸 / カテゴリ値 / 中間テーブル）
--   5.2 宛先の停止／再開（Contact.IsSuspended）
--   10.1 メールアカウント設定
--   13   認証情報は DPAPI で暗号化して保存
--
-- 意図的に「作らない」テーブル：
--   送信履歴・エラー履歴（要件定義書 9章「永続保存（DB化）は不要」）
--
-- 実行前提：
--   接続文字列で Foreign Keys=True を指定するか、接続ごとに
--   PRAGMA foreign_keys = ON を発行すること（SQLite の既定は OFF）。
------------------------------------------------------------------------------

------------------------------------------------------------------------------
-- スキーマバージョン管理
--   将来のマイグレーションのため、適用済みバージョンを記録する。
------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS SchemaVersion (
    Version   INTEGER NOT NULL PRIMARY KEY,
    AppliedAt TEXT    NOT NULL
);

------------------------------------------------------------------------------
-- 宛先（パートナーリスト）
--
-- CompanyName / ContactName に「空文字を禁止する CHECK 制約は付けない」。
-- 理由：要件定義書 7.1 の送信前検証②で「会社名または担当者名が空欄の宛先が
-- 配信対象に含まれる」ことを検知する仕様になっており、空欄の行が DB 上に
-- 存在しうることが前提となっているため。必須入力は登録フォーム側で担保する。
--
-- Email に UNIQUE 制約は付けない。
-- 理由：要件定義書 4章「重複メールアドレスはそのまま重複送信を許容」。
------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Contact (
    Id          INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    CompanyName TEXT    NOT NULL DEFAULT '',
    ContactName TEXT    NOT NULL DEFAULT '',
    Email       TEXT    NOT NULL DEFAULT '',
    -- メモは任意項目。上限100文字（要件定義書 5.1）
    Memo        TEXT    NULL,
    -- 停止フラグ。1 = 停止中（配信対象から除外・要件定義書 5.2）
    IsSuspended INTEGER NOT NULL DEFAULT 0,
    CreatedAt   TEXT    NOT NULL,
    UpdatedAt   TEXT    NOT NULL,

    CONSTRAINT CK_Contact_Memo_Length  CHECK (Memo IS NULL OR length(Memo) <= 100),
    CONSTRAINT CK_Contact_IsSuspended  CHECK (IsSuspended IN (0, 1))
);

-- 「新しい配信」画面は常に停止中を除外するため、絞り込みの起点になる
CREATE INDEX IF NOT EXISTS IX_Contact_IsSuspended ON Contact (IsSuspended);

-- メールリストへの追加時・確認画面での重複検出に使う
CREATE INDEX IF NOT EXISTS IX_Contact_Email ON Contact (Email);

-- 会社名検索は部分一致（LIKE '%...%'）のため、この索引は前方一致・
-- 並び替え用途にとどまる点に注意（要件定義書 6.1）。
CREATE INDEX IF NOT EXISTS IX_Contact_CompanyName ON Contact (CompanyName);

------------------------------------------------------------------------------
-- カテゴリ軸
--   「種別」「技術領域」の2件に固定。ユーザーによる追加・改名は提供しない
--   （要件定義書 5.3）。そのため Id はアプリ側から定数で参照できるよう
--   AUTOINCREMENT にせず固定値を Seed で投入する。
--   Code は表示名（Name）が将来変わってもコードから安定参照するためのキー。
------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS CategoryAxis (
    Id           INTEGER NOT NULL PRIMARY KEY,
    Code         TEXT    NOT NULL UNIQUE,
    Name         TEXT    NOT NULL,
    DisplayOrder INTEGER NOT NULL DEFAULT 0
);

------------------------------------------------------------------------------
-- カテゴリ値
--   各軸配下の値（案件／人材、開発／インフラ／その他 等）。
--   設定画面から自由に追加・削除できる（要件定義書 5.3 / 10.3）。
--
--   AxisId は ON DELETE RESTRICT。軸自体を削除する機能は提供しないため、
--   誤って軸が消えて値が孤立することを防ぐ。
------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS CategoryValue (
    Id           INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    AxisId       INTEGER NOT NULL,
    Name         TEXT    NOT NULL,
    DisplayOrder INTEGER NOT NULL DEFAULT 0,

    CONSTRAINT FK_CategoryValue_Axis
        FOREIGN KEY (AxisId) REFERENCES CategoryAxis (Id) ON DELETE RESTRICT,
    -- 同一軸内での値名の重複を禁止（軸をまたげば同名は許容）
    CONSTRAINT UQ_CategoryValue_Axis_Name UNIQUE (AxisId, Name),
    CONSTRAINT CK_CategoryValue_Name CHECK (length(trim(Name)) > 0)
);

CREATE INDEX IF NOT EXISTS IX_CategoryValue_AxisId ON CategoryValue (AxisId);

------------------------------------------------------------------------------
-- 宛先 × カテゴリ値（中間テーブル・多対多）
--   1つの宛先が1軸内で複数の値を持てる（要件定義書 5.3 マルチセレクト）。
--
--   CategoryValueId は ON DELETE CASCADE。
--   要件定義書 5.3「カテゴリ値を削除すると該当宛先から自動的に紐付けが外れる」
--   を DB 制約として表現している。
------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ContactCategoryValue (
    ContactId       INTEGER NOT NULL,
    CategoryValueId INTEGER NOT NULL,

    CONSTRAINT PK_ContactCategoryValue PRIMARY KEY (ContactId, CategoryValueId),
    CONSTRAINT FK_ContactCategoryValue_Contact
        FOREIGN KEY (ContactId) REFERENCES Contact (Id) ON DELETE CASCADE,
    CONSTRAINT FK_ContactCategoryValue_CategoryValue
        FOREIGN KEY (CategoryValueId) REFERENCES CategoryValue (Id) ON DELETE CASCADE
);

-- 「このカテゴリ値を使っている宛先は何件か」（削除時の警告・要件定義書 10.3）と
-- 軸内 OR 条件の絞り込み（要件定義書 6.1）の双方でこの向きの索引を使う
CREATE INDEX IF NOT EXISTS IX_ContactCategoryValue_CategoryValueId
    ON ContactCategoryValue (CategoryValueId);

------------------------------------------------------------------------------
-- メールアカウント設定
--   送信元アカウントは1つのみ（要件定義書 3章）。
--   Id = 1 の単一行に固定する。
--
--   EncryptedPassword は DPAPI（DataProtectionScope.CurrentUser）で
--   暗号化したバイト列を格納する。平文は決して保存しない（要件定義書 13章）。
------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS MailAccountSetting (
    Id                 INTEGER NOT NULL PRIMARY KEY,
    Host               TEXT    NOT NULL DEFAULT '',
    Port               INTEGER NOT NULL DEFAULT 587,
    UserName           TEXT    NOT NULL DEFAULT '',
    EncryptedPassword  BLOB    NULL,
    -- MailKit の SecureSocketOptions 名を文字列で保持する。既定は Auto
    -- （要件定義書 3章：STARTTLS 必須化はせず自動判定）
    SecureSocketOption TEXT    NOT NULL DEFAULT 'Auto',
    FromAddress        TEXT    NOT NULL DEFAULT '',
    FromDisplayName    TEXT    NULL,
    UpdatedAt          TEXT    NOT NULL,

    CONSTRAINT CK_MailAccountSetting_SingleRow CHECK (Id = 1),
    CONSTRAINT CK_MailAccountSetting_Port      CHECK (Port BETWEEN 1 AND 65535),
    CONSTRAINT CK_MailAccountSetting_Option
        CHECK (SecureSocketOption IN ('None', 'Auto', 'SslOnConnect', 'StartTls', 'StartTlsWhenAvailable'))
);

------------------------------------------------------------------------------
-- アプリ設定（キー・バリュー）
--   署名（要件定義書 7章：1つのみ登録・自動反映）、
--   バックアップ保存先・最終実行日時（要件定義書 5.5 / 10.2）などを保持する。
--   項目追加のたびに列を増やさずに済むよう KVS 形式とする。
------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS AppSetting (
    Key       TEXT NOT NULL PRIMARY KEY,
    Value     TEXT NULL,
    UpdatedAt TEXT NOT NULL
);
