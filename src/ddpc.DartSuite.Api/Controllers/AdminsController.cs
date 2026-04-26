using ddpc.DartSuite.Application.Contracts.Admins;
using ddpc.DartSuite.Api.Services;
using ddpc.DartSuite.Domain.Entities;
using ddpc.DartSuite.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ddpc.DartSuite.Api.Controllers;

[ApiController]
[Route("api/admins")]
public sealed class AdminsController(
    DartSuiteDbContext dbContext,
    TournamentAuthorizationService tournamentAuthorization) : ControllerBase
{
    private static AdminDto ToDto(Admin a) =>
        new(a.Id, a.AccountName, a.ValidFromDate, a.ValidToDate);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminDto>>> GetAll(CancellationToken cancellationToken)
    {
        var denied = ToDeniedResult(await tournamentAuthorization.EnsureAdminAsync(HttpContext, cancellationToken));
        if (denied is not null) return denied;

        var admins = await dbContext.Admins
            .AsNoTracking()
            .OrderBy(a => a.AccountName)
            .Select(a => new AdminDto(a.Id, a.AccountName, a.ValidFromDate, a.ValidToDate))
            .ToListAsync(cancellationToken);

        return Ok(admins);
    }

    [HttpPost]
    public async Task<ActionResult<AdminDto>> Create([FromBody] CreateAdminRequest request, CancellationToken cancellationToken)
    {
        var denied = ToDeniedResult(await tournamentAuthorization.EnsureAdminAsync(HttpContext, cancellationToken));
        if (denied is not null) return denied;

        if (string.IsNullOrWhiteSpace(request.AccountName))
            return BadRequest(new { message = "AccountName darf nicht leer sein." });

        var exists = await dbContext.Admins
            .AnyAsync(a => a.AccountName.ToLower() == request.AccountName.ToLower().Trim(), cancellationToken);
        if (exists)
            return Conflict(new { message = $"Ein Admin-Eintrag für '{request.AccountName}' existiert bereits." });

        var admin = new Admin
        {
            AccountName = request.AccountName.Trim(),
            ValidFromDate = request.ValidFromDate,
            ValidToDate = request.ValidToDate
        };
        dbContext.Admins.Add(admin);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(admin));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminDto>> Update(Guid id, [FromBody] UpdateAdminRequest request, CancellationToken cancellationToken)
    {
        if (request.Id != id) return BadRequest("Id mismatch.");

        var denied = ToDeniedResult(await tournamentAuthorization.EnsureAdminAsync(HttpContext, cancellationToken));
        if (denied is not null) return denied;

        var admin = await dbContext.Admins.FindAsync([id], cancellationToken);
        if (admin is null) return NotFound();

        if (string.IsNullOrWhiteSpace(request.AccountName))
            return BadRequest(new { message = "AccountName darf nicht leer sein." });

        var duplicate = await dbContext.Admins
            .AnyAsync(a => a.Id != id && a.AccountName.ToLower() == request.AccountName.ToLower().Trim(), cancellationToken);
        if (duplicate)
            return Conflict(new { message = $"Ein Admin-Eintrag für '{request.AccountName}' existiert bereits." });

        admin.AccountName = request.AccountName.Trim();
        admin.ValidFromDate = request.ValidFromDate;
        admin.ValidToDate = request.ValidToDate;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(admin));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var denied = ToDeniedResult(await tournamentAuthorization.EnsureAdminAsync(HttpContext, cancellationToken));
        if (denied is not null) return denied;

        var admin = await dbContext.Admins.FindAsync([id], cancellationToken);
        if (admin is null) return NotFound();

        dbContext.Admins.Remove(admin);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("check")]
    public async Task<ActionResult<bool>> CheckCurrentUserIsAdmin(CancellationToken cancellationToken)
    {
        var result = await tournamentAuthorization.EnsureAdminAsync(HttpContext, cancellationToken);
        return Ok(result.Allowed);
    }

    private static ActionResult? ToDeniedResult(AccessCheckResult result)
    {
        if (result.Allowed) return null;
        return result.StatusCode switch
        {
            StatusCodes.Status401Unauthorized => new UnauthorizedObjectResult(new { message = result.Message }),
            StatusCodes.Status403Forbidden => new ObjectResult(new { message = result.Message }) { StatusCode = 403 },
            _ => new ObjectResult(new { message = result.Message }) { StatusCode = result.StatusCode }
        };
    }
}
