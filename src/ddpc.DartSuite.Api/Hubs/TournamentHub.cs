using Microsoft.AspNetCore.SignalR;

namespace ddpc.DartSuite.Api.Hubs;

public sealed class TournamentHub : Hub
{
    public async Task JoinTournament(string tournamentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tournament-{tournamentId}");
    }

    public async Task LeaveTournament(string tournamentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tournament-{tournamentId}");
    }

    public async Task JoinMatch(string matchId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"match-{matchId}");
    }

    public async Task LeaveMatch(string matchId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"match-{matchId}");
    }

    // Client method names (for reference):
    // "MatchUpdated" - a match was created/updated/finished
    // "BoardsUpdated" - board state changed
    // "ParticipantsUpdated" - participant list changed
    // "TournamentUpdated" - tournament settings changed
    // "ScheduleUpdated" - schedule recalculated
}
