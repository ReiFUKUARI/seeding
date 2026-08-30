using Microsoft.Data.Sqlite;
using MailDeliveryTool.Core.Models;

namespace MailDeliveryTool.Core.Data.Repositories;

/// <summary>宛先（Contact）のCRUDと、絞り込み検索を担う。</summary>
public sealed class ContactRepository
{
    private readonly SqliteConnectionFactory _factory;

    public ContactRepository(SqliteConnectionFactory factory) => _factory = factory;

    /// <summary>全件取得（停止中も含む）。パートナーリスト画面用。</summary>
    public List<Contact> GetAll()
    {
        using var connection = _factory.Create();
        var contacts = LoadContacts(connection, whereClause: null, parameters: null);
        AttachCategoryValues(connection, contacts);
        return contacts;
    }

    public Contact? GetById(long id)
    {
        using var connection = _factory.Create();
        var contacts = LoadContacts(connection, "c.Id = $id", new Dictionary<string, object> { ["$id"] = id });
        if (contacts.Count == 0)
        {
            return null;
        }

        AttachCategoryValues(connection, contacts);
        return contacts[0];
    }

    /// <summary>
    /// 「新しい配信」画面の「すべて」タブ用の検索（要件定義書 6.1）。
    /// 会社名は部分一致。カテゴリは軸内OR・軸間AND。停止中は常に除外する。
    /// </summary>
    /// <param name="companyKeyword">会社名の部分一致キーワード（null/空文字なら絞り込まない）。</param>
    /// <param name="axisFilters">軸ID→選択されたカテゴリ値IDの一覧。値が空の軸は絞り込みに使わない。</param>
    public List<Contact> Search(string? companyKeyword, IReadOnlyDictionary<long, IReadOnlyList<long>>? axisFilters)
    {
        using var connection = _factory.Create();

        var conditions = new List<string> { "c.IsSuspended = 0" };
        var parameters = new Dictionary<string, object>();

        if (!string.IsNullOrWhiteSpace(companyKeyword))
        {
            conditions.Add("c.CompanyName LIKE $keyword ESCAPE '\\'");
            parameters["$keyword"] = "%" + EscapeLike(companyKeyword) + "%";
        }

        if (axisFilters is not null)
        {
            var axisIndex = 0;
            foreach (var (_, valueIds) in axisFilters)
            {
                if (valueIds is null || valueIds.Count == 0)
                {
                    continue;
                }

                var placeholders = new List<string>();
                for (var i = 0; i < valueIds.Count; i++)
                {
                    var paramName = $"$axis{axisIndex}_v{i}";
                    placeholders.Add(paramName);
                    parameters[paramName] = valueIds[i];
                }

                conditions.Add(
                    "EXISTS (SELECT 1 FROM ContactCategoryValue x " +
                    $"WHERE x.ContactId = c.Id AND x.CategoryValueId IN ({string.Join(",", placeholders)}))");
                axisIndex++;
            }
        }

        var contacts = LoadContacts(connection, string.Join(" AND ", conditions), parameters);
        AttachCategoryValues(connection, contacts);
        return contacts;
    }

