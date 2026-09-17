using Microsoft.Extensions.Logging;
using Osos.Core.Weather;

namespace Osos.Server.Services;

/// <summary>
/// Hangfire iş metodları. Zamanlanmış/anlık olarak bir kullanıcı adına OSOS sorgusu çalıştırır
/// ve sonucu DB'ye (JSON snapshot + Rows_ tablosu) kaydeder. Argümanlar Hangfire için basit tiptir.
/// </summary>
public sealed class JobRunner
{
    private readonly SearchService _search;
    private readonly OsosSessionService _osos;
    private readonly OpenMeteoClient _meteo;
    private readonly ILogger<JobRunner> _logger;

    public JobRunner(SearchService search, OsosSessionService osos, OpenMeteoClient meteo, ILogger<JobRunner> logger)
    {
        _search = search;
        _osos = osos;
        _meteo = meteo;
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

    /// <summary>Hava durumu (Open-Meteo) işini çalıştırır. daysBack: kayan aralık (bitiş=bugün).</summary>
    public async Task RunWeatherAsync(string appUserId, double lat, double lon, int daysBack, string timezone, double? tilt, double? azimuth)
    {
        var ct = CancellationToken.None;
        try
        {
            var end = DateTime.Now;
            var start = end.AddDays(-Math.Max(0, daysBack));
            var res = await _meteo.FetchHourlyAsync(lat, lon,
                DateOnly.FromDateTime(start), DateOnly.FromDateTime(end),
                string.IsNullOrWhiteSpace(timezone) ? "Europe/Istanbul" : timezone, tilt, azimuth, ct);
            var saved = await _search.SaveExternalResultAsync(appUserId, "Weather", "OpenMeteoHourly",
                new { lat, lon, timezone, tilt, azimuth, daysBack }, res.RowsJson, null, start, end, ct);
            _logger.LogInformation("Weather job: user={User} lat={Lat} lon={Lon} satır={Rows} geçmiş#{Id}",
                appUserId, lat, lon, saved.RowCount, saved.SearchHistoryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Weather job hatası: user={User}", appUserId);
            throw;
        }
    }
}
