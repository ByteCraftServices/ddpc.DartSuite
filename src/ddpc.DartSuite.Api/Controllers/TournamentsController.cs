using ddpc.DartSuite.Application.Abstractions;
using ddpc.DartSuite.Application.Contracts.Notifications;
using ddpc.DartSuite.Application.Contracts.Tournaments;
using ddpc.DartSuite.Api.Hubs;
using ddpc.DartSuite.Api.Services;
using ddpc.DartSuite.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace ddpc.DartSuite.Api.Controllers;

[ApiController]
[Route("api/tournaments")]
public sealed class TournamentsController(
    ITournamentManagementService tournamentService,
    IDiscordWebhookService discordWebhookService,
    TournamentAuthorizationService tournamentAuthorization,
    IHubContext<TournamentHub> tournamentHub,
    IOptions<VapidOptions> vapidOptions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TournamentDto>>> Get([FromQuery] string? host, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(host))
            return Ok(await tournamentService.GetTournamentsByHostAsync(host, cancellationToken));

        return Ok(await tournamentService.GetTournamentsAsync(cancellationToken));
    }

    [HttpGet("{tournamentId:guid}")]
    public async Task<ActionResult<TournamentDto>> GetById(Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await tournamentService.GetTournamentAsync(tournamentId, cancellationToken);
        return tournament is null ? NotFound() : Ok(tournament);
    }

    [HttpGet("by-code/{code}")]
    public async Task<ActionResult<TournamentDto>> GetByCode(string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 3)
            return BadRequest("Code must be exactly 3 characters.");

        var tournament = await tournamentService.GetTournamentByCodeAsync(code, cancellationToken);
        return tournament is null ? NotFound() : Ok(tournament);
    }

    [HttpPost]
    public async Task<ActionResult<TournamentDto>> Create([FromBody] CreateTournamentRequest request, CancellationToken cancellationToken)
    {
        var ownAccountAccess = tournamentAuthorization.EnsureSelfOrIntegration(HttpContext, request.OrganizerAccount);
        var denied = ToDeniedResult(ownAccountAccess);
        if (denied is not null) return denied;

        var tournament = await tournamentService.CreateTournamentAsync(request, cancellationToken);
        return Ok(tournament);
    }

    [HttpPut("{tournamentId:guid}")]
    public async Task<ActionResult<TournamentDto>> Update(Guid tournamentId, [FromBody] UpdateTournamentRequest request, CancellationToken cancellationToken)
    {
        if (request.Id != tournamentId)
            return BadRequest("Tournament id mismatch.");

        var denied = await RequireManagerAccessAsync(tournamentId, cancellationToken);
        if (denied is not null) return denied;

        try
        {
            var tournament = await tournamentService.UpdateTournamentAsync(request, cancellationToken);
            if (tournament is not null)
                await NotifyTournamentAsync(tournamentId, "TournamentUpdated");
            return tournament is null ? NotFound() : Ok(tournament);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPatch("{tournamentId:guid}/lock")]
    public async Task<ActionResult<TournamentDto>> SetLocked(Guid tournamentId, [FromQuery] bool locked, CancellationToken cancellationToken)
    {
        var denied = await RequireManagerAccessAsync(tournamentId, cancellationToken);
        if (denied is not null) return denied;

        var tournament = await tournamentService.SetLockedAsync(tournamentId, locked, cancellationToken);
        return tournament is null ? NotFound() : Ok(tournament);
    }

    // ─── Participants ───

    [HttpGet("{tournamentId:guid}/participants")]
    public async Task<ActionResult<IReadOnlyList<ParticipantDto>>> GetParticipants(Guid tournamentId, CancellationToken cancellationToken)
    {
        return Ok(await tournamentService.GetParticipantsAsync(tournamentId, cancellationToken));
    }

    [HttpGet("participants/search")]
    public async Task<ActionResult<IReadOnlyList<ParticipantDto>>> SearchParticipants([FromQuery] string q, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(Array.Empty<ParticipantDto>());
        return Ok(await tournamentService.SearchParticipantsAsync(q, cancellationToken));
    }

    [HttpPost("{tournamentId:guid}/participants")]
    public async Task<ActionResult<ParticipantDto>> AddParticipant(Guid tournamentId, [FromBody] AddParticipantRequest request, CancellationToken cancellationToken)
    {
        if (request.TournamentId != tournamentId)
            return BadRequest("Tournament id mismatch.");

        var denied = await RequireManagerAccessAsync(tournamentId, cancellationToken);
        if (denied is not null) return denied;

        try
        {
            var participant = await tournamentService.AddParticipantAsync(request, cancellationToken);
            await NotifyTournamentAsync(tournamentId, "ParticipantsUpdated");
            return Ok(participant);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{tournamentId:guid}/participants/{participantId:guid}")]
    public async Task<ActionResult<ParticipantDto>> UpdateParticipant(Guid tournamentId, Guid participantId, [FromBody] UpdateParticipantRequest request, CancellationToken cancellationToken)
    {
        if (request.TournamentId != tournamentId || request.ParticipantId != participantId)
            return BadRequest("Id mismatch.");

        var denied = await RequireManagerAccessAsync(tournamentId, cancellationToken);
        if (denied is not null) return denied;

        try
        {
            var result = await tournamentService.UpdateParticipantAsync(request, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPatch("{tournamentId:guid}/participants/{participantId:guid}/notification-preference")]
    public async Task<ActionResult<ParticipantDto>> UpdateParticipantNotificationPreference(
        Guid tournamentId, Guid participantId,
        [FromQuery] string preference,
        CancellationToken cancellationToken)
    {
        var result = await tournamentService.UpdateParticipantNotificationPreferenceAsync(tournamentId, participantId, preference, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{tournamentId:guid}/participants/{participantId:guid}")]
    public async Task<IActionResult> RemoveParticipant(Guid tournamentId, Guid participantId, CancellationToken cancellationToken)
    {
        var denied = await RequireManagerAccessAsync(tournamentId, cancellationToken);
        if (denied is not null) return denied;

        try
        {
            var removed = await tournamentService.RemoveParticipantAsync(tournamentId, participantId, cancellationToken);
            return removed ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{tournamentId:guid}/participants/assign-seed-pots")]
    public async Task<ActionResult<IReadOnlyList<ParticipantDto>>> AssignSeedPots(Guid tournamentId, CancellationToken cancellationToken)
    {
        var denied = await RequireManagerAccessAsync(tournamentId, cancellationToken);
        if (denied is not null) return denied;

        try
        {
            return Ok(await tournamentService.AssignSeedPotsAsync(tournamentId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ─── Rounds ───

    [HttpGet("{tournamentId:guid}/rounds")]
    public async Task<ActionResult<IReadOnlyList<TournamentRoundDto>>> GetRounds(Guid tournamentId, CancellationToken cancellationToken)
    {
        return Ok(await tournamentService.GetRoundsAsync(tournamentId, cancellationToken));
    }

    [HttpPost("{tournamentId:guid}/rounds")]
    public async Task<ActionResult<TournamentRoundDto>> SaveRound(Guid tournamentId, [FromBody] SaveTournamentRoundRequest request, CancellationToken cancellationToken)
    {
        if (request.TournamentId != tournamentId)
            return BadRequest("Tournament id mismatch.");

        var denied = await RequireManagerAccessAsync(tournamentId, cancellationToken);
        if (denied is not null) return denied;

        try
        {
            return Ok(await tournamentService.SaveRoundAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("{tournamentId:guid}/rounds/{phase}/{roundNumber:int}")]
    public async Task<IActionResult> DeleteRound(Guid tournamentId, string phase, int roundNumber, CancellationToken cancellationToken)
    {
        var denied = await RequireManagerAccessAsync(tournamentId, cancellationToken);
        if (denied is not null) return denied;

        try
        {
            var deleted = await tournamentService.DeleteRoundAsync(tournamentId, phase, roundNumber, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ─── Status ───

    [HttpPatch("{tournamentId:guid}/status")]
    public async Task<ActionResult<TournamentDto>> UpdateStatus(Guid tournamentId, [FromQuery] string status, CancellationToken cancellationToken)
    {
        var denied = await RequireManagerAccessAsync(tournamentId, cancellationToken);
        if (denied is not null) return denied;

        try
        {
            var result = await tournamentService.UpdateStatusAsync(tournamentId, status, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("{tournamentId:guid}")]
    public async Task<IActionResult> DeleteTournament(Guid tournamentId, CancellationToken cancellationToken)
    {
        var denied = await RequireManagerAccessAsync(tournamentId, cancellationToken);
        if (denied is not null) return denied;

        try
        {
            var deleted = await tournamentService.DeleteTournamentAsync(tournamentId, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ─── Teams ───

    [HttpGet("{tournamentId:guid}/teams")]
    public async Task<ActionResult<IReadOnlyList<TeamDto>>> GetTeams(Guid tournamentId, CancellationToken cancellationToken)
    {
        return Ok(await tournamentService.GetTeamsAsync(tournamentId, cancellationToken));
    }

    [HttpPost("{tournamentId:guid}/teams")]
    public async Task<ActionResult<TeamDto>> CreateTeam(Guid tournamentId, [FromBody] CreateTeamRequest request, CancellationToken cancellationToken)
    {
        if (request.TournamentId != tournamentId)
            return BadRequest("Tournament id mismatch.");

        var denied = await RequireManagerAccessAsync(tournamentId, cancellationToken);
        if (denied is not null) return denied;

        try
        {
            return Ok(await tournamentService.CreateTeamAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{tournamentId:guid}/teams/save")]
    public async Task<ActionResult<IReadOnlyList<TeamDto>>> SaveTeams(Guid tournamentId, [FromBody] SaveTeamsRequest request, CancellationToken cancellationToken)
    {
        if (request.TournamentId != tournamentId)
            return BadRequest("Tournament id mismatch.");

        var denied = await RequireManagerAccessAsync(tournamentId, cancellationToken);
        if (denied is not null) return denied;

        try
        {
            return Ok(await tournamentService.SaveTeamsAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("{tournamentId:guid}/teams/{teamId:guid}")]
    public async Task<IActionResult> DeleteTeam(Guid tournamentId, Guid teamId, CancellationToken cancellationToken)
    {
        var denied = await RequireManagerAccessAsync(tournamentId, cancellationToken);
        if (denied is not null) return denied;

        try
        {
            var deleted = await tournamentService.DeleteTeamAsync(tournamentId, teamId, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ─── Scoring Criteria ───

    [HttpGet("{tournamentId:guid}/scoring")]
    public async Task<ActionResult<IReadOnlyList<ScoringCriterionDto>>> GetScoringCriteria(Guid tournamentId, CancellationToken cancellationToken)
    {
        return Ok(await tournamentService.GetScoringCriteriaAsync(tournamentId, cancellationToken));
    }

    [HttpPost("{tournamentId:guid}/scoring")]
    public async Task<ActionResult<IReadOnlyList<ScoringCriterionDto>>> SaveScoringCriteria(Guid tournamentId, [FromBody] SaveScoringCriteriaRequest request, CancellationToken cancellationToken)
    {
        if (request.TournamentId != tournamentId)
            return BadRequest("Tournament id mismatch.");

        var denied = await RequireManagerAccessAsync(tournamentId, cancellationToken);
        if (denied is not null) return denied;

        try
        {
            return Ok(await tournamentService.SaveScoringCriteriaAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ─── Notifications (#14) ───

    [HttpGet("{tournamentId:guid}/notifications/{userAccountName}")]
    public async Task<ActionResult<IReadOnlyList<NotificationSubscriptionDto>>> GetNotifications(Guid tournamentId, string userAccountName, CancellationToken cancellationToken)
    {
        var denied = await RequireSelfOrManagerAccessAsync(tournamentId, userAccountName, cancellationToken);
        if (denied is not null) return denied;

        return Ok(await tournamentService.GetNotificationSubscriptionsAsync(tournamentId, userAccountName, cancellationToken));
    }

    [HttpPost("{tournamentId:guid}/notifications")]
    public async Task<ActionResult<NotificationSubscriptionDto>> SubscribeNotifications(Guid tournamentId, [FromBody] CreateNotificationSubscriptionRequest request, CancellationToken cancellationToken)
    {
        if (request.TournamentId != tournamentId)
            return BadRequest("Tournament id mismatch.");

        var denied = await RequireSelfOrManagerAccessAsync(tournamentId, request.UserAccountName, cancellationToken);
        if (denied is not null) return denied;

        return Ok(await tournamentService.SubscribeNotificationsAsync(request, cancellationToken));
    }

    [HttpDelete("notifications/{subscriptionId:guid}")]
    public async Task<IActionResult> UnsubscribeNotifications(Guid subscriptionId, CancellationToken cancellationToken)
    {
        var result = await tournamentService.UnsubscribeNotificationsAsync(subscriptionId, cancellationToken);
        return result ? NoContent() : NotFound();
    }

    // ─── Discord Webhook (#14) ───

    [HttpPost("{tournamentId:guid}/webhook/test")]
    public async Task<IActionResult> TestWebhook(Guid tournamentId, CancellationToken cancellationToken)
    {
        var denied = await RequireManagerAccessAsync(tournamentId, cancellationToken);
        if (denied is not null) return denied;

        var tournament = await tournamentService.GetTournamentAsync(tournamentId, cancellationToken);
        if (tournament is null) return NotFound();
        if (string.IsNullOrEmpty(tournament.DiscordWebhookUrl))
            return BadRequest(new { message = "No Discord webhook URL configured for this tournament." });

        var success = await discordWebhookService.TestWebhookAsync(tournament.DiscordWebhookUrl, cancellationToken);
        return success ? Ok(new { message = "Webhook-Test erfolgreich!" }) : BadRequest(new { message = "Webhook-Test fehlgeschlagen." });
    }

    // ─── View Preferences (#15) ───

    [HttpGet("preferences/{userAccountName}/{viewContext}")]
    public async Task<ActionResult<UserViewPreferenceDto>> GetViewPreference(string userAccountName, string viewContext, CancellationToken cancellationToken)
    {
        var denied = ToDeniedResult(tournamentAuthorization.EnsureSelfOrIntegration(HttpContext, userAccountName));
        if (denied is not null) return denied;

        var pref = await tournamentService.GetUserViewPreferenceAsync(userAccountName, viewContext, cancellationToken);
        return pref is null ? NotFound() : Ok(pref);
    }

    [HttpPut("preferences/{userAccountName}/{viewContext}")]
    public async Task<ActionResult<UserViewPreferenceDto>> SaveViewPreference(string userAccountName, string viewContext, [FromBody] string settingsJson, CancellationToken cancellationToken)
    {
        var denied = ToDeniedResult(tournamentAuthorization.EnsureSelfOrIntegration(HttpContext, userAccountName));
        if (denied is not null) return denied;

        return Ok(await tournamentService.SaveUserViewPreferenceAsync(userAccountName, viewContext, settingsJson, cancellationToken));
    }

    // ─── VAPID Public Key ───

    [HttpGet("vapid-public-key")]
    public ActionResult<string> GetVapidPublicKey()
    {
        var key = vapidOptions.Value.PublicKey;
        return string.IsNullOrEmpty(key) ? NotFound() : Ok(key);
    }

    private Task NotifyTournamentAsync(Guid tournamentId, string method)
    {
        return tournamentHub.Clients.Group($"tournament-{tournamentId}").SendAsync(method, tournamentId.ToString());
    }

    private async Task<ActionResult?> RequireManagerAccessAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        return ToDeniedResult(await tournamentAuthorization.EnsureManagerOrIntegrationAsync(HttpContext, tournamentId, cancellationToken));
    }

    private async Task<ActionResult?> RequireSelfOrManagerAccessAsync(Guid tournamentId, string userAccountName, CancellationToken cancellationToken)
    {
        return ToDeniedResult(await tournamentAuthorization.EnsureSelfOrManagerOrIntegrationAsync(HttpContext, tournamentId, userAccountName, cancellationToken));
    }

    private ActionResult? ToDeniedResult(AccessCheckResult access)
    {
        return access.Allowed
            ? null
            : StatusCode(access.StatusCode, new { message = access.Message });
    }
}