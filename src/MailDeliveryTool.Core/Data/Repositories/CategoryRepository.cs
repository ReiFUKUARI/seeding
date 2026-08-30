using Microsoft.Data.Sqlite;
using MailDeliveryTool.Core.Models;

namespace MailDeliveryTool.Core.Data.Repositories;

/// <summary>カテゴリ軸・カテゴリ値の参照とCRUD（要件定義書 5.3・10.3）。</summary>
public sealed class CategoryRepository
{
    /// <summary>SQLite の SQLITE_CONSTRAINT 主結果コード。</summary>
    private const int SqliteConstraintErrorCode = 19;

    private readonly SqliteConnectionFactory _factory;

    public CategoryRepository(SqliteConnectionFactory factory) => _factory = factory;

    /// <summary>2つの軸を、それぞれの配下の値（DisplayOrder順）付きで取得する。</summary>
    public List<CategoryAxis> GetAxes()
    {
        using var connection = _factory.Create();

        var axes = new List<CategoryAxis>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT Id, Code, Name, DisplayOrder FROM CategoryAxis ORDER BY DisplayOrder";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                axes.Add(new CategoryAxis
                {
                    Id = reader.GetInt64(0),
                    Code = reader.GetString(1),
                    Name = reader.GetString(2),
                    DisplayOrder = reader.GetInt32(3),
                });
            }
        }

        var byId = axes.ToDictionary(a => a.Id);
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT Id, AxisId, Name, DisplayOrder FROM CategoryValue ORDER BY AxisId, DisplayOrder, Id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var value = new CategoryValue
                {
                    Id = reader.GetInt64(0),
                    AxisId = reader.GetInt64(1),
                    Name = reader.GetString(2),
                    DisplayOrder = reader.GetInt32(3),
                };
                if (byId.TryGetValue(value.AxisId, out var axis))
                {
                    axis.Values.Add(value);
                }
            }
        }

        return axes;
    }

    /// <summary>
    /// カテゴリ値を追加する（要件定義書 5.3・10.3）。表示順は同一軸内の末尾に自動採番する。
    /// 同一軸内での重複は <see cref="DuplicateCategoryValueException"/> を投げる（DBのUNIQUE制約に対応）。
    /// </summary>
    public long AddValue(long axisId, string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("カテゴリ値の名前が空です。", nameof(name));
        }

        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO CategoryValue (AxisId, Name, DisplayOrder)
            VALUES ($axisId, $name,
                (SELECT IFNULL(MAX(DisplayOrder), 0) + 1 FROM CategoryValue WHERE AxisId = $axisId))
            """;
        command.Parameters.AddWithValue("$axisId", axisId);
        command.Parameters.AddWithValue("$name", trimmed);

        try
        {
            command.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteConstraintErrorCode)
        {
            throw new DuplicateCategoryValueException(axisId, trimmed, ex);
        }

        return connection.LastInsertRowId;
    }

    /// <summary>
    /// カテゴリ値を削除する。紐付いていた宛先からは自動的に外れる
    /// （DBのON DELETE CASCADE。要件定義書5.3）。呼び出し前に <see cref="GetUsageCount"/> で
    /// 使用件数を確認し、警告を出すこと（要件定義書10.3）。
    /// </summary>
    public void DeleteValue(long valueId)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM CategoryValue WHERE Id = $id";
        command.Parameters.AddWithValue("$id", valueId);
        if (command.ExecuteNonQuery() == 0)
        {
            throw new InvalidOperationException($"カテゴリ値が見つかりません（Id={valueId}）。");
        }
    }

    /// <summary>このカテゴリ値を使用している宛先の件数。削除前の警告表示に使う（要件定義書10.3）。</summary>
    public int GetUsageCount(long valueId)
    {
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(DISTINCT ContactId) FROM ContactCategoryValue WHERE CategoryValueId = $id";
        command.Parameters.AddWithValue("$id", valueId);
        return Convert.ToInt32(command.ExecuteScalar());
    }
}

/// <summary>同一軸内に同名のカテゴリ値を追加しようとした場合の例外。</summary>
public sealed class DuplicateCategoryValueException : Exception
{
    public long AxisId { get; }
    public string Name { get; }

    public DuplicateCategoryValueException(long axisId, string name, Exception inner)
        : base($"軸ID={axisId} には既に「{name}」が登録されています。", inner)
    {
        AxisId = axisId;
        Name = name;
    }
}
