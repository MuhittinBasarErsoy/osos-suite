using System.Security.Claims;
using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Osos.Contracts;
using Osos.Server.Services;

namespace Osos.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/jobs")]
public sealed class JobsController : ControllerBase
{
    private readonly IBackgroundJobClient _jobs;
    private readonly IRecurringJobManager _recurring;

    public JobsController(IBackgroundJobClient jobs, IRecurringJobManager recurring)
    {
        _jobs = jobs;
        _recurring = recurring;
    }

    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string Prefix => $"q:{Uid}:";

    /// <summary>Sorguyu hemen (arka planda tek sefer) çalıştırır.</summary>
    [HttpPost("run-now")]
    public IActionResult RunNow(RunNowRequest req)
    {
        var uid = Uid;
        string id = _jobs.Enqueue<JobRunner>(r => r.RunQueryAsync(uid, req.Screen, req.Serno, req.DaysBack, req.Type));
        return Ok(new { jobId = id, message = "İş kuyruğa alındı." });
    }

    /// <summary>Zamanlanmış (cron) iş oluşturur/günceller.</summary>
    [HttpPost("schedule")]
    public IActionResult Schedule(ScheduleJobRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Cron))
            return BadRequest(new { message = "Cron ifadesi gerekli." });

        string slug = string.IsNullOrWhiteSpace(req.Name) ? Guid.NewGuid().ToString("N")[..8] : Sanitize(req.Name!);
        string id = $"{Prefix}{req.Screen}:{slug}";
        var uid = Uid;

        _recurring.AddOrUpdate<JobRunner>(
            id,
            r => r.RunQueryAsync(uid, req.Screen, req.Serno, req.DaysBack, req.Type),
            req.Cron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });

        return Ok(new { jobId = id, message = "Zamanlanmış iş oluşturuldu." });
    }

    /// <summary>Hava durumu işini hemen çalıştırır.</summary>
    [HttpPost("weather/run-now")]
    public IActionResult WeatherRunNow(WeatherJobRequest req)
    {
        var uid = Uid;
        string id = _jobs.Enqueue<JobRunner>(r => r.RunWeatherAsync(uid, req.Latitude, req.Longitude, req.DaysBack, req.Timezone, req.Tilt, req.Azimuth));
        return Ok(new { jobId = id, message = "Hava durumu işi kuyruğa alındı." });
    }

    /// <summary>Zamanlanmış hava durumu işi.</summary>
    [HttpPost("weather/schedule")]
    public IActionResult WeatherSchedule(WeatherJobRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Cron)) return BadRequest(new { message = "Cron ifadesi gerekli." });
        string slug = string.IsNullOrWhiteSpace(req.Name) ? Guid.NewGuid().ToString("N")[..8] : Sanitize(req.Name!);
        string id = $"{Prefix}Weather:{slug}";
        var uid = Uid;
        _recurring.AddOrUpdate<JobRunner>(id,
            r => r.RunWeatherAsync(uid, req.Latitude, req.Longitude, req.DaysBack, req.Timezone, req.Tilt, req.Azimuth),
            req.Cron, new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        return Ok(new { jobId = id, message = "Zamanlanmış hava durumu işi oluşturuldu." });
    }

    /// <summary>Kullanıcının zamanlanmış işleri.</summary>
    [HttpGet]
    public ActionResult<IEnumerable<JobDto>> List()
    {
        using var conn = JobStorage.Current.GetConnection();
        var jobs = conn.GetRecurringJobs()
            .Where(j => j.Id.StartsWith(Prefix))
            .Select(j =>
            {
                bool isWeather = j.Job?.Method?.Name == nameof(JobRunner.RunWeatherAsync);
                var (screen, serno, daysBack) = isWeather ? ("Weather", 0L, ArgInt(j, 3)) : ParseArgs(j);
                return new JobDto(
                    j.Id, screen, serno, daysBack, j.Cron,
                    j.NextExecution?.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
                    j.LastExecution?.ToLocalTime().ToString("dd.MM.yyyy HH:mm"),
                    j.LastJobState);
            })
            .ToList();
        return jobs;
    }

    private static int ArgInt(RecurringJobDto j, int index)
    {
        try
        {
            var a = System.Text.Json.JsonSerializer.Serialize(j.Job?.Args ?? new List<object>());
            using var doc = System.Text.Json.JsonDocument.Parse(a);
            return doc.RootElement.GetArrayLength() > index && doc.RootElement[index].TryGetInt32(out var v) ? v : 0;
        }
        catch { return 0; }
    }

    /// <summary>Zamanlanmış işi hemen tetikler.</summary>
    [HttpPost("{id}/trigger")]
    public IActionResult Trigger(string id)
    {
        if (!id.StartsWith(Prefix)) return NotFound();
        _recurring.Trigger(id);
        return Ok(new { message = "Tetiklendi." });
    }

    /// <summary>Zamanlanmış işi siler.</summary>
    [HttpDelete("{id}")]
    public IActionResult Delete(string id)
    {
        if (!id.StartsWith(Prefix)) return NotFound();
        _recurring.RemoveIfExists(id);
        return NoContent();
    }

    private static (string screen, long serno, int daysBack) ParseArgs(RecurringJobDto j)
    {
        // Job argümanları: RunQueryAsync(appUserId, screen, serno, daysBack, type)
        try
        {
            var args = System.Text.Json.JsonDocument.Parse(j.Job?.Args is null ? "[]"
                : System.Text.Json.JsonSerializer.Serialize(j.Job.Args));
            var a = args.RootElement;
            string screen = a.GetArrayLength() > 1 ? a[1].GetString() ?? "" : "";
            long serno = a.GetArrayLength() > 2 && a[2].TryGetInt64(out var s) ? s : 0;
            int days = a.GetArrayLength() > 3 && a[3].TryGetInt32(out var d) ? d : 0;
            return (screen, serno, days);
        }
        catch { return ("", 0, 0); }
    }

    private static string Sanitize(string s)
    {
        var chars = s.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray();
        var r = new string(chars);
        return string.IsNullOrEmpty(r) ? Guid.NewGuid().ToString("N")[..8] : (r.Length > 40 ? r[..40] : r);
    }
}
