using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Osos.Server.Data;

namespace Osos.Server.Services;

/// <summary>
/// OSOS sorgu sonucunu, JSON dışında, her ekran tipi için ayrı bir SQL tablosuna
/// gerçek sütunlarla yazar. Tablo ve sütunlar çalışma anında (dinamik) oluşturulur.
/// Tablolar: Rows_Consumption, Rows_Endex, Rows_Profiles, Rows_Subscriptions, Rows_Dashboard ...
/// </summary>
public sealed class ResultMaterializer
{
    private readonly AppDbContext _db;
    public ResultMaterializer(AppDbContext db) => _db = db;

    public async Task MaterializeAsync(string screen, long searchHistoryId, string appUserId,
        long? serno, string resultJson, CancellationToken ct)
    {
        var rows = Flatten(resultJson);
        if (rows.Count == 0) return;

        string table = "Rows_" + Sanitize(screen);
        var columns = new List<string>();
        foreach (var r in rows)
            foreach (var k in r.Keys)
            {
                var c = Sanitize(k);
                if (c.Length > 0 && !columns.Contains(c)) columns.Add(c);
            }

        var conn = _db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);

        await EnsureTableAsync(conn, table, ct);
        await EnsureColumnsAsync(conn, table, columns, ct);
        await InsertRowsAsync(conn, table, columns, rows, searchHistoryId, appUserId, serno, ct);
    }

    private static async Task EnsureTableAsync(DbConnection conn, string table, CancellationToken ct)
    {
        string sql = $@"IF OBJECT_ID(N'[dbo].[{table}]', N'U') IS NULL
CREATE TABLE [dbo].[{table}] (
    [Id] BIGINT IDENTITY(1,1) PRIMARY KEY,
    [SearchHistoryId] BIGINT NOT NULL,
    [AppUserId] NVARCHAR(450) NULL,
    [Serno] BIGINT NULL,
    [CapturedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);";
        await Exec(conn, sql, ct);
    }

    private static async Task EnsureColumnsAsync(DbConnection conn, string table, List<string> columns, CancellationToken ct)
    {
        foreach (var c in columns)
        {
            // Rezerve sütunlarla çakışmasın
            if (c is "Id" or "SearchHistoryId" or "AppUserId" or "Serno" or "CapturedAt") continue;
            string sql = $"IF COL_LENGTH(N'[dbo].[{table}]', N'{c.Replace("'", "''")}') IS NULL ALTER TABLE [dbo].[{table}] ADD [{c}] NVARCHAR(MAX) NULL;";
            await Exec(conn, sql, ct);
        }
    }

    private static async Task InsertRowsAsync(DbConnection conn, string table, List<string> columns,
        List<Dictionary<string, string>> rows, long searchHistoryId, string appUserId, long? serno, CancellationToken ct)
    {
        var dataCols = columns.Where(c => c is not ("Id" or "SearchHistoryId" or "AppUserId" or "Serno" or "CapturedAt")).ToList();

        foreach (var r in rows)
        {
            var colList = new StringBuilder("[SearchHistoryId],[AppUserId],[Serno]");
            var valList = new StringBuilder("@sh,@au,@sn");
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "";
            AddParam(cmd, "@sh", searchHistoryId);
            AddParam(cmd, "@au", (object?)appUserId ?? DBNull.Value);
            AddParam(cmd, "@sn", (object?)serno ?? DBNull.Value);

            int i = 0;
            foreach (var c in dataCols)
            {
                // orijinal alan adını sanitize edilmiş isimle eşle
                string? val = null;
                foreach (var kv in r) if (Sanitize(kv.Key) == c) { val = kv.Value; break; }
                string p = "@p" + i++;
                colList.Append(",[").Append(c).Append(']');
                valList.Append(',').Append(p);
                AddParam(cmd, p, (object?)val ?? DBNull.Value);
            }

            cmd.CommandText = $"INSERT INTO [dbo].[{table}] ({colList}) VALUES ({valList});";
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static void AddParam(DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static async Task Exec(DbConnection conn, string sql, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>SQL tanımlayıcı için güvenli isim: yalnızca harf/rakam/alt çizgi/boşluk; en çok 128 karakter.</summary>
    private static string Sanitize(string name)
    {
        var sb = new StringBuilder();
        foreach (var ch in name)
            if (char.IsLetterOrDigit(ch) || ch == '_' || ch == ' ') sb.Append(ch);
        var s = sb.ToString().Trim();
        if (s.Length > 128) s = s[..128];
        if (s.Length == 0) s = "col";
        if (char.IsDigit(s[0])) s = "c_" + s;
        return s;
    }

    /// <summary>Yanıttaki ilk obje dizisini bulup satırları string sözlüklerine düzleştirir.</summary>
    private static List<Dictionary<string, string>> Flatten(string json)
    {
        var result = new List<Dictionary<string, string>>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var arr = FindFirstObjectArray(doc.RootElement);
            if (arr is null) return result;
            foreach (var item in arr.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var d = new Dictionary<string, string>();
                JsonProperty? explode = null;
                foreach (var p in item.EnumerateObject())
                    d[p.Name] = Cell(p.Value);   // JSON sütunu ham haliyle kalır
                foreach (var p in item.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.Object)
                        AddChildren(d, p.Name, p.Value);
                    else if (explode is null && IsObjectArray(p.Value))
                        explode = p;
                }

                if (explode is null) { result.Add(d); continue; }

                // İç içe obje dizisi: her eleman ayrı satır; JSON sütununda o elemanın JSON'u,
                // alanları da kendi sütunlarında.
                foreach (var child in explode.Value.Value.EnumerateArray())
                {
                    if (child.ValueKind != JsonValueKind.Object) continue;
                    var row = new Dictionary<string, string>(d) { [explode.Value.Name] = child.GetRawText() };
                    AddChildren(row, explode.Value.Name, child);
                    result.Add(row);
                }
            }
        }
        catch { /* düzleştirilemiyorsa boş geç */ }
        return result;
    }

    /// <summary>İç içe objenin skaler alanlarını kendi sütunlarına ekler; ad çakışırsa "üst_alt" kullanılır.</summary>
    private static void AddChildren(Dictionary<string, string> d, string parent, JsonElement obj)
    {
        foreach (var c in obj.EnumerateObject())
        {
            if (c.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array) continue;
            var name = d.ContainsKey(c.Name) ? parent + "_" + c.Name : c.Name;
            d[name] = Cell(c.Value);
        }
    }

    private static bool IsObjectArray(JsonElement v)
    {
        if (v.ValueKind != JsonValueKind.Array) return false;
        foreach (var i in v.EnumerateArray()) if (i.ValueKind == JsonValueKind.Object) return true;
        return false;
    }

    private static string Cell(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => v.GetString() ?? "",
        JsonValueKind.Number => v.GetRawText(),
        JsonValueKind.True => "1",
        JsonValueKind.False => "0",
        JsonValueKind.Null => "",
        _ => v.GetRawText()   // iç içe obje/dizi → ham metin
    };

    private static JsonElement? FindFirstObjectArray(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var i in el.EnumerateArray()) if (i.ValueKind == JsonValueKind.Object) return el;
                return null;
            case JsonValueKind.Object:
                foreach (var p in el.EnumerateObject())
                {
                    var r = FindFirstObjectArray(p.Value);
                    if (r is not null) return r;
                }
                return null;
            default: return null;
        }
    }
}
