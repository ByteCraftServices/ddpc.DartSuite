using ddpc.DartSuite.Application.Contracts.Admins;
using ddpc.DartSuite.Application.Contracts.Autodarts;
using ddpc.DartSuite.Application.Contracts.Boards;
using ddpc.DartSuite.Application.Contracts.Matches;
using ddpc.DartSuite.Application.Contracts.Notifications;
using ddpc.DartSuite.Application.Contracts.Tournaments;
using Microsoft.AspNetCore.Components;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ddpc.DartSuite.Web.Services;

public sealed class DartSuiteApiService
{
    private readonly HttpClient _httpClient;
    private readonly NavigationManager _navigation;

    public DartSuiteApiService(HttpClient httpClient, NavigationManager navigation)
    {
        _httpClient = httpClient;
        _navigation = navigation;
    }

    private void RedirectToLogin()
    {
        var relativePath = _navigation.ToBaseRelativePath(_navigation.Uri);

        if (relativePath.StartsWith("login", StringComparison.OrdinalIgnoreCase))
            return;

        var returnUrl = string.IsNullOrWhiteSpace(relativePath) ? "/" : $"/{relativePath}";
        var target = $"/login?returnUrl={Uri.EscapeDataString(returnUrl)}";
        _navigation.NavigateTo(target, replace: true);
    }

    private async Task<T?> GetFromJsonOrDefaultAsync<T>(string requestUri, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<T>(requestUri, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Read requests can be polled in the background; avoid hard navigation loops on expired sessions.
            return default;
        }
        catch (HttpRequestException)
        {
            // Keep UI interactive when read endpoints temporarily fail (e.g. 500/network hiccups).
            return default;
        }
        catch (TaskCanceledException)
        {
            return default;
        }
        catch (NotSupportedException)
        {
            return default;
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        if (response.IsSuccessStatusCode) return;

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var isReadRequest = response.RequestMessage?.Method == HttpMethod.Get;
            if (!isReadRequest)
                RedirectToLogin();

            throw new InvalidOperationException("Nicht authentifiziert. Bitte erneut anmelden.");
        }

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
        => await GetFromJsonOrDefaultAsync<IReadOnlyList<BoardDto>>("api/boards", cancellationToken) ?? Array.Empty<BoardDto>();

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

    public async Task<BoardExtensionSyncRequestAcceptedDto> RequestBoardExtensionSyncAsync(Guid boardId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/boards/{boardId}/extension-sync/request", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<BoardExtensionSyncRequestAcceptedDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<BoardExtensionSyncDebugDto?> GetLastBoardExtensionSyncDebugAsync(Guid boardId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/boards/{boardId}/extension-sync/last", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<BoardExtensionSyncDebugDto>(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<TournamentDto>> GetTournamentsAsync(CancellationToken cancellationToken = default)
        => await GetFromJsonOrDefaultAsync<IReadOnlyList<TournamentDto>>("api/tournaments", cancellationToken) ?? Array.Empty<TournamentDto>();

    public async Task<TournamentDto?> GetTournamentAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/tournaments/{tournamentId}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TournamentDto>(cancellationToken: cancellationToken);
    }

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
        => await GetFromJsonOrDefaultAsync<IReadOnlyList<ParticipantDto>>($"api/tournaments/{tournamentId}/participants", cancellationToken) ?? Array.Empty<ParticipantDto>();

    public async Task<IReadOnlyList<ParticipantDto>> SearchParticipantsAsync(string query, CancellationToken cancellationToken = default)
        => await GetFromJsonOrDefaultAsync<IReadOnlyList<ParticipantDto>>($"api/tournaments/participants/search?q={Uri.EscapeDataString(query)}", cancellationToken) ?? Array.Empty<ParticipantDto>();

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

    public async Task<IReadOnlyList<ParticipantDto>> AssignSeedPotsAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/tournaments/{tournamentId}/participants/assign-seed-pots", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<ParticipantDto>>(cancellationToken: cancellationToken)) ?? Array.Empty<ParticipantDto>();
    }

    public async Task<IReadOnlyList<TournamentRoundDto>> GetRoundsAsync(Guid tournamentId, CancellationToken cancellationToken = default)
        => await GetFromJsonOrDefaultAsync<IReadOnlyList<TournamentRoundDto>>($"api/tournaments/{tournamentId}/rounds", cancellationToken) ?? Array.Empty<TournamentRoundDto>();

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
        => await GetFromJsonOrDefaultAsync<IReadOnlyList<TeamDto>>($"api/tournaments/{tournamentId}/teams", cancellationToken) ?? Array.Empty<TeamDto>();

    public async Task<TeamDto> CreateTeamAsync(Guid tournamentId, CreateTeamRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/tournaments/{tournamentId}/teams", request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<TeamDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<IReadOnlyList<TeamDto>> SaveTeamsAsync(Guid tournamentId, SaveTeamsRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/tournaments/{tournamentId}/teams/save", request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<TeamDto>>(cancellationToken: cancellationToken)) ?? Array.Empty<TeamDto>();
    }

