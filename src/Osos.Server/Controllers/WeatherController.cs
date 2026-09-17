using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Osos.Contracts;
using Osos.Core.Weather;
using Osos.Server.Services;

namespace Osos.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/weather")]
public sealed class WeatherController : ControllerBase
{
    private readonly OpenMeteoClient _meteo;
    private readonly SearchService _search;

    public WeatherController(OpenMeteoClient meteo, SearchService search)
    {
        _meteo = meteo;
        _search = search;
    }

    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpPost]
    public async Task<ActionResult<OsosResult>> Get(WeatherQuery q, CancellationToken ct)
    {
        if (q.Latitude is < -90 or > 90 || q.Longitude is < -180 or > 180)
            return BadRequest(new { message = "Koordinatlar geçerli aralıkta değil." });
        if (q.StartDate.Date > q.EndDate.Date)
            return BadRequest(new { message = "Başlangıç tarihi bitiş tarihinden sonra olamaz." });

        try
        {
            var res = await _meteo.FetchHourlyAsync(
                q.Latitude, q.Longitude,
                DateOnly.FromDateTime(q.StartDate), DateOnly.FromDateTime(q.EndDate),
                string.IsNullOrWhiteSpace(q.Timezone) ? "Europe/Istanbul" : q.Timezone,
                q.Tilt, q.Azimuth, ct);

            var result = await _search.SaveExternalResultAsync(
                Uid, "Weather", "OpenMeteoHourly", q, res.RowsJson,
                serno: null, start: q.StartDate, end: q.EndDate, ct);

            return result;
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
