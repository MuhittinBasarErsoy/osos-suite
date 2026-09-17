using System.Globalization;
using System.Text.Json;

namespace Osos.Core.Weather;

/// <summary>
/// Open-Meteo saatlik veri indiricisi (Python hava_durumu_indirici mantığının C# karşılığı).
/// Tarih aralığına göre uygun kaynağı seçer: Archive (ERA5), Historical Forecast, Forecast.
/// API anahtarı gerektirmez.
/// </summary>
public sealed class OpenMeteoClient
{
    public const string ForecastUrl = "https://api.open-meteo.com/v1/forecast";
    public const string HistoricalForecastUrl = "https://historical-forecast-api.open-meteo.com/v1/forecast";
    public const string ArchiveUrl = "https://archive-api.open-meteo.com/v1/archive";

    public static readonly string[] Variables =
    {
        "temperature_2m","relative_humidity_2m","dew_point_2m","apparent_temperature",
        "precipitation","rain","snowfall","snow_depth","weather_code","pressure_msl",
        "surface_pressure","cloud_cover","cloud_cover_low","cloud_cover_mid","cloud_cover_high",
        "et0_fao_evapotranspiration","vapour_pressure_deficit","wind_speed_10m","wind_speed_100m",
        "wind_direction_10m","wind_direction_100m","wind_gusts_10m",
        "soil_temperature_0_to_7cm","soil_temperature_7_to_28cm","soil_temperature_28_to_100cm",
        "soil_temperature_100_to_255cm","soil_moisture_0_to_7cm","soil_moisture_7_to_28cm",
        "soil_moisture_28_to_100cm","soil_moisture_100_to_255cm","shortwave_radiation",
        "direct_radiation","diffuse_radiation","direct_normal_irradiance","global_tilted_irradiance",
    };

    /// <summary>time + tüm değişkenler (kolon sırası).</summary>
    public static readonly string[] Columns = new[] { "time" }.Concat(Variables).ToArray();

    private readonly HttpClient _http;
    public OpenMeteoClient(HttpClient http) => _http = http;

    public sealed record WeatherResult(string RowsJson, int RowCount, IReadOnlyList<string> Sources);

    public async Task<WeatherResult> FetchHourlyAsync(double lat, double lon, DateOnly start, DateOnly end,
        string timezone, double? tilt, double? azimuth, CancellationToken ct = default)
    {
        if (start > end) throw new ArgumentException("Başlangıç tarihi bitiş tarihinden sonra olamaz.");

        var byTime = new SortedDictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        var sources = new List<string>();

        foreach (var (name, url, segStart, segEnd) in SourceSegments(start, end))
        {
            var rows = await FetchSegmentAsync(name, url, segStart, segEnd, lat, lon, timezone, tilt, azimuth, ct);
            foreach (var r in rows)
                byTime[(string)r["time"]!] = r;   // dedupe by time (sonraki kaynak öncekini ezer)
            sources.Add(name);
        }

        if (byTime.Count == 0) throw new InvalidOperationException("Seçilen tarih aralığı için veri bulunamadı.");

        string json = JsonSerializer.Serialize(byTime.Values);
        return new WeatherResult(json, byTime.Count, sources);
    }

    /// <summary>Tarih aralığını en uygun Open-Meteo kaynaklarına böler.</summary>
    private static List<(string name, string url, DateOnly start, DateOnly end)> SourceSegments(DateOnly start, DateOnly end)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var forecastFloor = today.AddDays(-92);
        var historicalFloor = new DateOnly(2022, 1, 1);
        var segments = new List<(string, string, DateOnly, DateOnly)>();

        if (start < historicalFloor)
            segments.Add(("Archive API (ERA5/ERA5-Land)", ArchiveUrl, start, Min(end, historicalFloor.AddDays(-1))));

        var histStart = Max(start, historicalFloor);
        var histEnd = Min(end, forecastFloor.AddDays(-1));
        if (histStart <= histEnd)
            segments.Add(("Historical Forecast API", HistoricalForecastUrl, histStart, histEnd));

        var liveStart = Max(start, forecastFloor);
        if (liveStart <= end)
            segments.Add(("Forecast API", ForecastUrl, liveStart, end));

        return segments.Where(s => s.Item3 <= s.Item4).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> FetchSegmentAsync(string name, string url,
        DateOnly start, DateOnly end, double lat, double lon, string timezone, double? tilt, double? azimuth, CancellationToken ct)
    {
        // Forecast modellerinde toprak katmanları farklı → Archive dışında soil_* isteme.
        var requestVars = url == ArchiveUrl ? Variables : Variables.Where(v => !v.StartsWith("soil_")).ToArray();

        var q = new List<string>
        {
            "latitude=" + lat.ToString(CultureInfo.InvariantCulture),
            "longitude=" + lon.ToString(CultureInfo.InvariantCulture),
            "start_date=" + start.ToString("yyyy-MM-dd"),
            "end_date=" + end.ToString("yyyy-MM-dd"),
            "hourly=" + string.Join(",", requestVars),
            "timezone=" + Uri.EscapeDataString(timezone),
        };
        if (tilt is not null) q.Add("tilt=" + tilt.Value.ToString(CultureInfo.InvariantCulture));
        if (azimuth is not null) q.Add("azimuth=" + azimuth.Value.ToString(CultureInfo.InvariantCulture));

        using var req = new HttpRequestMessage(HttpMethod.Get, url + "?" + string.Join("&", q));
        req.Headers.TryAddWithoutValidation("User-Agent", "HavaDurumuIndirici/3.0");

        using var resp = await _http.SendAsync(req, ct);
        string text = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            string reason = text;
            try { using var ed = JsonDocument.Parse(text); if (ed.RootElement.TryGetProperty("reason", out var r)) reason = r.GetString() ?? text; }
            catch { }
            throw new InvalidOperationException($"{name}: {reason}");
        }

        using var doc = JsonDocument.Parse(text);
        if (!doc.RootElement.TryGetProperty("hourly", out var hourly) ||
            !hourly.TryGetProperty("time", out var timeEl) || timeEl.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"{name}: seçilen dönem için veri bulunamadı.");

        var times = timeEl.EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
        var arrays = new Dictionary<string, JsonElement[]?>();
        foreach (var v in Variables)
            arrays[v] = hourly.TryGetProperty(v, out var arr) && arr.ValueKind == JsonValueKind.Array
                ? arr.EnumerateArray().ToArray() : null;

        var rows = new List<Dictionary<string, object?>>(times.Length);
        for (int i = 0; i < times.Length; i++)
        {
            var row = new Dictionary<string, object?>(Columns.Length) { ["time"] = times[i] };
            foreach (var v in Variables)
            {
                var a = arrays[v];
                row[v] = (a is not null && i < a.Length) ? ToValue(a[i]) : null;
            }
            rows.Add(row);
        }
        return rows;
    }

    private static object? ToValue(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Number => e.TryGetInt64(out var l) ? l : e.GetDouble(),
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Null => null,
        _ => e.GetRawText()
    };

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}
