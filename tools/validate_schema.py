#!/usr/bin/env python3
"""
SQLite スキーマ検証スクリプト（フェーズ4・成果物②の検証用）

Schema_v1.sql / Seed_v1.sql を実際の SQLite エンジンに適用し、
要件定義書に書かれた振る舞いが DB 制約として成立しているかを確認する。

.NET SDK がない環境でもスキーマ単体を検証できるようにするのが目的。
アプリ本体は Microsoft.Data.Sqlite から同じ DDL を実行する。

使い方:
    python3 tools/validate_schema.py
終了コード 0 = 全件パス
"""
import os
import sqlite3
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCHEMA_DIR = os.path.join(ROOT, "src", "MailDeliveryTool.Core", "Data", "Schema")

results = []


def check(name, fn):
    try:
        fn()
        results.append((True, name, ""))
    except AssertionError as e:
        results.append((False, name, str(e) or "assertion failed"))
    except Exception as e:  # noqa: BLE001
        results.append((False, name, f"{type(e).__name__}: {e}"))


def new_db():
    con = sqlite3.connect(":memory:")
    con.execute("PRAGMA foreign_keys = ON")
    for f in ("Schema_v1.sql", "Seed_v1.sql"):
        with open(os.path.join(SCHEMA_DIR, f), encoding="utf-8") as fh:
            con.executescript(fh.read())
    con.execute("PRAGMA foreign_keys = ON")  # executescript は暗黙コミットするため再設定
    return con


def add_contact(con, company="A社", name="担当", mail="a@example.jp", memo=None, suspended=0):
    cur = con.execute(
        "INSERT INTO Contact (CompanyName, ContactName, Email, Memo, IsSuspended, CreatedAt, UpdatedAt)"
        " VALUES (?,?,?,?,?, datetime('now'), datetime('now'))",
        (company, name, mail, memo, suspended),
    )
    return cur.lastrowid


# --- 5.3 カテゴリ軸は種別/技術領域の2件に固定 -------------------------------
def t_axes_fixed():
    con = new_db()
    rows = con.execute("SELECT Code, Name FROM CategoryAxis ORDER BY DisplayOrder").fetchall()
    assert rows == [("Type", "種別"), ("TechField", "技術領域")], rows


# --- 5.3 同一軸内の値名は重複不可 / 軸をまたげば同名可 -----------------------
def t_value_unique_per_axis():
    con = new_db()
    try:
        con.execute("INSERT INTO CategoryValue (AxisId, Name) VALUES (1, '案件')")
        raise AssertionError("同一軸内の同名値が登録できてしまった")
    except sqlite3.IntegrityError:
        pass
    # 別軸なら同名を許容する
    con.execute("INSERT INTO CategoryValue (AxisId, Name) VALUES (2, '案件')")


# --- 5.3 カテゴリ値削除で宛先の紐付けが自動的に外れる（ON DELETE CASCADE） --
def t_value_delete_cascades():
    con = new_db()
    cid = add_contact(con)
    con.execute("INSERT INTO ContactCategoryValue VALUES (?, 1)", (cid,))
    con.execute("INSERT INTO ContactCategoryValue VALUES (?, 3)", (cid,))
    con.execute("DELETE FROM CategoryValue WHERE Id = 1")
    left = con.execute(
        "SELECT CategoryValueId FROM ContactCategoryValue WHERE ContactId = ?", (cid,)
    ).fetchall()
    assert left == [(3,)], f"紐付けが外れていない: {left}"
    # 宛先自体は残る（停止・削除とは別概念）
    assert con.execute("SELECT COUNT(*) FROM Contact").fetchone()[0] == 1


# --- 5.3 軸自体は削除できない（ON DELETE RESTRICT） -------------------------
def t_axis_delete_restricted():
    con = new_db()
    try:
        con.execute("DELETE FROM CategoryAxis WHERE Id = 1")
        raise AssertionError("値が残っている軸を削除できてしまった")
    except sqlite3.IntegrityError:
        pass


# --- 宛先削除で中間テーブルも消える（ON DELETE CASCADE） -------------------
def t_contact_delete_cascades():
    con = new_db()
    cid = add_contact(con)
    con.execute("INSERT INTO ContactCategoryValue VALUES (?, 1)", (cid,))
    con.execute("DELETE FROM Contact WHERE Id = ?", (cid,))
    assert con.execute("SELECT COUNT(*) FROM ContactCategoryValue").fetchone()[0] == 0