    /// <summary>宛先を新規登録する。カテゴリ値の紐付けも同一トランザクションで登録する。</summary>
    public long Add(Contact contact)
    {
        using var connection = _factory.Create();
        using var transaction = connection.BeginTransaction();

        var now = DateTimeOffset.Now.ToString("O");
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO Contact (CompanyName, ContactName, Email, Memo, IsSuspended, CreatedAt, UpdatedAt)
                VALUES ($company, $name, $email, $memo, $suspended, $createdAt, $updatedAt)
                """;
            command.Parameters.AddWithValue("$company", contact.CompanyName);
            command.Parameters.AddWithValue("$name", contact.ContactName);
            command.Parameters.AddWithValue("$email", contact.Email);
            command.Parameters.AddWithValue("$memo", (object?)contact.Memo ?? DBNull.Value);
            command.Parameters.AddWithValue("$suspended", contact.IsSuspended ? 1 : 0);
            command.Parameters.AddWithValue("$createdAt", now);
            command.Parameters.AddWithValue("$updatedAt", now);
            command.ExecuteNonQuery();

            using var idCommand = connection.CreateCommand();
            idCommand.Transaction = transaction;
            idCommand.CommandText = "SELECT last_insert_rowid();";
            contact.Id = Convert.ToInt64(idCommand.ExecuteScalar());
        }

        ReplaceCategoryValues(connection, transaction, contact.Id, contact.CategoryValueIds);

        transaction.Commit();
        return contact.Id;
    }

    /// <summary>
    /// 宛先を更新する（会社名・担当者名・メールアドレス・メモ・カテゴリ値）。
    /// 停止フラグは変更しない（<see cref="SetSuspended"/> を使うこと）。
    /// </summary>
    public void Update(Contact contact)
    {
        using var connection = _factory.Create();
        using var transaction = connection.BeginTransaction();

        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE Contact
                SET CompanyName = $company, ContactName = $name, Email = $email,
                    Memo = $memo, UpdatedAt = $updatedAt
                WHERE Id = $id
                """;
            command.Parameters.AddWithValue("$company", contact.CompanyName);
            command.Parameters.AddWithValue("$name", contact.ContactName);
            command.Parameters.AddWithValue("$email", contact.Email);
            command.Parameters.AddWithValue("$memo", (object?)contact.Memo ?? DBNull.Value);
            command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.Now.ToString("O"));
            command.Parameters.AddWithValue("$id", contact.Id);
            if (command.ExecuteNonQuery() == 0)
            {
                throw new InvalidOperationException($"宛先が見つかりません（Id={contact.Id}）。");
            }
        }

        ReplaceCategoryValues(connection, transaction, contact.Id, contact.CategoryValueIds);

        transaction.Commit();
    }

    /// <summary>停止／再開を切り替える（要件定義書 5.2）。</summary>
    public void SetSuspended(long id, bool suspended)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Contact SET IsSuspended = $suspended, UpdatedAt = $updatedAt WHERE Id = $id";
        command.Parameters.AddWithValue("$suspended", suspended ? 1 : 0);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.Now.ToString("O"));
        command.Parameters.AddWithValue("$id", id);
        if (command.ExecuteNonQuery() == 0)
        {
            throw new InvalidOperationException($"宛先が見つかりません（Id={id}）。");
        }
    }

    private static List<Contact> LoadContacts(
        SqliteConnection connection, string? whereClause, IReadOnlyDictionary<string, object>? parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT Id, CompanyName, ContactName, Email, Memo, IsSuspended, CreatedAt, UpdatedAt FROM Contact c";
        if (!string.IsNullOrEmpty(whereClause))
        {
            command.CommandText += " WHERE " + whereClause;
        }

        command.CommandText += " ORDER BY CompanyName, Id";

        if (parameters is not null)
        {
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }
        }

        var result = new List<Contact>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Contact
            {
                Id = reader.GetInt64(0),
                CompanyName = reader.GetString(1),
                ContactName = reader.GetString(2),
                Email = reader.GetString(3),
                Memo = reader.IsDBNull(4) ? null : reader.GetString(4),
                IsSuspended = reader.GetInt64(5) != 0,
                CreatedAt = DateTimeOffset.Parse(reader.GetString(6)),
                UpdatedAt = DateTimeOffset.Parse(reader.GetString(7)),
            });
        }

        return result;
    }

    private static void AttachCategoryValues(SqliteConnection connection, List<Contact> contacts)
    {
        if (contacts.Count == 0)
        {
            return;
        }

        var byId = contacts.ToDictionary(c => c.Id);

        using var command = connection.CreateCommand();
        var placeholders = string.Join(",", contacts.Select((_, i) => $"$id{i}"));
        command.CommandText =
            $"SELECT ContactId, CategoryValueId FROM ContactCategoryValue WHERE ContactId IN ({placeholders})";
        for (var i = 0; i < contacts.Count; i++)
        {
            command.Parameters.AddWithValue($"$id{i}", contacts[i].Id);
        }

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (byId.TryGetValue(reader.GetInt64(0), out var contact))
            {
                contact.CategoryValueIds.Add(reader.GetInt64(1));
            }
        }
    }

    private static void ReplaceCategoryValues(
        SqliteConnection connection, SqliteTransaction transaction, long contactId, IReadOnlyCollection<long> valueIds)
    {
        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM ContactCategoryValue WHERE ContactId = $id";
            delete.Parameters.AddWithValue("$id", contactId);
            delete.ExecuteNonQuery();
        }

        foreach (var valueId in valueIds.Distinct())
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                "INSERT INTO ContactCategoryValue (ContactId, CategoryValueId) VALUES ($contactId, $valueId)";
            insert.Parameters.AddWithValue("$contactId", contactId);
            insert.Parameters.AddWithValue("$valueId", valueId);
            insert.ExecuteNonQuery();
        }
    }

    /// <summary>LIKE検索の特殊文字（% _ \）をエスケープする。呼び出し側で前後に % を付けて使う。</summary>
    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
