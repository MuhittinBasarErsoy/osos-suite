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