# --- 5.1 メモは100文字まで --------------------------------------------------
def t_memo_length_limit():
    con = new_db()
    add_contact(con, memo="あ" * 100)          # 境界値: ちょうど100文字は通る
    add_contact(con, memo=None)                # 任意項目なので NULL 可
    try:
        add_contact(con, memo="あ" * 101)
        raise AssertionError("101文字のメモが登録できてしまった")
    except sqlite3.IntegrityError:
        pass


# --- 4章 重複メールアドレスを DB として許容する -----------------------------
def t_duplicate_email_allowed():
    con = new_db()
    add_contact(con, company="A社", mail="dup@example.jp")
    add_contact(con, company="B社", mail="dup@example.jp")
    assert con.execute("SELECT COUNT(*) FROM Contact WHERE Email='dup@example.jp'").fetchone()[0] == 2


# --- 7.1 会社名・担当者名が空欄の宛先が DB 上に存在しうる -------------------
def t_empty_name_allowed():
    con = new_db()
    add_contact(con, company="デルタ商会", name="", mail="info@delta.jp")
    assert con.execute("SELECT COUNT(*) FROM Contact WHERE ContactName=''").fetchone()[0] == 1


# --- 5.2 停止フラグは 0/1 のみ ---------------------------------------------
def t_suspended_flag_domain():
    con = new_db()
    try:
        add_contact(con, suspended=2)
        raise AssertionError("IsSuspended に 2 が入ってしまった")
    except sqlite3.IntegrityError:
        pass


# --- 3章/10.1 メールアカウント設定は単一行 ----------------------------------
def t_mail_account_single_row():
    con = new_db()
    try:
        con.execute(
            "INSERT INTO MailAccountSetting (Id, Host, UpdatedAt) VALUES (2, 'x', datetime('now'))"
        )
        raise AssertionError("2行目のメールアカウント設定が登録できてしまった")
    except sqlite3.IntegrityError:
        pass


def t_mail_account_option_domain():
    con = new_db()
    for ok in ("None", "Auto", "SslOnConnect", "StartTls", "StartTlsWhenAvailable"):
        con.execute("UPDATE MailAccountSetting SET SecureSocketOption = ? WHERE Id = 1", (ok,))
    try:
        con.execute("UPDATE MailAccountSetting SET SecureSocketOption = 'Bogus' WHERE Id = 1")
        raise AssertionError("未定義の暗号化方式が保存できてしまった")
    except sqlite3.IntegrityError:
        pass


# --- 6.1 軸内 OR・軸間 AND の絞り込みが1クエリで表現できる ------------------
def t_or_within_axis_and_between_axes():
    """
    種別=案件 OR 人材、かつ 技術領域=開発 で絞り込む。
    停止中の宛先は結果に一切含まれないこと（5.2 / 6.1）。
    """
    con = new_db()
    # 値ID: 1=案件 2=人材 3=開発 4=インフラ 5=その他
    def mk(company, vals, suspended=0):
        cid = add_contact(con, company=company, mail=f"{company}@example.jp", suspended=suspended)
        for v in vals:
            con.execute("INSERT INTO ContactCategoryValue VALUES (?,?)", (cid, v))
        return cid

    mk("案件×開発", [1, 3])            # ヒットする
    mk("人材×開発", [2, 3])            # ヒットする（軸内 OR）
    mk("案件×インフラ", [1, 4])        # 技術領域が一致しないので除外（軸間 AND）
    mk("停止中案件×開発", [1, 3], suspended=1)  # 停止中なので除外

    sql = """
        SELECT c.CompanyName
        FROM Contact c
        WHERE c.IsSuspended = 0
          AND EXISTS (SELECT 1 FROM ContactCategoryValue x
                      WHERE x.ContactId = c.Id AND x.CategoryValueId IN (1, 2))
          AND EXISTS (SELECT 1 FROM ContactCategoryValue x
                      WHERE x.ContactId = c.Id AND x.CategoryValueId IN (3))
        ORDER BY c.CompanyName
    """
    got = sorted(r[0] for r in con.execute(sql))
    assert got == ["人材×開発", "案件×開発"], got