    public async Task DeleteTeamAsync(Guid tournamentId, Guid teamId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/tournaments/{tournamentId}/teams/{teamId}", cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<ScoringCriterionDto>> GetScoringCriteriaAsync(Guid tournamentId, CancellationToken cancellationToken = default)
        => await GetFromJsonOrDefaultAsync<IReadOnlyList<ScoringCriterionDto>>($"api/tournaments/{tournamentId}/scoring", cancellationToken) ?? Array.Empty<ScoringCriterionDto>();

    public async Task SaveScoringCriteriaAsync(Guid tournamentId, SaveScoringCriteriaRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/tournaments/{tournamentId}/scoring", request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<MatchDto>> GetMatchesAsync(Guid tournamentId, CancellationToken cancellationToken = default)
        => await GetFromJsonOrDefaultAsync<IReadOnlyList<MatchDto>>($"api/matches/{tournamentId}", cancellationToken) ?? Array.Empty<MatchDto>();

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
        => await GetFromJsonOrDefaultAsync<IReadOnlyList<GroupStandingDto>>($"api/matches/{tournamentId}/group-standings", cancellationToken) ?? Array.Empty<GroupStandingDto>();

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
        => await GetFromJsonOrDefaultAsync<IReadOnlyList<MatchListenerInfoDto>>("api/matches/listeners", cancellationToken) ?? Array.Empty<MatchListenerInfoDto>();

    public async Task EnsureMatchListenerAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/matches/{matchId}/listener", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
    }

    public async Task ReconcileMatchMonitoringAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/matches/{tournamentId}/monitoring/reconcile", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
    }

    public async Task ReconcileBoardMonitoringAsync(Guid boardId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/boards/{boardId}/monitoring/reconcile", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
    }

    // ─── Board Status (#10) ───

    public async Task<BoardDto?> GetBoardAsync(Guid id, CancellationToken cancellationToken = default)
        => await GetFromJsonOrDefaultAsync<BoardDto>($"api/boards/{id}", cancellationToken);

    public async Task<BoardDto> UpdateBoardConnectionStateAsync(Guid id, string state, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/boards/{id}/connection-state?state={Uri.EscapeDataString(state)}", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<BoardDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<BoardDto> UpdateBoardExtensionStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/boards/{id}/extension-status?status={Uri.EscapeDataString(status)}", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<BoardDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<IReadOnlyList<BoardDto>> GetBoardsByTournamentAsync(Guid tournamentId, CancellationToken cancellationToken = default)
        => await GetFromJsonOrDefaultAsync<IReadOnlyList<BoardDto>>($"api/boards/tournament/{tournamentId}", cancellationToken) ?? Array.Empty<BoardDto>();

    // ─── Match Statistics (#18) ───

    public async Task<IReadOnlyList<MatchPlayerStatisticDto>> GetMatchStatisticsAsync(Guid matchId, CancellationToken cancellationToken = default)
        => await GetFromJsonOrDefaultAsync<IReadOnlyList<MatchPlayerStatisticDto>>($"api/matches/{matchId}/statistics", cancellationToken) ?? Array.Empty<MatchPlayerStatisticDto>();

    public async Task<IReadOnlyList<MatchPlayerStatisticDto>> SyncMatchStatisticsAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/matches/{matchId}/statistics/sync", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<MatchPlayerStatisticDto>>(cancellationToken: cancellationToken))
            ?? Array.Empty<MatchPlayerStatisticDto>();
    }

    // ─── Match Followers (#14) ───

    public async Task<IReadOnlyList<MatchFollowerDto>> GetMatchFollowersAsync(Guid matchId, CancellationToken cancellationToken = default)
        => await GetFromJsonOrDefaultAsync<IReadOnlyList<MatchFollowerDto>>($"api/matches/{matchId}/followers", cancellationToken) ?? Array.Empty<MatchFollowerDto>();

