# DBスキーマ 初版 v1（フェーズ4・成果物②）

DDL の実体は
[`src/MailDeliveryTool.Core/Data/Schema/Schema_v1.sql`](../src/MailDeliveryTool.Core/Data/Schema/Schema_v1.sql) /
[`Seed_v1.sql`](../src/MailDeliveryTool.Core/Data/Schema/Seed_v1.sql)。
本書は設計判断の記録。

---

## 1. 構成

```
CategoryAxis (固定2件: 種別 / 技術領域)
     │ 1
     │
     │ N
CategoryValue (案件 / 人材 / 開発 / インフラ / その他 ...)
     │ 1
     │
     │ N
ContactCategoryValue  ── N ─── 1 ──  Contact
   (中間テーブル・多対多)

MailAccountSetting (単一行)      AppSetting (KVS: 署名 / バックアップ設定)
SchemaVersion (マイグレーション管理)
```

| テーブル | 役割 | 要件定義書 |
|---|---|---|
| `Contact` | 宛先（パートナーリストの1行） | 5.1 / 5.2 / 5.4 |
| `CategoryAxis` | カテゴリ軸。種別・技術領域の2件に固定 | 5.3 |
| `CategoryValue` | 軸配下の値。ユーザーが追加・削除できる | 5.3 / 10.3 |
| `ContactCategoryValue` | 宛先とカテゴリ値の多対多 | 5.3 / 5.4 |
| `MailAccountSetting` | 送信元アカウント（常に1行） | 3 / 10.1 |
| `AppSetting` | 署名・バックアップ設定のKVS | 7 / 5.5 / 10.2 |
| `SchemaVersion` | 適用済みスキーマ版数 | ― |

---

## 2. 主要な設計判断

### 2.1 会社名・担当者名に「空文字禁止」の制約を付けていない

要件定義書 5.1 では必須項目ですが、**DB制約にはしていません**。

理由は 7.1 の送信前検証に「配信対象に会社名または担当者名が空欄の宛先が
含まれる」という項目があるためです。この検証が意味を持つのは、
空欄の宛先が DB 上に存在しうる前提のときだけです（フェーズ3モックの
シードデータにも担当者名が空の「デルタ商会」が含まれています）。

必須入力は登録フォーム側で担保し、DBは既存データを受け入れられるようにします。

### 2.2 メールアドレスに UNIQUE 制約を付けていない

要件定義書 4章「重複メールアドレスはそのまま重複送信を許容。
確認画面で軽く警告表示のみ（送信はブロックしない）」に従います。
DB で弾いてしまうとこの仕様が実現できません。
重複排除はメールリストへの追加時（6.1）にアプリ側で行います。

### 2.3 カテゴリ値の削除は ON DELETE CASCADE

要件定義書 5.3「削除すると該当宛先から自動的に紐付けが外れる」を
DB 制約として表現しています。アプリ側で中間テーブルを消す処理は不要です。

一方 `CategoryValue.AxisId` は **ON DELETE RESTRICT**。軸自体を削除する機能は
提供しない（5.3）ため、誤って軸が消えて値が孤立する事故を防ぎます。

> SQLite の外部キー制約は**既定で無効**です。`SqliteConnectionFactory` が
> 接続文字列に `Foreign Keys=True` を設定しているため有効化されますが、
> 別経路で接続する場合は `PRAGMA foreign_keys = ON` を忘れないこと。

### 2.4 カテゴリ軸のIDは固定値

`CategoryAxis.Id` は AUTOINCREMENT にせず、種別=1・技術領域=2 を
Seed で固定投入します（`CategoryAxis.TypeAxisId` / `TechFieldAxisId` 定数と対応）。
軸が2件に固定される以上、コードから定数で参照できるほうが安全なためです。

表示名（`Name`）が将来変わっても壊れないよう、安定参照用の `Code`
（`Type` / `TechField`）も持たせています。

### 2.4.1 停止フラグとCSVエクスポート