# --- 10.3 カテゴリ値削除前の「使用中の宛先件数」が取得できる ----------------
def t_usage_count_query():
    con = new_db()
    for i in range(3):
        cid = add_contact(con, company=f"C{i}", mail=f"c{i}@example.jp")
        con.execute("INSERT INTO ContactCategoryValue VALUES (?, 1)", (cid,))
    n = con.execute(
        "SELECT COUNT(DISTINCT ContactId) FROM ContactCategoryValue WHERE CategoryValueId = 1"
    ).fetchone()[0]
    assert n == 3, n


# --- 中間テーブルの複合主キーで同じ紐付けが二重登録されない ----------------
def t_link_pk_prevents_duplicates():
    con = new_db()
    cid = add_contact(con)
    con.execute("INSERT INTO ContactCategoryValue VALUES (?, 1)", (cid,))
    try:
        con.execute("INSERT INTO ContactCategoryValue VALUES (?, 1)", (cid,))
        raise AssertionError("同じ紐付けが二重登録できてしまった")
    except sqlite3.IntegrityError:
        pass


# --- 存在しないカテゴリ値への紐付けは外部キーで弾かれる --------------------
def t_link_fk_enforced():
    con = new_db()
    cid = add_contact(con)
    try:
        con.execute("INSERT INTO ContactCategoryValue VALUES (?, 9999)", (cid,))
        raise AssertionError("存在しないカテゴリ値に紐付けできてしまった")
    except sqlite3.IntegrityError:
        pass


# --- 9章 送信履歴テーブルは作らない ----------------------------------------
def t_no_send_history_table():
    con = new_db()
    names = {r[0] for r in con.execute("SELECT name FROM sqlite_master WHERE type='table'")}
    unexpected = {n for n in names if "History" in n or "Log" in n}
    assert not unexpected, f"不要な履歴テーブルがある: {unexpected}"


# --- DDL / Seed の再実行が安全であること ------------------------------------
def t_scripts_are_idempotent():
    con = new_db()
    for f in ("Schema_v1.sql", "Seed_v1.sql"):
        with open(os.path.join(SCHEMA_DIR, f), encoding="utf-8") as fh:
            con.executescript(fh.read())
    assert con.execute("SELECT COUNT(*) FROM CategoryAxis").fetchone()[0] == 2
    assert con.execute("SELECT COUNT(*) FROM CategoryValue").fetchone()[0] == 5
    assert con.execute("SELECT COUNT(*) FROM MailAccountSetting").fetchone()[0] == 1


TESTS = [
    ("5.3 カテゴリ軸は種別/技術領域の2件固定", t_axes_fixed),
    ("5.3 同一軸内の値名は重複不可・別軸なら同名可", t_value_unique_per_axis),
    ("5.3 カテゴリ値削除で紐付けが自動的に外れる", t_value_delete_cascades),
    ("5.3 使用中の軸は削除できない", t_axis_delete_restricted),
    ("宛先削除で中間テーブルも削除される", t_contact_delete_cascades),
    ("5.1 メモは100文字まで・任意項目", t_memo_length_limit),
    ("4章 重複メールアドレスをDBとして許容", t_duplicate_email_allowed),
    ("7.1 会社名/担当者名が空欄の宛先が存在しうる", t_empty_name_allowed),
    ("5.2 停止フラグは0/1のみ", t_suspended_flag_domain),
    ("3章 メールアカウント設定は単一行", t_mail_account_single_row),
    ("3章 暗号化方式はMailKit既知の値のみ", t_mail_account_option_domain),
    ("6.1 軸内OR・軸間AND・停止中除外の絞り込み", t_or_within_axis_and_between_axes),
    ("10.3 カテゴリ値の使用中件数を取得できる", t_usage_count_query),
    ("中間テーブルの複合主キーで二重登録を防ぐ", t_link_pk_prevents_duplicates),
    ("存在しないカテゴリ値への紐付けを外部キーで拒否", t_link_fk_enforced),
    ("9章 送信履歴テーブルは作らない", t_no_send_history_table),
    ("DDL/Seedの再実行が安全（冪等）", t_scripts_are_idempotent),
]

for label, fn in TESTS:
    check(label, fn)

passed = sum(1 for ok, _, _ in results if ok)
for ok, label, msg in results:
    print(f"{'PASS' if ok else 'FAIL'}  {label}" + (f"\n      -> {msg}" if not ok else ""))
print(f"\n{passed}/{len(results)} passed  (SQLite {sqlite3.sqlite_version})")
sys.exit(0 if passed == len(results) else 1)
