using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Osos.Contracts;
using Osos.Core.Osos;
using Osos.Server.Data;
using Osos.Server.Services;

namespace Osos.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/osos")]
public sealed class OsosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly OsosSessionService _osos;
    private readonly SearchService _search;
    private readonly ILogger<OsosController> _logger;

    public OsosController(AppDbContext db, OsosSessionService osos, SearchService search, ILogger<OsosController> logger)
    {
        _db = db;
        _osos = osos;
        _search = search;
        _logger = logger;
    }

    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static long D(DateTime dt) => SearchService.ToOsosDate(dt);

    /// <summary>OSOS hesabını bağlar/günceller (giriş doğrulaması yapar).</summary>
    [HttpPost("link")]
    public async Task<ActionResult<OsosLinkResponse>> Link(OsosLinkRequest req, CancellationToken ct)
    {
        try
        {
            var (ok, msg, serno) = await _osos.TryLoginAsync(req.OsosUserCode, req.OsosPassword, req.RememberMe, ct);
            if (!ok) return BadRequest(new OsosLinkResponse(false, msg));

            var cred = await _db.OsosCredentials.FirstOrDefaultAsync(c => c.AppUserId == Uid, ct);
            if (cred is null)
            {
                cred = new OsosCredential { AppUserId = Uid };
                _db.OsosCredentials.Add(cred);
            }
            cred.OsosUserCode = req.OsosUserCode;
            cred.OsosPasswordProtected = _osos.Protect(req.OsosPassword);
            cred.RememberMe = req.RememberMe;
            cred.CustomerSerno = serno;
            cred.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            return new OsosLinkResponse(true, $"OSOS hesabı bağlandı (Serno: {serno}).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OSOS link hatası");
            return BadRequest(new OsosLinkResponse(false, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    /// <summary>Giriş yapan müşterinin Serno'su + tesisat/abone listesi (UI otomatik doldurma için).</summary>
    [HttpGet("me")]
    public async Task<ActionResult<object>> Me(CancellationToken ct)
    {
        try
        {
            var (serno, subsJson) = await _osos.GetProfileAsync(Uid, ct);
            using var doc = subsJson is null ? null : System.Text.Json.JsonDocument.Parse(subsJson);
            return Ok(new
            {
                serno,
                subscriptions = doc?.RootElement.Clone() ?? default
            });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Serno verilmemişse (<=0) müşteri Serno'sunu kullan.</summary>
    private async Task<long> ResolveSernoAsync(long requested, CancellationToken ct)
        => requested > 0 ? requested : await _osos.GetCustomerSernoAsync(Uid, ct);

    [HttpPost("consumption")]
    public async Task<ActionResult<OsosResult>> Consumption(ConsumptionQuery q, CancellationToken ct)
    {
        long serno = await ResolveSernoAsync(q.Serno, ct);
        return await Run("Consumption", OsosMethods.GetCustomerSelectedConsumptions, new
        {
            Serno = serno,
            StartDate = D(q.StartDate),
            EndDate = D(q.EndDate),
            Selected = q.Selected ?? Array.Empty<long>(),
            Type = q.Type,
            Period = q.Period,
            MarkFilterString = (string?)null,
            TitleFilterString = (string?)null,
            TotalItemCount = q.TotalItemCount
        }, serno, q.StartDate, q.EndDate, ct);
    }

    [HttpPost("endex")]
    public async Task<ActionResult<OsosResult>> Endex(EndexQuery q, CancellationToken ct)
    {
        long serno = await ResolveSernoAsync(q.Serno, ct);
        return await Run("Endex", OsosMethods.GetCustomerSelectedCurrentEndexes, new
        {
            Serno = serno,
            StartDate = D(q.StartDate),
            EndDate = D(q.EndDate),
            Selected = q.Selected ?? Array.Empty<long>(),
            MarkFilterString = (string?)null,
            TitleFilterString = (string?)null,
            TotalItemCount = q.TotalItemCount
        }, serno, q.StartDate, q.EndDate, ct);
    }

    [HttpPost("profiles")]
    public async Task<ActionResult<OsosResult>> Profiles(ProfilesQuery q, CancellationToken ct)
    {
        long serno = await ResolveSernoAsync(q.Serno, ct);
        return await Run("Profiles", OsosMethods.GetCustomerSelectedProfiles, new
        {
            Serno = serno,
            StartDate = D(q.StartDate),
            EndDate = D(q.EndDate),
            Selected = q.Selected ?? Array.Empty<long>(),
            MarkFilterString = (string?)null,
            TitleFilterString = (string?)null,
            TotalItemCount = q.TotalItemCount,
            WithourMultiplier = q.WithoutMultiplier   // OSOS'taki alan adı (yazım hatası korunuyor)
        }, serno, q.StartDate, q.EndDate, ct);
    }

    [HttpPost("subscriptions")]
    public async Task<ActionResult<OsosResult>> Subscriptions(SubscriptionsQuery q, CancellationToken ct)
    {
        long serno = await ResolveSernoAsync(q.Serno, ct);
        return await Run("Subscriptions", OsosMethods.GetCustomerPortalSubscriptions, new
        {
            Serno = serno,
            PageSize = q.PageSize,
            PageNumber = q.PageNumber
        }, serno, null, null, ct);
    }

    [HttpPost("dashboard/owner-consumptions")]
    public Task<ActionResult<OsosResult>> OwnerConsumptions(OwnerConsumptionsQuery q, CancellationToken ct) =>
        Run("Dashboard", OsosMethods.GetOwnerConsumptions, new
        {
            OwnerSerno = q.OwnerSerno,
            OwnerType = q.OwnerType,
            StartDate = D(q.StartDate),
            EndDate = D(q.EndDate),
            IsOnlySuccess = q.IsOnlySuccess,
            IncludeLoadProfiles = q.IncludeLoadProfiles,
            IncludeVersions = false,
            WithoutMultiplier = q.WithoutMultiplier,
            MergeResult = q.MergeResult
        }, q.OwnerSerno, q.StartDate, q.EndDate, ct);

    private async Task<ActionResult<OsosResult>> Run(string screen, string method, object parameters,
        long? serno, DateTime? start, DateTime? end, CancellationToken ct)
    {
        try
        {
            var result = await _search.RunAndSaveAsync(Uid, screen, method, parameters, serno, start, end, ct);
            return result;
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
