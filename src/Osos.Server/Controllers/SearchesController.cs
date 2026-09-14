using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Osos.Contracts;
using Osos.Server.Services;

namespace Osos.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/searches")]
public sealed class SearchesController : ControllerBase
{
    private readonly SearchService _search;
    public SearchesController(SearchService search) => _search = search;

    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public Task<PagedResult<SearchHistoryDto>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => _search.GetHistoryAsync(Uid, Math.Max(1, page), Math.Clamp(pageSize, 1, 200), ct);

    [HttpGet("{id:long}")]
    public async Task<ActionResult<SearchResultDto>> Get(long id, CancellationToken ct)
    {
        var snap = await _search.GetSnapshotAsync(Uid, id, ct);
        return snap is null ? NotFound() : snap;
    }

    [HttpPost("{id:long}/rerun")]
    public async Task<ActionResult<OsosResult>> Rerun(long id, CancellationToken ct)
    {
        try { return await _search.RerunAsync(Uid, id, ct); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
        => await _search.DeleteAsync(Uid, id, ct) ? NoContent() : NotFound();
}
