------------------------------------------------------------------------------
-- 初期データ v1
--
-- カテゴリ軸は「種別」「技術領域」の2件に固定（要件定義書 5.3）。
-- 初期のカテゴリ値はフェーズ3モックの既定値に合わせる。
-- 値はユーザーが設定画面から追加・削除できるため、あくまで初期値。
--
-- 冪等性：INSERT OR IGNORE により、既に投入済みのDBに再実行しても
-- 重複や上書きは発生しない。
------------------------------------------------------------------------------

INSERT OR IGNORE INTO CategoryAxis (Id, Code, Name, DisplayOrder) VALUES
    (1, 'Type',      '種別',     1),
    (2, 'TechField', '技術領域', 2);

INSERT OR IGNORE INTO CategoryValue (AxisId, Name, DisplayOrder) VALUES
    (1, '案件',       1),
    (1, '人材',       2),
    (2, '開発',       1),
    (2, 'インフラ',   2),
    (2, 'その他',     3);

-- メールアカウント設定は単一行。初回起動時は空の行を用意しておき、
-- 設定画面から編集させる（未設定判定は Host の空文字で行う）。
INSERT OR IGNORE INTO MailAccountSetting
    (Id, Host, Port, UserName, EncryptedPassword, SecureSocketOption, FromAddress, FromDisplayName, UpdatedAt)
VALUES
    (1, '', 587, '', NULL, 'Auto', '', NULL, datetime('now'));

-- アプリ設定の初期値
INSERT OR IGNORE INTO AppSetting (Key, Value, UpdatedAt) VALUES
    ('Signature',        '', datetime('now')),
    ('BackupFolderPath', '', datetime('now')),
    ('LastBackupAt',     '', datetime('now'));
