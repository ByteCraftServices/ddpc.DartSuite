using ddpc.DartSuite.ApiClient;
using ddpc.DartSuite.ApiClient.Contracts;
using ddpc.DartSuite.Api.Services;
using ddpc.DartSuite.Application.Abstractions;
using ddpc.DartSuite.Application.Contracts.Autodarts;
using ddpc.DartSuite.Application.Contracts.Boards;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;

namespace ddpc.DartSuite.Api.Controllers;

[ApiController]
[Route("api/autodarts")]
public sealed class AutodartsController(
    IAutodartsClient autodartsClient,
    IBoardManagementService boardService,
    AutodartsSessionStore sessionStore,
    IOptions<AutodartsOptions> options,
    ILogger<AutodartsController> logger) : ControllerBase
{
    private readonly AutodartsOptions _options = options.Value;

    // ───────────────────────────── Session Status ─────────────────────────────

    [HttpGet("status")]
    public ActionResult<AutodartsSessionStatusDto> GetStatus()
    {
        var session = sessionStore.GetActive();
        if (session is null || string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return Ok(new AutodartsSessionStatusDto(false, null, null));
        }

        var profileDto = session.Profile is not null
            ? new AutodartsProfileDto(session.Profile.Id, session.Profile.DisplayName, session.Profile.Country, session.Profile.Email)
            : null;

        return Ok(new AutodartsSessionStatusDto(true, profileDto, session.ExpiresAt));
    }

    // ───────────────────────────── Login (Password Grant) ─────────────────────

    [HttpPost("login")]
    public async Task<ActionResult<AutodartsLoginResponse>> Login([FromBody] AutodartsAuthRequest request, CancellationToken cancellationToken)
    {
        AutodartsLoginResult loginResult;
        try
        {
            loginResult = await autodartsClient.LoginAsync(request, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Autodarts login failed: {Message}", ex.Message);
            return Unauthorized(new { message = ex.Message });
        }

        // Store session with tokens
        var sessionId = Guid.NewGuid().ToString("N");
        var token = loginResult.Token;
        sessionStore.Create(sessionId, string.Empty);
        sessionStore.UpdateTokens(sessionId, token.AccessToken, token.RefreshToken,
            DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn > 0 ? token.ExpiresIn : 3600));

        var profile = loginResult.Profile;
        sessionStore.SetProfile(sessionId, profile);
        sessionStore.SetCredentials(sessionId, request.UsernameOrEmail, request.Password);

        var boards = loginResult.Boards;
        await SyncBoardsAsync(boards, cancellationToken);

        logger.LogInformation("Autodarts login successful for {User}, session {SessionId}", profile.DisplayName, sessionId);

        var response = new AutodartsLoginResponse(
            new AutodartsProfileDto(profile.Id, profile.DisplayName, profile.Country, profile.Email),
            boards.Select(board => new AutodartsBoardInfoDto(
                board.ExternalBoardId, board.Name, board.LocalIpAddress, board.BoardManagerUrl, board.Ownership)).ToList());

        return Ok(response);
    }

    // ───────────────────────────── OAuth PKCE Flow ────────────────────────────

    [HttpGet("oauth/start")]
    public ActionResult<AutodartsOauthStartResponse> StartOauth()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            return BadRequest(new { message = "Autodarts client id is not configured." });
        }

        var sessionId = Guid.NewGuid().ToString("N");
        var codeVerifier = CreateCodeVerifier();
        sessionStore.Create(sessionId, codeVerifier);

        var codeChallenge = CreateCodeChallenge(codeVerifier);
        var redirectUri = BuildCallbackUri();
        var scope = string.IsNullOrWhiteSpace(_options.Scope) ? "openid profile email" : _options.Scope;
        var authUrl = new Uri(new Uri(_options.AuthBaseUrl),
            $"realms/{_options.Realm}/protocol/openid-connect/auth" +
            $"?client_id={Uri.EscapeDataString(_options.ClientId!)}" +
            "&response_type=code" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&scope={Uri.EscapeDataString(scope!)}" +
            $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
            "&code_challenge_method=S256" +
            $"&state={Uri.EscapeDataString(sessionId)}");

        return Ok(new AutodartsOauthStartResponse(authUrl.ToString(), sessionId));
    }

    [HttpGet("oauth/callback")]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken cancellationToken)
    {
        var webBaseUrl = string.IsNullOrWhiteSpace(_options.WebBaseUrl) ? "https://localhost:7144" : _options.WebBaseUrl;

        if (!string.IsNullOrWhiteSpace(error))
        {
            return Redirect($"{webBaseUrl}/login?autodarts=error&reason={Uri.EscapeDataString(error)}");
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return Redirect($"{webBaseUrl}/login?autodarts=error&reason=missing_code");
        }

        if (!sessionStore.TryGet(state, out var session) || string.IsNullOrWhiteSpace(session.CodeVerifier))
        {
            return Redirect($"{webBaseUrl}/login?autodarts=error&reason=invalid_session");
        }

        var redirectUri = BuildCallbackUri();
        AutodartsTokenResponse token;
        try
        {
            token = await autodartsClient.ExchangeAuthorizationCodeAsync(code, session.CodeVerifier!, redirectUri, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Redirect($"{webBaseUrl}/login?autodarts=error&reason={Uri.EscapeDataString(ex.Message)}");
        }

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn > 0 ? token.ExpiresIn : 3600);
        sessionStore.UpdateTokens(state, token.AccessToken, token.RefreshToken, expiresAt);

        // Fetch and store profile
        try
        {
            var profile = await autodartsClient.GetProfileAsync(token.AccessToken, cancellationToken);
            sessionStore.SetProfile(state, profile);
        }
        catch (InvalidOperationException)
        {
            // Profile fetch optional at this stage
        }

        return Redirect($"{webBaseUrl}/login?autodarts=success&sessionId={Uri.EscapeDataString(state)}");
    }

    [HttpGet("oauth/session/{sessionId}")]
    public async Task<ActionResult<AutodartsLoginResponse>> GetSession(string sessionId, CancellationToken cancellationToken)
    {
        var accessToken = await GetValidAccessTokenAsync(sessionId, cancellationToken);
        if (accessToken is null)
        {
            return Unauthorized(new { message = "Autodarts session not found or expired." });
        }

        AutodartsProfile profile;
        IReadOnlyList<AutodartsBoard> boards;
        try
        {
            profile = await autodartsClient.GetProfileAsync(accessToken, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }

        try
        {
            boards = await autodartsClient.GetBoardsAsync(accessToken, profile.Id, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Boards fetch failed during session restore (non-fatal): {Message}", ex.Message);
            boards = Array.Empty<AutodartsBoard>();
        }

        sessionStore.SetProfile(sessionId, profile);
        await SyncBoardsAsync(boards, cancellationToken);

        var response = new AutodartsLoginResponse(
            new AutodartsProfileDto(profile.Id, profile.DisplayName, profile.Country, profile.Email),
            boards.Select(board => new AutodartsBoardInfoDto(
                board.ExternalBoardId, board.Name, board.LocalIpAddress, board.BoardManagerUrl, board.Ownership)).ToList());

        return Ok(response);
    }

    // ───────────────────────────── Boards ─────────────────────────────────────

    [HttpGet("boards")]
    public async Task<IActionResult> GetAutodartsBoards(CancellationToken cancellationToken)
    {
        var (accessToken, session) = await GetActiveAccessTokenAsync(cancellationToken);
        if (accessToken is null)
        {
            return Unauthorized(new { message = "Not connected to Autodarts. Please login first." });
        }

        try
        {
            var boards = await autodartsClient.GetBoardsAsync(accessToken, session?.Profile?.Id, cancellationToken);
            await SyncBoardsAsync(boards, cancellationToken);

            return Ok(boards.Select(b => new AutodartsBoardInfoDto(
                b.ExternalBoardId, b.Name, b.LocalIpAddress, b.BoardManagerUrl, b.Ownership)).ToList());
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Failed to fetch boards: {Message}", ex.Message);
            return StatusCode(502, new { message = ex.Message });
        }
    }

    // ───────────────────────────── Friends ────────────────────────────────────

    [HttpGet("friends")]
    public async Task<IActionResult> GetFriends(CancellationToken cancellationToken)
    {
        var (accessToken, _) = await GetActiveAccessTokenAsync(cancellationToken);
        if (accessToken is null)
        {
            return Unauthorized(new { message = "Not connected to Autodarts. Please login first." });
        }

        try
        {
            var friends = await autodartsClient.GetFriendsAsync(accessToken, cancellationToken);
            return Ok(friends.Select(f => new AutodartsFriendDto(f.Id, f.Name, f.Country, f.Status)).ToList());
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Failed to fetch friends: {Message}", ex.Message);
            return StatusCode(502, new { message = ex.Message });
        }
    }

    // ───────────────────────────── Matches ────────────────────────────────────

    [HttpGet("matches/{matchId}")]
    public async Task<IActionResult> GetMatch(string matchId, CancellationToken cancellationToken)
    {
        var (accessToken, _) = await GetActiveAccessTokenAsync(cancellationToken);
        if (accessToken is null)
        {
            return Unauthorized(new { message = "Not connected to Autodarts. Please login first." });
        }

        try
        {
            var match = await autodartsClient.GetMatchAsync(accessToken, matchId, cancellationToken);
            if (match is null)
            {
                return NotFound(new { message = $"Match {matchId} not found." });
            }

            return Ok(match.RawJson);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Failed to fetch match {MatchId}: {Message}", matchId, ex.Message);
            return StatusCode(502, new { message = ex.Message });
        }
    }

    // ───────────────────────────── Lobbies ────────────────────────────────────

    [HttpGet("lobbies/{lobbyId}")]
    public async Task<IActionResult> GetLobby(string lobbyId, CancellationToken cancellationToken)
    {
        var (accessToken, _) = await GetActiveAccessTokenAsync(cancellationToken);
        if (accessToken is null)
        {
            return Unauthorized(new { message = "Not connected to Autodarts. Please login first." });
        }

        try
        {
            var lobby = await autodartsClient.GetLobbyAsync(accessToken, lobbyId, cancellationToken);
            if (lobby is null)
            {
                return NotFound(new { message = $"Lobby {lobbyId} not found." });
            }

            return Ok(lobby.RawJson);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Failed to fetch lobby {LobbyId}: {Message}", lobbyId, ex.Message);
            return StatusCode(502, new { message = ex.Message });
        }
    }

    // ───────────────────────────── Extension endpoints ────────────────────────

    [HttpPost("page-event")]
    public async Task<IActionResult> PageEvent([FromBody] PageEventRequest request, CancellationToken cancellationToken)
    {
        logger.LogDebug("Page event from extension: {Url}, matchId={MatchId}, lobbyId={LobbyId}",
            request.SourceUrl, request.MatchId, request.LobbyId);

        var (accessToken, session) = await GetActiveAccessTokenAsync(cancellationToken);

        object? matchData = null;
        object? lobbyData = null;

        if (accessToken is not null)
        {
            if (!string.IsNullOrWhiteSpace(request.MatchId))
            {
                var match = await autodartsClient.GetMatchAsync(accessToken, request.MatchId!, cancellationToken);
                matchData = match is not null
                    ? new { match.Id, match.Variant, match.GameMode, match.Finished }
                    : null;
            }

            if (!string.IsNullOrWhiteSpace(request.LobbyId))
            {
                var lobby = await autodartsClient.GetLobbyAsync(accessToken, request.LobbyId!, cancellationToken);
                lobbyData = lobby is not null
                    ? new { lobby.Id, lobby.Status }
                    : null;
            }
        }

        return Ok(new
        {
            received = true,
            connected = accessToken is not null,
            match = matchData,
            lobby = lobbyData
        });
    }

    [HttpPost("boards-import")]
    public async Task<IActionResult> ImportBoards([FromBody] AutodartsBoardsImportRequest request, CancellationToken cancellationToken)
    {
        if (request.Boards is null || request.Boards.Count == 0)
        {
            return BadRequest(new { message = "No boards supplied." });
        }

        var boards = request.Boards
            .Where(board => !string.IsNullOrWhiteSpace(board.ExternalBoardId))
            .Select(board => new AutodartsBoard(
                board.ExternalBoardId,
                board.Name ?? board.ExternalBoardId,
                board.LocalIpAddress,
                board.BoardManagerUrl,
                board.Ownership))
            .ToList();

        if (boards.Count == 0)
        {
            return BadRequest(new { message = "No valid boards supplied." });
        }

        await SyncBoardsAsync(boards, cancellationToken);
        return Ok(new { synced = boards.Count });
    }

    [HttpPost("match-import")]
    public IActionResult ImportMatch([FromBody] AutodartsMatchImportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SourceUrl))
        {
            return BadRequest(new { message = "Source URL is required." });
        }

        // TODO: persist match data to database
        return Ok(new
        {
            request.SourceUrl,
            request.MatchId,
            request.LobbyId,
            hasMatch = HasPayload(request.Match),
            hasStats = HasPayload(request.Stats),
            hasLobby = HasPayload(request.Lobby)
        });
    }

    [HttpGet("ping")]
    public IActionResult Ping()
    {
        var session = sessionStore.GetActive();
        return Ok(new
        {
            status = "ok",
            connected = session is not null && !string.IsNullOrWhiteSpace(session.AccessToken),
            user = session?.Profile?.DisplayName
        });
    }

    // ───────────────────────────── Legacy endpoints ───────────────────────────

    [HttpPost("refresh-login")]
    public async Task<ActionResult<AutodartsLoginResponse>> RefreshLogin([FromBody] AutodartsRefreshLoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new { message = "Autodarts refresh token is required." });
        }

        AutodartsTokenResponse token;
        try
        {
            token = await autodartsClient.RefreshAccessTokenAsync(request.RefreshToken, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }

        var sessionId = Guid.NewGuid().ToString("N");
        sessionStore.Create(sessionId, string.Empty);
        sessionStore.UpdateTokens(sessionId, token.AccessToken, token.RefreshToken,
            DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn > 0 ? token.ExpiresIn : 3600));

        AutodartsProfile profile;
        IReadOnlyList<AutodartsBoard> boards;
        try
        {
            profile = await autodartsClient.GetProfileAsync(token.AccessToken, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }

        try
        {
            boards = await autodartsClient.GetBoardsAsync(token.AccessToken, profile.Id, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Boards fetch failed during refresh-login (non-fatal): {Message}", ex.Message);
            boards = Array.Empty<AutodartsBoard>();
        }

        sessionStore.SetProfile(sessionId, profile);
        await SyncBoardsAsync(boards, cancellationToken);

        var response = new AutodartsLoginResponse(
            new AutodartsProfileDto(profile.Id, profile.DisplayName, profile.Country, profile.Email),
            boards.Select(board => new AutodartsBoardInfoDto(
                board.ExternalBoardId, board.Name, board.LocalIpAddress, board.BoardManagerUrl, board.Ownership)).ToList());

        return Ok(response);
    }

    [HttpPost("token-login")]
    public async Task<ActionResult<AutodartsLoginResponse>> TokenLogin([FromBody] AutodartsTokenLoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AccessToken))
        {
            return BadRequest(new { message = "Autodarts access token is required." });
        }

        AutodartsProfile profile;
        IReadOnlyList<AutodartsBoard> boards;
        try
        {
            profile = await autodartsClient.GetProfileAsync(request.AccessToken, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }

        try
        {
            boards = await autodartsClient.GetBoardsAsync(request.AccessToken, profile.Id, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Boards fetch failed during token-login (non-fatal): {Message}", ex.Message);
            boards = Array.Empty<AutodartsBoard>();
        }

        var sessionId = Guid.NewGuid().ToString("N");
        sessionStore.Create(sessionId, string.Empty);
        sessionStore.UpdateTokens(sessionId, request.AccessToken, null,
            DateTimeOffset.UtcNow.AddMinutes(55));
        sessionStore.SetProfile(sessionId, profile);

        await SyncBoardsAsync(boards, cancellationToken);

        var response = new AutodartsLoginResponse(
            new AutodartsProfileDto(profile.Id, profile.DisplayName, profile.Country, profile.Email),
            boards.Select(board => new AutodartsBoardInfoDto(
                board.ExternalBoardId, board.Name, board.LocalIpAddress, board.BoardManagerUrl, board.Ownership)).ToList());

        return Ok(response);
    }

    // ───────────────────────────── Helpers ────────────────────────────────────

    private async Task<(string? AccessToken, AutodartsSession? Session)> GetActiveAccessTokenAsync(CancellationToken cancellationToken)
    {
        var session = sessionStore.GetActive();
        if (session is null || string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return (null, null);
        }

        var accessToken = await GetValidAccessTokenAsync(session.SessionId, cancellationToken);
        if (accessToken is null)
        {
            return (null, null);
        }

        // Re-read session in case it was updated during refresh
        sessionStore.TryGet(session.SessionId, out var updatedSession);
        return (accessToken, updatedSession ?? session);
    }

    private async Task<string?> GetValidAccessTokenAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (!sessionStore.TryGet(sessionId, out var session) || string.IsNullOrWhiteSpace(session.AccessToken))
        {
            return null;
        }

        // If token is not expired, use it
        if (!sessionStore.IsTokenExpired(sessionId))
        {
            return session.AccessToken;
        }

        // Try refresh
        if (!string.IsNullOrWhiteSpace(session.RefreshToken))
        {
            try
            {
                logger.LogInformation("Refreshing Autodarts token for session {SessionId}", sessionId);
                var newToken = await autodartsClient.RefreshAccessTokenAsync(session.RefreshToken!, cancellationToken);
                var expiresAt = DateTimeOffset.UtcNow.AddSeconds(newToken.ExpiresIn > 0 ? newToken.ExpiresIn : 3600);
                sessionStore.UpdateTokens(sessionId, newToken.AccessToken, newToken.RefreshToken, expiresAt);
                return newToken.AccessToken;
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning("Token refresh failed: {Message}", ex.Message);
            }
        }

        // Return existing token as last resort (might still work)
        return session.AccessToken;
    }

    private async Task SyncBoardsAsync(IReadOnlyList<AutodartsBoard> boards, CancellationToken cancellationToken)
    {
        var existing = await boardService.GetBoardsAsync(cancellationToken);
        var existingExternalIds = existing.Select(x => x.ExternalBoardId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var board in boards)
        {
            if (existingExternalIds.Contains(board.ExternalBoardId))
            {
                continue;
            }

            try
            {
                await boardService.CreateBoardAsync(new CreateBoardRequest(
                    board.ExternalBoardId,
                    board.Name,
                    board.LocalIpAddress,
                    board.BoardManagerUrl),
                    cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // Ignore duplicates from concurrent requests.
            }
        }
    }

    private string BuildCallbackUri()
        => $"{Request.Scheme}://{Request.Host}/api/autodarts/oauth/callback";

    private static string CreateCodeVerifier()
        => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string CreateCodeChallenge(string verifier)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(verifier));
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool HasPayload(JsonElement? element)
        => element.HasValue && element.Value.ValueKind != JsonValueKind.Null && element.Value.ValueKind != JsonValueKind.Undefined;
}