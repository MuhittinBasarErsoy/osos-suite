using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Osos.Contracts;
using Osos.Server.Data;

namespace Osos.Server.Services;

/// <summary>OSOS çağrılarını çalıştırır, geçmişi + sonuç anlık görüntüsünü MSSQL'e kaydeder.</summary>
public sealed class SearchService
{
    private readonly AppDbContext _db;
    private readonly OsosSessionService _osos;

    public SearchService(AppDbContext db, OsosSessionService osos)
    {
        _db = db;
        _osos = osos;
    }

    /// <summary>OSOS tarih formatı: yyyyMMddHHmmss (long).</summary>
    public static long ToOsosDate(DateTime dt) => long.Parse(dt.ToString("yyyyMMddHHmmss"));

    /// <summary>Çağrıyı yapar, geçmiş + snapshot kaydeder, ham sonucu döner.</summary>
    public async Task<OsosResult> RunAndSaveAsync(
        string appUserId, string screen, string methodName, object parameters, long? serno,
        DateTime? start, DateTime? end, CancellationToken ct)
    {
        string rawJson = await _osos.CallAsync(appUserId, methodName, parameters, ct);
        int rowCount = CountRows(rawJson);

        var history = new SearchHistory
        {
            AppUserId = appUserId,
            Screen = screen,
            MethodName = methodName,
            ParametersJson = JsonSerializer.Serialize(parameters),
            Serno = serno,
            StartDate = start,
            EndDate = end,
            RowCount = rowCount,
            Snapshot = new SearchResultSnapshot { ResultJson = rawJson, RowCount = rowCount }
        };
        _db.SearchHistories.Add(history);
        await _db.SaveChangesAsync(ct);

        return new OsosResult(rawJson, rowCount, history.Id);
    }

    public async Task<PagedResult<SearchHistoryDto>> GetHistoryAsync(string appUserId, int page, int pageSize, CancellationToken ct)
    {
        var q = _db.SearchHistories.AsNoTracking().Where(h => h.AppUserId == appUserId).OrderByDescending(h => h.CreatedAt);
        int total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(h => new SearchHistoryDto(h.Id, h.Screen, h.MethodName, h.ParametersJson, h.Serno,
                h.StartDate, h.EndDate, h.RowCount, h.CreatedAt))
            .ToListAsync(ct);
        return new PagedResult<SearchHistoryDto>(items, total, page, pageSize);
    }

    public async Task<SearchResultDto?> GetSnapshotAsync(string appUserId, long searchId, CancellationToken ct)
    {
        var h = await _db.SearchHistories.AsNoTracking().Include(x => x.Snapshot)
            .FirstOrDefaultAsync(x => x.Id == searchId && x.AppUserId == appUserId, ct);
        if (h?.Snapshot is null) return null;
        return new SearchResultDto(h.Id, h.Snapshot.ResultJson, h.Snapshot.RowCount, h.Snapshot.CapturedAt);
    }

    /// <summary>Kayıtlı bir aramayı aynı parametrelerle tekrar çalıştırır (yeni geçmiş kaydı).</summary>
    public async Task<OsosResult> RerunAsync(string appUserId, long searchId, CancellationToken ct)
    {
        var h = await _db.SearchHistories.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == searchId && x.AppUserId == appUserId, ct)
            ?? throw new InvalidOperationException("Arama bulunamadı.");

        using var doc = JsonDocument.Parse(h.ParametersJson);
        object parameters = doc.RootElement.Clone();
        return await RunAndSaveAsync(appUserId, h.Screen, h.MethodName, parameters, h.Serno, h.StartDate, h.EndDate, ct);
    }

    public async Task<bool> DeleteAsync(string appUserId, long searchId, CancellationToken ct)
    {
        int n = await _db.SearchHistories.Where(x => x.Id == searchId && x.AppUserId == appUserId).ExecuteDeleteAsync(ct);
        return n > 0;
    }

    /// <summary>Kayıtlı bir aramanın sonucunu CSV (UTF-8 BOM, Excel uyumlu) olarak üretir.</summary>
    public async Task<(byte[] bytes, string fileName)?> ExportCsvAsync(string appUserId, long searchId, CancellationToken ct)
    {
        var h = await _db.SearchHistories.AsNoTracking().Include(x => x.Snapshot)
            .FirstOrDefaultAsync(x => x.Id == searchId && x.AppUserId == appUserId, ct);
        if (h?.Snapshot is null) return null;

        string csv = JsonToCsv(h.Snapshot.ResultJson);
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        byte[] body = System.Text.Encoding.UTF8.GetBytes(csv);
        byte[] bytes = new byte[bom.Length + body.Length];
        Buffer.BlockCopy(bom, 0, bytes, 0, bom.Length);
        Buffer.BlockCopy(body, 0, bytes, bom.Length, body.Length);

        string fileName = $"{h.Screen}_{h.Id}_{h.CreatedAt:yyyyMMdd_HHmm}.csv";
        return (bytes, fileName);
    }

    private static string JsonToCsv(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var arr = FindFirstArray(doc.RootElement);
        if (arr is null) return "";

        // sütunları objelerin birleşiminden topla
        var cols = new List<string>();
        var rows = new List<Dictionary<string, string>>();
        foreach (var item in arr.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var dict = new Dictionary<string, string>();
            foreach (var p in item.EnumerateObject())
            {
                if (!cols.Contains(p.Name)) cols.Add(p.Name);
                dict[p.Name] = CellValue(p.Value);
            }
            rows.Add(dict);
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Join(";", cols.Select(Escape)));
        foreach (var r in rows)
            sb.AppendLine(string.Join(";", cols.Select(c => Escape(r.TryGetValue(c, out var v) ? v : ""))));
        return sb.ToString();
    }

    private static string CellValue(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => v.GetString() ?? "",
        JsonValueKind.Number => v.GetRawText(),
        JsonValueKind.True => "Evet",
        JsonValueKind.False => "Hayır",
        JsonValueKind.Null => "",
        _ => v.GetRawText()
    };

    // Excel için ; ayraçlı; alan içinde ; " veya yeni satır varsa tırnakla.
    private static string Escape(string s)
    {
        if (s.Contains(';') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    private static JsonElement? FindFirstArray(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var i in el.EnumerateArray()) if (i.ValueKind == JsonValueKind.Object) return el;
                return el.GetArrayLength() > 0 ? null : el;
            case JsonValueKind.Object:
                foreach (var p in el.EnumerateObject())
                {
                    var r = FindFirstArray(p.Value);
                    if (r is not null) return r;
                }
                return null;
            default: return null;
        }
    }

    /// <summary>Yanıttaki satır sayısını kabaca tahmin eder (ilk bulunan dizi).</summary>
    private static int CountRows(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return FindFirstArrayLength(doc.RootElement) ?? 0;
        }
        catch { return 0; }
    }

    private static int? FindFirstArrayLength(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Array:
                return el.GetArrayLength();
            case JsonValueKind.Object:
                foreach (var p in el.EnumerateObject())
                {
                    var r = FindFirstArrayLength(p.Value);
                    if (r is not null) return r;
                }
                return null;
            default:
                return null;
        }
    }
}
