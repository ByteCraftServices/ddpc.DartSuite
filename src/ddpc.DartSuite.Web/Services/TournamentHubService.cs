using Microsoft.AspNetCore.SignalR.Client;

namespace ddpc.DartSuite.Web.Services;

public sealed class TournamentHubService : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly string _hubUrl;

    public event Func<string, Task>? OnMatchUpdated;
    public event Func<string, Task>? OnBoardsUpdated;
    public event Func<string, Task>? OnParticipantsUpdated;
    public event Func<string, Task>? OnTournamentUpdated;
    public event Func<string, Task>? OnScheduleUpdated;
    public event Func<Task>? OnReconnected;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public TournamentHubService(IConfiguration configuration)
    {
        var apiBase = configuration["Api:BaseUrl"]?.TrimEnd('/') ?? throw new InvalidOperationException("Api:BaseUrl not configured");
        _hubUrl = $"{apiBase}/hubs/tournaments";
    }

    public async Task StartAsync()
    {
        if (_connection is not null) return;

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)])
            .Build();

        _connection.On<string>("MatchUpdated", async tournamentId =>
        {
            if (OnMatchUpdated is not null) await OnMatchUpdated.Invoke(tournamentId);
        });

        _connection.On<string>("BoardsUpdated", async tournamentId =>
        {
            if (OnBoardsUpdated is not null) await OnBoardsUpdated.Invoke(tournamentId);
        });

        _connection.On<string>("ParticipantsUpdated", async tournamentId =>
        {
            if (OnParticipantsUpdated is not null) await OnParticipantsUpdated.Invoke(tournamentId);
        });

        _connection.On<string>("TournamentUpdated", async tournamentId =>
        {
            if (OnTournamentUpdated is not null) await OnTournamentUpdated.Invoke(tournamentId);
        });

        _connection.On<string>("ScheduleUpdated", async tournamentId =>
        {
            if (OnScheduleUpdated is not null) await OnScheduleUpdated.Invoke(tournamentId);
        });

        _connection.Reconnected += async _ =>
        {
            if (OnReconnected is not null) await OnReconnected.Invoke();
        };

        await _connection.StartAsync();
    }

    public async Task JoinTournamentAsync(string tournamentId)
    {
        if (_connection?.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("JoinTournament", tournamentId);
    }

    public async Task LeaveTournamentAsync(string tournamentId)
    {
        if (_connection?.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("LeaveTournament", tournamentId);
    }

    public async Task JoinMatchAsync(string matchId)
    {
        if (_connection?.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("JoinMatch", matchId);
    }

    public async Task LeaveMatchAsync(string matchId)
    {
        if (_connection?.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("LeaveMatch", matchId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
