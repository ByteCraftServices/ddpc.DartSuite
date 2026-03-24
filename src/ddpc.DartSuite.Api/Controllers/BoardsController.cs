using ddpc.DartSuite.Application.Abstractions;
using ddpc.DartSuite.Application.Contracts.Boards;
using ddpc.DartSuite.Api.Hubs;
using ddpc.DartSuite.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ddpc.DartSuite.Api.Controllers;

[ApiController]
[Route("api/boards")]
public sealed class BoardsController(
    IBoardManagementService boardService,
    IMatchManagementService matchService,
    AutodartsMatchListenerService listenerService,
    IHubContext<BoardStatusHub> hubContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BoardDto>>> Get(CancellationToken cancellationToken)
    {
        return Ok(await boardService.GetBoardsAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<BoardDto>> Create([FromBody] CreateBoardRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var board = await boardService.CreateBoardAsync(request, cancellationToken);
            await hubContext.Clients.All.SendAsync("BoardAdded", board, cancellationToken);
            return Ok(board);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BoardDto>> Update(Guid id, [FromBody] UpdateBoardRequest request, CancellationToken cancellationToken)
    {
        if (request.Id != id) return BadRequest("Id mismatch.");
        var board = await boardService.UpdateBoardAsync(request, cancellationToken);
        return board is null ? NotFound() : Ok(board);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await boardService.DeleteBoardAsync(id, cancellationToken);
            if (deleted) await hubContext.Clients.All.SendAsync("BoardRemoved", id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<BoardDto>> UpdateStatus(Guid id, [FromQuery] string status, [FromQuery] string? externalMatchId, CancellationToken cancellationToken)
    {
        var board = await boardService.UpdateBoardStatusAsync(id, status, externalMatchId, cancellationToken);
        if (board is null) return NotFound();
        await hubContext.Clients.All.SendAsync("BoardStatusChanged", board, cancellationToken);

        // Auto-start listener: if externalMatchId was provided, use it directly;
        // otherwise check if the current match already has an ExternalMatchId
        if (board.CurrentMatchId.HasValue)
        {
            if (!string.IsNullOrEmpty(externalMatchId))
            {
                listenerService.EnsureListener(board.CurrentMatchId.Value, externalMatchId, board.Id);
            }
            else
            {
                var match = await matchService.GetMatchAsync(board.CurrentMatchId.Value, cancellationToken);
                if (match is not null && !string.IsNullOrEmpty(match.ExternalMatchId) && match.FinishedUtc is null)
                {
                    listenerService.EnsureListener(match.Id, match.ExternalMatchId, board.Id);
                }
            }
        }

        return Ok(board);
    }

    [HttpPatch("{id:guid}/managed")]
    public async Task<ActionResult<BoardDto>> SetManagedMode(Guid id, [FromQuery] string mode, [FromQuery] Guid? tournamentId, CancellationToken cancellationToken)
    {
        var board = await boardService.SetManagedModeAsync(id, mode, tournamentId, cancellationToken);
        if (board is null) return NotFound();
        await hubContext.Clients.All.SendAsync("BoardManagedModeChanged", board, cancellationToken);
        return Ok(board);
    }

    [HttpPatch("{id:guid}/current-match")]
    public async Task<ActionResult<BoardDto>> SetCurrentMatch(Guid id, [FromQuery] Guid? matchId, [FromQuery] string? matchLabel, CancellationToken cancellationToken)
    {
        var board = await boardService.SetCurrentMatchAsync(id, matchId, matchLabel, cancellationToken);
        if (board is null) return NotFound();
        await hubContext.Clients.All.SendAsync("BoardCurrentMatchChanged", board, cancellationToken);
        return Ok(board);
    }

    [HttpPatch("{id:guid}/heartbeat")]
    public async Task<IActionResult> Heartbeat(Guid id, CancellationToken cancellationToken)
    {
        var result = await boardService.HeartbeatAsync(id, cancellationToken);
        return result ? Ok() : NotFound();
    }
}