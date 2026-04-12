using Microsoft.AspNetCore.SignalR;

namespace ddpc.DartSuite.Api.Hubs;

public sealed class BoardStatusHub : Hub
{
	public async Task JoinBoardGroup(string boardId)
	{
		await Groups.AddToGroupAsync(Context.ConnectionId, $"board-{boardId}");
	}

	public async Task LeaveBoardGroup(string boardId)
	{
		await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"board-{boardId}");
	}

	public async Task JoinTournamentBoardsGroup(string tournamentId)
	{
		await Groups.AddToGroupAsync(Context.ConnectionId, $"tournament-boards-{tournamentId}");
	}

	public async Task LeaveTournamentBoardsGroup(string tournamentId)
	{
		await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tournament-boards-{tournamentId}");
	}
}