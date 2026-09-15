using Microsoft.Extensions.Logging;

namespace Osos.Server.Services;

/// <summary>
/// Hangfire iş metodları. Zamanlanmış/anlık olarak bir kullanıcı adına OSOS sorgusu çalıştırır
/// ve sonucu DB'ye (JSON snapshot + Rows_ tablosu) kaydeder. Argümanlar Hangfire için basit tiptir.
/// </summary>
public sealed class JobRunner
{
    private readonly SearchService _search;
    private readonly OsosSessionService _osos;
    private readonly ILogger<JobRunner> _logger;

    public JobRunner(SearchService search, OsosSessionService osos, ILogger<JobRunner> logger)
    {
        _search = search;
        _osos = osos;
        _logger = logger;
    }

    /// <summary>
    /// Sorguyu çalıştırır. serno=0 ise müşteri Serno'su otomatik kullanılır.
    /// daysBack: bitiş = şimdi, başlangıç = şimdi - daysBack gün (zamanlı işlerde kayan aralık).
    /// </summary>
    public async Task RunQueryAsync(string appUserId, string screen, long serno, int daysBack, int type)
    {
        var ct = CancellationToken.None;
        try
        {
            long effectiveSerno = serno > 0 ? serno : await _osos.GetCustomerSernoAsync(appUserId, ct);
            var end = DateTime.Now;
            var start = end.AddDays(-Math.Max(0, daysBack));
            var res = await _search.RunScreenAsync(appUserId, screen, effectiveSerno, start, end, type, null, ct);
            _logger.LogInformation("Job çalıştı: {Screen} user={User} serno={Serno} satır={Rows} geçmiş#{Id}",
                screen, appUserId, effectiveSerno, res.RowCount, res.SearchHistoryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job hatası: {Screen} user={User}", screen, appUserId);
            throw; // Hangfire yeniden denesin / dashboard'da görünsün
        }
    }
}
