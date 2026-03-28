using ddpc.DartSuite.Application.Contracts.Autodarts;
using ddpc.DartSuite.Application.Contracts.Boards;
using ddpc.DartSuite.Application.Contracts.Matches;
using ddpc.DartSuite.Application.Contracts.Tournaments;
using System.Net.Http.Json;
using System.Text.Json;

namespace ddpc.DartSuite.Web.Services;

public sealed class DartSuiteApiService
{
    private readonly HttpClient _httpClient;

    public DartSuiteApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    private static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        string message;

        try
        {
            using var doc = JsonDocument.Parse(body);
            message = doc.RootElement.TryGetProperty("message", out var el) ? el.GetString() ?? body : body;
        }
        catch
        {
            message = body;
        }

        throw new InvalidOperationException(string.IsNullOrWhiteSpace(message) ? "The request failed." : message);
    }

    public async Task<IReadOnlyList<BoardDto>> GetBoardsAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<BoardDto>>("api/boards", cancellationToken) ?? Array.Empty<BoardDto>();

    public async Task<BoardDto> AddBoardAsync(CreateBoardRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/boards", request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<BoardDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<BoardDto> UpdateBoardAsync(UpdateBoardRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/boards/{request.Id}", request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<BoardDto>(cancellationToken: cancellationToken))!;
    }

    public async Task DeleteBoardAsync(Guid boardId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/boards/{boardId}", cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
    }

    public async Task SendUpcomingMatchAsync(Guid boardId, Guid matchId, string matchLabel, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync(
            $"api/boards/{boardId}/current-match?matchId={matchId}&matchLabel={Uri.EscapeDataString(matchLabel)}",
            null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
    }

    public async Task ClearCurrentMatchAsync(Guid boardId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/boards/{boardId}/current-match", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<TournamentDto>> GetTournamentsAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<TournamentDto>>("api/tournaments", cancellationToken) ?? Array.Empty<TournamentDto>();

    public async Task<TournamentDto> CreateTournamentAsync(CreateTournamentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/tournaments", request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<TournamentDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<TournamentDto> UpdateTournamentAsync(UpdateTournamentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/tournaments/{request.Id}", request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<TournamentDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<TournamentDto> SetTournamentLockedAsync(Guid tournamentId, bool locked, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/tournaments/{tournamentId}/lock?locked={locked}", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<TournamentDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<IReadOnlyList<ParticipantDto>> GetParticipantsAsync(Guid tournamentId, CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<ParticipantDto>>($"api/tournaments/{tournamentId}/participants", cancellationToken) ?? Array.Empty<ParticipantDto>();

    public async Task<IReadOnlyList<ParticipantDto>> SearchParticipantsAsync(string query, CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<ParticipantDto>>($"api/tournaments/participants/search?q={Uri.EscapeDataString(query)}", cancellationToken) ?? Array.Empty<ParticipantDto>();

    public async Task<ParticipantDto> AddParticipantAsync(AddParticipantRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/tournaments/{request.TournamentId}/participants", request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<ParticipantDto>(cancellationToken: cancellationToken))!;
    }

    public async Task RemoveParticipantAsync(Guid tournamentId, Guid participantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/tournaments/{tournamentId}/participants/{participantId}", cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
    }

    public async Task<ParticipantDto> UpdateParticipantAsync(Guid tournamentId, UpdateParticipantRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/tournaments/{tournamentId}/participants/{request.ParticipantId}", request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<ParticipantDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<IReadOnlyList<TournamentRoundDto>> GetRoundsAsync(Guid tournamentId, CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<TournamentRoundDto>>($"api/tournaments/{tournamentId}/rounds", cancellationToken) ?? Array.Empty<TournamentRoundDto>();

    public async Task<TournamentRoundDto> SaveRoundAsync(Guid tournamentId, SaveTournamentRoundRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/tournaments/{tournamentId}/rounds", request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<TournamentRoundDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<bool> DeleteRoundAsync(Guid tournamentId, string phase, int roundNumber, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/tournaments/{tournamentId}/rounds/{phase}/{roundNumber}", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<TournamentDto?> UpdateTournamentStatusAsync(Guid tournamentId, string status, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/tournaments/{tournamentId}/status?status={Uri.EscapeDataString(status)}", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TournamentDto>(cancellationToken: cancellationToken);
    }

    public async Task DeleteTournamentAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/tournaments/{tournamentId}", cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<TeamDto>> GetTeamsAsync(Guid tournamentId, CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<TeamDto>>($"api/tournaments/{tournamentId}/teams", cancellationToken) ?? Array.Empty<TeamDto>();

    public async Task<TeamDto> CreateTeamAsync(Guid tournamentId, CreateTeamRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/tournaments/{tournamentId}/teams", request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<TeamDto>(cancellationToken: cancellationToken))!;
    }

    public async Task DeleteTeamAsync(Guid tournamentId, Guid teamId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/tournaments/{tournamentId}/teams/{teamId}", cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<ScoringCriterionDto>> GetScoringCriteriaAsync(Guid tournamentId, CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<ScoringCriterionDto>>($"api/tournaments/{tournamentId}/scoring", cancellationToken) ?? Array.Empty<ScoringCriterionDto>();

    public async Task SaveScoringCriteriaAsync(Guid tournamentId, SaveScoringCriteriaRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/tournaments/{tournamentId}/scoring", request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<MatchDto>> GetMatchesAsync(Guid tournamentId, CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<MatchDto>>($"api/matches/{tournamentId}", cancellationToken) ?? Array.Empty<MatchDto>();

    public async Task<IReadOnlyList<MatchDto>> GenerateMatchesAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/matches/{tournamentId}/generate", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<MatchDto>>(cancellationToken: cancellationToken)) ?? Array.Empty<MatchDto>();
    }

    public async Task<IReadOnlyList<MatchDto>> GenerateGroupMatchesAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/matches/{tournamentId}/generate-groups", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<MatchDto>>(cancellationToken: cancellationToken)) ?? Array.Empty<MatchDto>();
    }

    public async Task<IReadOnlyList<GroupStandingDto>> GetGroupStandingsAsync(Guid tournamentId, CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<GroupStandingDto>>($"api/matches/{tournamentId}/group-standings", cancellationToken) ?? Array.Empty<GroupStandingDto>();

    public async Task<IReadOnlyList<MatchDto>> GenerateScheduleAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/matches/{tournamentId}/generate-schedule", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<MatchDto>>(cancellationToken: cancellationToken)) ?? Array.Empty<MatchDto>();
    }

    public async Task SwapParticipantsAsync(Guid matchId, Guid participantId, Guid targetMatchId, Guid targetParticipantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/matches/{matchId}/swap?participantId={participantId}&targetMatchId={targetMatchId}&targetParticipantId={targetParticipantId}", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
    }

    public async Task AssignBoardToMatchAsync(Guid matchId, Guid boardId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/matches/{matchId}/board?boardId={boardId}", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
    }

    public async Task<MatchDto> UpdateMatchScheduleAsync(Guid matchId, DateTimeOffset? startTime, bool lockTime, Guid? boardId, bool lockBoard, CancellationToken cancellationToken = default)
    {
        var qs = $"lockTime={lockTime}&lockBoard={lockBoard}";
        if (startTime.HasValue) qs += $"&startTime={Uri.EscapeDataString(startTime.Value.ToString("o"))}";
        if (boardId.HasValue) qs += $"&boardId={boardId.Value}";
        var response = await _httpClient.PatchAsync($"api/matches/{matchId}/schedule?{qs}", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<MatchDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<MatchDto> ToggleMatchTimeLockAsync(Guid matchId, bool locked, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/matches/{matchId}/lock-time?locked={locked}", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<MatchDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<MatchDto> ToggleMatchBoardLockAsync(Guid matchId, bool locked, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/matches/{matchId}/lock-board?locked={locked}", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<MatchDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<MatchDto?> ReportResultAsync(ReportMatchResultRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/matches/result", request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<MatchDto>(cancellationToken: cancellationToken);
    }

    public async Task<MatchDto?> SyncMatchFromExternalAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/matches/{matchId}/sync-external", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<MatchDto>(cancellationToken: cancellationToken);
    }

    public async Task<MatchDto?> ResetMatchAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/matches/{matchId}/reset", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<MatchDto>(cancellationToken: cancellationToken);
    }

    public async Task<MatchDto?> UpdateMatchAsync(UpdateMatchRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/matches/{request.MatchId}", request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<MatchDto>(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<MatchDto>> BatchResetMatchesAsync(IReadOnlyList<Guid> matchIds, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/matches/batch-reset", matchIds, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<MatchDto>>(cancellationToken: cancellationToken)) ?? Array.Empty<MatchDto>();
    }

    public async Task<IReadOnlyList<MatchDto>> CleanupStaleMatchesAsync(Guid tournamentId, int staleMinutes = 120, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/matches/{tournamentId}/cleanup?staleMinutes={staleMinutes}", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<MatchDto>>(cancellationToken: cancellationToken)) ?? Array.Empty<MatchDto>();
    }

    public async Task<IReadOnlyList<MatchDto>> CheckExternalMatchesAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/matches/{tournamentId}/check-external", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<MatchDto>>(cancellationToken: cancellationToken)) ?? Array.Empty<MatchDto>();
    }

    public async Task<AutodartsLoginResponse> LoginAutodartsAsync(string usernameOrEmail, string password, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/autodarts/login",
            new { UsernameOrEmail = usernameOrEmail, Password = password },
            cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<AutodartsLoginResponse>(cancellationToken: cancellationToken))
            ?? new AutodartsLoginResponse(new AutodartsProfileDto(null, null, null, null), Array.Empty<AutodartsBoardInfoDto>());
    }

    public async Task<AutodartsLoginResponse> RefreshAutodartsAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/autodarts/refresh-login",
            new AutodartsRefreshLoginRequest(refreshToken),
            cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<AutodartsLoginResponse>(cancellationToken: cancellationToken))
            ?? new AutodartsLoginResponse(new AutodartsProfileDto(null, null, null, null), Array.Empty<AutodartsBoardInfoDto>());
    }

    public async Task<AutodartsOauthStartResponse> StartAutodartsOauthAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/autodarts/oauth/start", cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<AutodartsOauthStartResponse>(cancellationToken: cancellationToken))!;
    }

    public async Task<AutodartsLoginResponse> GetAutodartsSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/autodarts/oauth/session/{sessionId}", cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<AutodartsLoginResponse>(cancellationToken: cancellationToken))
            ?? new AutodartsLoginResponse(new AutodartsProfileDto(null, null, null, null), Array.Empty<AutodartsBoardInfoDto>());
    }

    public async Task<AutodartsSessionStatusDto> GetAutodartsStatusAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/autodarts/status", cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<AutodartsSessionStatusDto>(cancellationToken: cancellationToken))
            ?? new AutodartsSessionStatusDto(false, null, null);
    }

    public async Task<IReadOnlyList<AutodartsFriendDto>> GetAutodartsFriendsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/autodarts/friends", cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<AutodartsFriendDto>>(cancellationToken: cancellationToken))
            ?? Array.Empty<AutodartsFriendDto>();
    }

    public async Task<IReadOnlyList<AutodartsBoardInfoDto>> GetAutodartsBoardsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/autodarts/boards", cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<AutodartsBoardInfoDto>>(cancellationToken: cancellationToken))
            ?? Array.Empty<AutodartsBoardInfoDto>();
    }

    public async Task<IReadOnlyList<MatchListenerInfoDto>> GetMatchListenersAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<MatchListenerInfoDto>>("api/matches/listeners", cancellationToken) ?? Array.Empty<MatchListenerInfoDto>();

    public async Task EnsureMatchListenerAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/matches/{matchId}/listener", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
    }
}