`Contact.IsSuspended` はCSVエクスポートにも含める方針
（[D-006](./decisions.md#d-006-csvエクスポートに停止中の宛先も含める)）。
ただし要件定義書 5.5 のCSV列には停止フラグに対応する列がないため、
エクスポート側にのみ「状態」列を追加する案を暫定採用している。
CSV機能の実装前に確定させること。

### 2.5 送信履歴テーブルは作らない

要件定義書 9章「送信履歴・エラー履歴の永続保存（DB化）は不要」。
送信結果は画面表示とクリップボードコピーのみで完結させます。
`tools/validate_schema.py` に、履歴系テーブルが増えていないことを
確認するテストを入れてあります。

### 2.6 メールアカウント設定は単一行

`CHECK (Id = 1)` で2行目の挿入を禁止しています。
要件定義書 3章「送信元アカウントは営業担当者個人のメールアドレス1つのみ
（マルチアカウント切替なし）」を制約として表現したものです。

パスワードは `EncryptedPassword BLOB` に **DPAPI で暗号化したバイト列のみ**を
格納します（要件定義書 13章）。平文は保存しません。

### 2.7 署名やバックアップ設定は KVS

署名（7章）・バックアップ保存先・最終バックアップ日時（5.5 / 10.2）は
`AppSetting` テーブルにキー・バリューで保持します。
設定項目が増えるたびにスキーマ変更（＝マイグレーション）が必要になるのを避けるためです。

| キー | 用途 |
|---|---|
| `Signature` | 署名（7章。1つのみ登録・自動反映） |
| `BackupFolderPath` | バックアップ保存先。空なら `AppPaths.DefaultBackupDirectory` |
| `LastBackupAt` | 最終バックアップ日時。**週次自動バックアップの判定に使う**（[D-004](./decisions.md#d-004-週次バックアップは起動時に前回から7日経過していたら実行)）。手動実行時も更新すること |

---

## 3. 索引

| 索引 | 目的 |
|---|---|
| `IX_Contact_IsSuspended` | 「新しい配信」画面は常に停止中を除外するため、絞り込みの起点になる |
| `IX_Contact_Email` | メールリスト追加時・確認画面での重複検出 |
| `IX_Contact_CompanyName` | 並び替えおよび前方一致 |
| `IX_CategoryValue_AxisId` | 軸ごとの値一覧（設定画面・フィルタのチェックボックス生成） |
| `IX_ContactCategoryValue_CategoryValueId` | カテゴリ値の使用中件数取得（10.3）、軸内OR条件の絞り込み（6.1） |

> 会社名検索は**部分一致**（6.1）のため `LIKE '%...%'` となり、
> `IX_Contact_CompanyName` は効きません。個人利用で宛先件数も限られるため
> 当面は問題になりませんが、件数が増えて遅くなった場合は FTS5 の導入を検討します。

---

## 4. 絞り込みクエリの形（要件定義書 6.1）

「軸内は OR、軸間は AND」は軸ごとの `EXISTS` の AND で表現します。

```sql
SELECT c.*
FROM Contact c
WHERE c.IsSuspended = 0                        -- 停止中は一切表示しない（5.2 / 6.1）
  AND c.CompanyName LIKE '%' || $keyword || '%' -- 会社名は部分一致
  -- 軸1（種別）: 選択された値のいずれかを持つ
  AND EXISTS (SELECT 1 FROM ContactCategoryValue x
              WHERE x.ContactId = c.Id AND x.CategoryValueId IN (...))
  -- 軸2（技術領域）: 選択された値のいずれかを持つ
  AND EXISTS (SELECT 1 FROM ContactCategoryValue x
              WHERE x.ContactId = c.Id AND x.CategoryValueId IN (...))
```

チェックが1つも入っていない軸は、その軸の `EXISTS` 句ごと省略します
（＝絞り込まない）。

---

## 5. 検証

`tools/validate_schema.py` が DDL を実際の SQLite に適用し、
要件レベルの振る舞いを17項目検証します。

```bash
python3 tools/validate_schema.py
```

.NET SDK がない環境でもスキーマ単体を検証できるよう Python で書いています
（アプリ本体は `Microsoft.Data.Sqlite` から同じ DDL を実行します）。

**実行結果: 17/17 パス（SQLite 3.45.1 で確認）**

検証している内容:

- カテゴリ軸が種別／技術領域の2件に固定されていること
- 同一軸内での値名の重複を拒否し、別軸なら同名を許容すること
- カテゴリ値の削除で宛先の紐付けが自動的に外れること（CASCADE）
- 使用中の軸を削除できないこと（RESTRICT）
- メモが100文字までで、任意項目であること
- 重複メールアドレスを許容すること
- 会社名／担当者名が空欄の宛先が存在しうること
- 軸内OR・軸間AND・停止中除外の絞り込みが期待どおり動くこと
- カテゴリ値の使用中件数が取得できること（削除時の警告用）
- 送信履歴テーブルが存在しないこと
- DDL/Seed の再実行が安全であること（冪等）ほか

---

## 6. マイグレーション方針

`SchemaVersion` テーブルに適用済み版数を記録し、
`DatabaseInitializer.CurrentSchemaVersion` と比較します。

DDL を変更する際は:

1. `Schema_v2.sql` を追加（既存の `Schema_v1.sql` は**変更しない**）
2. `DatabaseInitializer.CurrentSchemaVersion` を 2 に更新
3. `DatabaseInitializer.EnsureCreated` に v1→v2 の適用処理を追加

既に配布済みのPCには v1 のDBが存在するため、v1 を書き換えると
既存データとの整合が取れなくなります。
