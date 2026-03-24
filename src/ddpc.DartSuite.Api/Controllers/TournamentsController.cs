using ddpc.DartSuite.Application.Abstractions;
using ddpc.DartSuite.Application.Contracts.Tournaments;
using Microsoft.AspNetCore.Mvc;

namespace ddpc.DartSuite.Api.Controllers;

[ApiController]
[Route("api/tournaments")]
public sealed class TournamentsController(ITournamentManagementService tournamentService) : ControllerBase
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
        var tournament = await tournamentService.CreateTournamentAsync(request, cancellationToken);
        return Ok(tournament);
    }

    [HttpPut("{tournamentId:guid}")]
    public async Task<ActionResult<TournamentDto>> Update(Guid tournamentId, [FromBody] UpdateTournamentRequest request, CancellationToken cancellationToken)
    {
        if (request.Id != tournamentId)
            return BadRequest("Tournament id mismatch.");

        try
        {
            var tournament = await tournamentService.UpdateTournamentAsync(request, cancellationToken);
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
        var tournament = await tournamentService.SetLockedAsync(tournamentId, locked, cancellationToken);
        return tournament is null ? NotFound() : Ok(tournament);
    }

    // ─── Participants ───

    [HttpGet("{tournamentId:guid}/participants")]
    public async Task<ActionResult<IReadOnlyList<ParticipantDto>>> GetParticipants(Guid tournamentId, CancellationToken cancellationToken)
    {
        return Ok(await tournamentService.GetParticipantsAsync(tournamentId, cancellationToken));
    }

    [HttpPost("{tournamentId:guid}/participants")]
    public async Task<ActionResult<ParticipantDto>> AddParticipant(Guid tournamentId, [FromBody] AddParticipantRequest request, CancellationToken cancellationToken)
    {
        if (request.TournamentId != tournamentId)
            return BadRequest("Tournament id mismatch.");

        try
        {
            var participant = await tournamentService.AddParticipantAsync(request, cancellationToken);
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

    [HttpDelete("{tournamentId:guid}/participants/{participantId:guid}")]
    public async Task<IActionResult> RemoveParticipant(Guid tournamentId, Guid participantId, CancellationToken cancellationToken)
    {
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

        return Ok(await tournamentService.SaveRoundAsync(request, cancellationToken));
    }

    [HttpDelete("{tournamentId:guid}/rounds/{phase}/{roundNumber:int}")]
    public async Task<IActionResult> DeleteRound(Guid tournamentId, string phase, int roundNumber, CancellationToken cancellationToken)
    {
        var deleted = await tournamentService.DeleteRoundAsync(tournamentId, phase, roundNumber, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    // ─── Status ───

    [HttpPatch("{tournamentId:guid}/status")]
    public async Task<ActionResult<TournamentDto>> UpdateStatus(Guid tournamentId, [FromQuery] string status, CancellationToken cancellationToken)
    {
        var result = await tournamentService.UpdateStatusAsync(tournamentId, status, cancellationToken);
        return result is null ? NotFound() : Ok(result);
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

        return Ok(await tournamentService.CreateTeamAsync(request, cancellationToken));
    }

    [HttpDelete("{tournamentId:guid}/teams/{teamId:guid}")]
    public async Task<IActionResult> DeleteTeam(Guid tournamentId, Guid teamId, CancellationToken cancellationToken)
    {
        var deleted = await tournamentService.DeleteTeamAsync(tournamentId, teamId, cancellationToken);
        return deleted ? NoContent() : NotFound();
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

        return Ok(await tournamentService.SaveScoringCriteriaAsync(request, cancellationToken));
    }
}