    public async Task<MatchFollowerDto> FollowMatchAsync(Guid matchId, string userAccountName, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/matches/{matchId}/follow?userAccountName={Uri.EscapeDataString(userAccountName)}", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<MatchFollowerDto>(cancellationToken: cancellationToken))!;
    }

    public async Task UnfollowMatchAsync(Guid matchId, string userAccountName, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/matches/{matchId}/follow?userAccountName={Uri.EscapeDataString(userAccountName)}", cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
    }

    // ─── Scheduling (#12) ───

    public async Task<IReadOnlyList<MatchDto>> RecalculateScheduleAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/matches/{tournamentId}/recalculate-schedule", null, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<MatchDto>>(cancellationToken: cancellationToken))
            ?? Array.Empty<MatchDto>();
    }

    // ─── Notifications (#14) ───

    public async Task<IReadOnlyList<NotificationSubscriptionDto>> GetNotificationSubscriptionsAsync(Guid tournamentId, string userAccountName, CancellationToken cancellationToken = default)
        => await GetFromJsonOrDefaultAsync<IReadOnlyList<NotificationSubscriptionDto>>($"api/tournaments/{tournamentId}/notifications/{Uri.EscapeDataString(userAccountName)}", cancellationToken)
            ?? Array.Empty<NotificationSubscriptionDto>();

    public async Task<NotificationSubscriptionDto> SubscribeNotificationsAsync(CreateNotificationSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/tournaments/{request.TournamentId}/notifications", request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<NotificationSubscriptionDto>(cancellationToken: cancellationToken))!;
    }

    public async Task UnsubscribeNotificationsAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/tournaments/notifications/{subscriptionId}", cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
    }

    // ─── Discord Webhook (#14) ───

    public async Task<bool> TestDiscordWebhookAsync(Guid tournamentId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/tournaments/{tournamentId}/webhook/test", null, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    // ─── VAPID Public Key ───

    public async Task<string?> GetVapidPublicKeyAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/tournaments/vapid-public-key", cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    // ─── View Preferences (#15) ───

    public async Task<UserViewPreferenceDto?> GetViewPreferenceAsync(string userAccountName, string viewContext, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/tournaments/preferences/{Uri.EscapeDataString(userAccountName)}/{Uri.EscapeDataString(viewContext)}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<UserViewPreferenceDto>(cancellationToken: cancellationToken);
    }

    public async Task<UserViewPreferenceDto> SaveViewPreferenceAsync(string userAccountName, string viewContext, string settingsJson, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/tournaments/preferences/{Uri.EscapeDataString(userAccountName)}/{Uri.EscapeDataString(viewContext)}", settingsJson, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<UserViewPreferenceDto>(cancellationToken: cancellationToken))!;
    }

    // ─── Admins ───

    public async Task<bool> CheckIsAdminAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/admins/check", cancellationToken);
        if (!response.IsSuccessStatusCode) return false;
        return (await response.Content.ReadFromJsonAsync<bool>(cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AdminDto>> GetAdminsAsync(CancellationToken cancellationToken = default)
        => await GetFromJsonOrDefaultAsync<IReadOnlyList<AdminDto>>("api/admins", cancellationToken) ?? Array.Empty<AdminDto>();

    public async Task<AdminDto> CreateAdminAsync(CreateAdminRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/admins", request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<AdminDto>(cancellationToken: cancellationToken))!;
    }

    public async Task<AdminDto> UpdateAdminAsync(UpdateAdminRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/admins/{request.Id}", request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<AdminDto>(cancellationToken: cancellationToken))!;
    }

    public async Task DeleteAdminAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/admins/{id}", cancellationToken);
        await EnsureSuccessOrThrowAsync(response, cancellationToken);
    }

    public sealed record BoardExtensionSyncRequestAcceptedDto(bool Requested, Guid RequestId, DateTimeOffset RequestedAtUtc);

    public sealed record BoardExtensionSyncDebugDto(
        Guid BoardId,
        Guid? RequestId,
        DateTimeOffset? RequestedAtUtc,
        DateTimeOffset? ConsumedAtUtc,
        bool? ShouldSync,
        DateTimeOffset? ReportedAtUtc,
        bool? Matched,
        Guid? MatchId,
        string? MatchedBy,
        string? DerivedStatus,
        Guid? BoardCurrentMatchId,
        string? BoardCurrentMatchLabel,
        string? ExternalMatchId,
        string? Player1,
        string? Player2,
        string? MatchStatus,
        string? SourceUrl,
        Guid? TournamentId);
}
