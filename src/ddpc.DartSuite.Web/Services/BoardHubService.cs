using ddpc.DartSuite.Application.Contracts.Boards;
using ddpc.DartSuite.Application.Contracts.Matches;
using Microsoft.AspNetCore.SignalR.Client;

namespace ddpc.DartSuite.Web.Services;

public sealed class BoardHubService : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly string _hubUrl;

    public event Func<bool, Task>? OnConnectionChanged;
    public event Func<BoardDto, Task>? OnBoardAdded;
    public event Func<Guid, Task>? OnBoardRemoved;
    public event Func<BoardDto, Task>? OnBoardStatusChanged;
    public event Func<BoardDto, Task>? OnBoardConnectionChanged;
    public event Func<BoardDto, Task>? OnBoardExtensionStatusChanged;
    public event Func<BoardDto, Task>? OnBoardCurrentMatchChanged;
    public event Func<BoardDto, Task>? OnBoardManagedModeChanged;
    public event Func<MatchDto, Task>? OnMatchUpdated;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public BoardHubService(IConfiguration configuration)
    {
        var apiBase = configuration["Api:BaseUrl"]?.TrimEnd('/') ?? throw new InvalidOperationException("Api:BaseUrl not configured");
        _hubUrl = $"{apiBase}/hubs/boards";
    }

    public async Task StartAsync()
    {
        if (_connection is not null)
            return;

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)])
            .Build();

        _connection.On<BoardDto>("BoardAdded", async payload =>
        {
            if (OnBoardAdded is not null)
                await OnBoardAdded.Invoke(payload);
        });

        _connection.On<Guid>("BoardRemoved", async boardId =>
        {
            if (OnBoardRemoved is not null)
                await OnBoardRemoved.Invoke(boardId);
        });

        _connection.On<BoardDto>("BoardStatusChanged", async payload =>
        {
            if (OnBoardStatusChanged is not null)
                await OnBoardStatusChanged.Invoke(payload);
        });

        _connection.On<BoardDto>("BoardConnectionChanged", async payload =>
        {
            if (OnBoardConnectionChanged is not null)
                await OnBoardConnectionChanged.Invoke(payload);
        });

        _connection.On<BoardDto>("BoardExtensionStatusChanged", async payload =>
        {
            if (OnBoardExtensionStatusChanged is not null)
                await OnBoardExtensionStatusChanged.Invoke(payload);
        });

        _connection.On<BoardDto>("BoardCurrentMatchChanged", async payload =>
        {
            if (OnBoardCurrentMatchChanged is not null)
                await OnBoardCurrentMatchChanged.Invoke(payload);
        });

        _connection.On<BoardDto>("BoardManagedModeChanged", async payload =>
        {
            if (OnBoardManagedModeChanged is not null)
                await OnBoardManagedModeChanged.Invoke(payload);
        });

        _connection.On<MatchDto>("MatchUpdated", async payload =>
        {
            if (OnMatchUpdated is not null)
                await OnMatchUpdated.Invoke(payload);
        });

        _connection.Reconnecting += async _ =>
        {
            if (OnConnectionChanged is not null)
                await OnConnectionChanged.Invoke(false);
        };

        _connection.Reconnected += async _ =>
        {
            if (OnConnectionChanged is not null)
                await OnConnectionChanged.Invoke(true);
        };

        _connection.Closed += async _ =>
        {
            if (OnConnectionChanged is not null)
                await OnConnectionChanged.Invoke(false);
        };

        await _connection.StartAsync();

        if (OnConnectionChanged is not null)
            await OnConnectionChanged.Invoke(true);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is null)
            return;

        await _connection.DisposeAsync();
        _connection = null;
    }
}
