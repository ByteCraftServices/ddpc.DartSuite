using ddpc.DartSuite.ApiClient.Contracts;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ddpc.DartSuite.ApiClient;

public sealed class AutodartsClient(HttpClient httpClient, IOptions<AutodartsOptions> options) : IAutodartsClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly AutodartsOptions _options = options.Value;

    public async Task<AutodartsLoginResult> LoginAsync(AutodartsAuthRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.UsernameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new InvalidOperationException("Autodarts credentials are required.");
        }

        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new InvalidOperationException("Autodarts client id is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.AuthBaseUrl) || string.IsNullOrWhiteSpace(_options.ApiBaseUrl))
        {
            throw new InvalidOperationException("Autodarts endpoints are not configured.");
        }

        var token = await RequestTokenAsync(request, null, cancellationToken);
        using var userInfoDoc = await RequestUserInfoAsync(token.AccessToken, cancellationToken);
        var profile = ParseUserInfo(userInfoDoc);

        // Discover API-capable audience by trying boards endpoint with audience candidates.
        // The default token (aud=account) is often rejected by Autodarts API endpoints.
        var apiToken = token;
        IReadOnlyList<AutodartsBoard> boards;
        try
        {
            var (fetchedBoards, boardUser) = await RequestBoardsAsync(token.AccessToken, profile.Id, cancellationToken);
            boards = fetchedBoards;
            profile = MergeProfile(profile, boardUser);
        }
        catch (InvalidOperationException)
        {
            // Default token rejected — try audience candidates to find one that works
            boards = Array.Empty<AutodartsBoard>();
            foreach (var candidate in BuildAudienceCandidates())
            {
                try
                {
                    var candidateToken = await RequestTokenAsync(request, candidate, cancellationToken);
                    var (fetchedBoards, boardUser) = await RequestBoardsAsync(candidateToken.AccessToken, profile.Id, cancellationToken);
                    boards = fetchedBoards;
                    profile = MergeProfile(profile, boardUser);
                    apiToken = candidateToken; // Use the API-capable token
                    break;
                }
                catch (InvalidOperationException) { /* try next candidate */ }
            }
        }

        return new AutodartsLoginResult(profile, boards, new AutodartsTokenResponse(apiToken.AccessToken, apiToken.RefreshToken, apiToken.ExpiresIn));
    }

    public async Task<AutodartsTokenResponse> ExchangeAuthorizationCodeAsync(
        string code,
        string codeVerifier,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new InvalidOperationException("Autodarts client id is not configured.");
        }

        var tokenUrl = new Uri(new Uri(_options.AuthBaseUrl), $"realms/{_options.Realm}/protocol/openid-connect/token");
        var fields = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "authorization_code"),
            new("client_id", _options.ClientId!),
            new("code", code),
            new("redirect_uri", redirectUri),
            new("code_verifier", codeVerifier)
        };

        if (!string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            fields.Add(new("client_secret", _options.ClientSecret!));
        }

        if (!string.IsNullOrWhiteSpace(_options.Scope))
        {
            fields.Add(new("scope", _options.Scope!));
        }

        using var response = await _httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(fields), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(body)
                ? "Autodarts authorization code exchange failed."
                : body);
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var accessToken = GetString(doc.RootElement, "access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Autodarts access token missing.");
        }

        var refreshToken = GetString(doc.RootElement, "refresh_token");
        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var expiresEl) && expiresEl.TryGetInt32(out var secs)
            ? secs
            : 0;

        return new AutodartsTokenResponse(accessToken, refreshToken, expiresIn);
    }

    public async Task<AutodartsTokenResponse> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new InvalidOperationException("Autodarts refresh token is required.");
        }

        var tokenUrl = new Uri(new Uri(_options.AuthBaseUrl), $"realms/{_options.Realm}/protocol/openid-connect/token");
        var fields = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "refresh_token"),
            new("client_id", _options.ClientId!),
            new("refresh_token", refreshToken)
        };

        if (!string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            fields.Add(new("client_secret", _options.ClientSecret!));
        }

        using var response = await _httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(fields), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(body)
                ? "Autodarts refresh token exchange failed."
                : body);
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var accessToken = GetString(doc.RootElement, "access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Autodarts access token missing.");
        }

        var newRefreshToken = GetString(doc.RootElement, "refresh_token");
        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var expiresEl) && expiresEl.TryGetInt32(out var secs)
            ? secs
            : 0;

        return new AutodartsTokenResponse(accessToken, newRefreshToken, expiresIn);
    }

    public async Task<AutodartsProfile> GetProfileAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var userInfoDoc = await RequestUserInfoAsync(accessToken, cancellationToken);
        return ParseUserInfo(userInfoDoc);
    }

    public async Task<IReadOnlyList<AutodartsBoard>> GetBoardsAsync(string accessToken, string? userId, CancellationToken cancellationToken = default)
    {
        var result = await RequestBoardsAsync(accessToken, userId, cancellationToken);
        return result.Boards;
    }

    public async Task<IReadOnlyList<AutodartsFriend>> GetFriendsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var friendsUrl = new Uri(new Uri(_options.ApiBaseUrl), "as/v0/users/me/friends");
        using var request = new HttpRequestMessage(HttpMethod.Get, friendsUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<AutodartsFriend>();
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var friends = new List<AutodartsFriend>();

        var items = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement
            : doc.RootElement.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array
                ? arr
                : (JsonElement?)null;

        if (items is null)
        {
            return friends;
        }

        foreach (var item in items.Value.EnumerateArray())
        {
            friends.Add(new AutodartsFriend(
                GetString(item, "id"),
                GetString(item, "name", "username", "preferred_username"),
                GetString(item, "country"),
                GetString(item, "status")));
        }

        return friends;
    }

    public async Task<AutodartsMatchDetail?> GetMatchAsync(string accessToken, string matchId, CancellationToken cancellationToken = default)
    {
        var matchUrl = new Uri(new Uri(_options.ApiBaseUrl), $"gs/v0/matches/{Uri.EscapeDataString(matchId)}");
        using var request = new HttpRequestMessage(HttpMethod.Get, matchUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new HttpRequestException("Autodarts API returned 401 Unauthorized", null, System.Net.HttpStatusCode.Unauthorized);
        }
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Autodarts API returned {(int)response.StatusCode}: {errorBody}",
                null,
                response.StatusCode);
        }

        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        return new AutodartsMatchDetail(
            GetString(root, "id") ?? matchId,
            GetString(root, "variant"),
            GetString(root, "gameMode"),
            root.TryGetProperty("finished", out var fin) && fin.GetBoolean(),
            GetElementOrNull(root, "players"),
            GetElementOrNull(root, "turns"),
            GetElementOrNull(root, "legs"),
            GetElementOrNull(root, "sets"),
            GetElementOrNull(root, "stats"),
            root.Clone());
    }

    public async Task<AutodartsLobby?> GetLobbyAsync(string accessToken, string lobbyId, CancellationToken cancellationToken = default)
    {
        var lobbyUrl = new Uri(new Uri(_options.ApiBaseUrl), $"gs/v0/lobbies/{Uri.EscapeDataString(lobbyId)}");
        using var request = new HttpRequestMessage(HttpMethod.Get, lobbyUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        return new AutodartsLobby(
            GetString(root, "id") ?? lobbyId,
            GetString(root, "status"),
            GetElementOrNull(root, "players"),
            GetElementOrNull(root, "settings"),
            root.Clone());
    }

    public async IAsyncEnumerable<AutodartsEvent> ReadEventsAsync(
        string boardExternalId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var payload = $"{{\"board\":\"{boardExternalId}\",\"status\":\"running\",\"message\":\"simulated-event\"}}";
            yield return new AutodartsEvent("board-status", boardExternalId, payload, DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }

    public async Task<AutodartsTokenResponse> AuthenticateAsync(
        string usernameOrEmail,
        string password,
        string? audience = null,
        CancellationToken cancellationToken = default)
    {
        var request = new AutodartsAuthRequest(usernameOrEmail, password);
        var token = await RequestTokenAsync(request, audience, cancellationToken);
        return new AutodartsTokenResponse(token.AccessToken, token.RefreshToken, token.ExpiresIn);
    }

    public IEnumerable<string> GetAudienceCandidates() => BuildAudienceCandidates();

    private async Task<AutodartsToken> RequestTokenAsync(AutodartsAuthRequest request, string? audienceOverride, CancellationToken cancellationToken)
    {
        var tokenUrl = new Uri(new Uri(_options.AuthBaseUrl), $"realms/{_options.Realm}/protocol/openid-connect/token");
        var fields = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "password"),
            new("client_id", _options.ClientId!),
            new("username", request.UsernameOrEmail),
            new("password", request.Password)
        };

        if (!string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            fields.Add(new("client_secret", _options.ClientSecret!));
        }

        if (!string.IsNullOrWhiteSpace(_options.Scope))
        {
            fields.Add(new("scope", _options.Scope!));
        }

        var audience = string.IsNullOrWhiteSpace(audienceOverride) ? _options.Audience : audienceOverride;
        if (!string.IsNullOrWhiteSpace(audience))
        {
            fields.Add(new("audience", audience!));
        }

        using var response = await _httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(fields), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(body)
                ? "Autodarts authentication failed."
                : body);
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var accessToken = GetString(doc.RootElement, "access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Autodarts access token missing.");
        }

        var refreshToken = GetString(doc.RootElement, "refresh_token");
        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var expiresEl) && expiresEl.TryGetInt32(out var secs)
            ? secs
            : 0;

        return new AutodartsToken(accessToken, refreshToken, expiresIn);
    }

    private async Task<JsonDocument?> RequestUserInfoAsync(string accessToken, CancellationToken cancellationToken)
    {
        var userInfoUrl = new Uri(new Uri(_options.AuthBaseUrl), $"realms/{_options.Realm}/protocol/openid-connect/userinfo");
        using var request = new HttpRequestMessage(HttpMethod.Get, userInfoUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
    }

    private async Task<(IReadOnlyList<AutodartsBoard> Boards, AutodartsBoardUser? User)> RequestBoardsAsync(
        string accessToken,
        string? userId,
        CancellationToken cancellationToken)
    {
        var boardsUrl = new Uri(new Uri(_options.ApiBaseUrl), "bs/v0/boards/");
        using var request = new HttpRequestMessage(HttpMethod.Get, boardsUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenInfo = BuildTokenDebugInfo(accessToken);
            var details = string.IsNullOrWhiteSpace(body) ? "unauthorized" : body;
            throw new InvalidOperationException($"Autodarts boards request failed: {details}. Token info: {tokenInfo}");
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var boardsElement = GetBoardsElement(doc.RootElement);
        var boards = new List<AutodartsBoard>();
        AutodartsBoardUser? matchedUser = null;

        if (boardsElement is null)
        {
            return (boards, matchedUser);
        }

        foreach (var board in boardsElement.Value.EnumerateArray())
        {
            var externalId = GetString(board, "id") ?? string.Empty;
            var name = GetString(board, "name") ?? externalId;
            var ip = GetString(board, "ip");
            var managerUrl = GetString(board, "managerUrl") ?? GetString(board, "manager");

            var ownership = (string?)null;
            if (board.TryGetProperty("permissions", out var permissions) && permissions.ValueKind == JsonValueKind.Array)
            {
                foreach (var permission in permissions.EnumerateArray())
                {
                    var role = GetString(permission, "role");
                    if (permission.TryGetProperty("user", out var userElement))
                    {
                        var permissionUser = ParseBoardUser(userElement);
                        if (!string.IsNullOrWhiteSpace(userId) &&
                            string.Equals(permissionUser?.Id, userId, StringComparison.OrdinalIgnoreCase))
                        {
                            ownership = NormalizeOwnership(role);
                            matchedUser ??= permissionUser;
                            break;
                        }

                        matchedUser ??= permissionUser;
                    }
                }
            }

            boards.Add(new AutodartsBoard(externalId, name, ip, managerUrl, ownership));
        }

        return (boards, matchedUser);
    }

    private async Task<(IReadOnlyList<AutodartsBoard> Boards, AutodartsBoardUser? User)> RequestBoardsWithFallbackAsync(
        AutodartsAuthRequest request,
        string? userId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            return await RequestBoardsAsync(accessToken, userId, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Fall through to audience retries.
        }

        foreach (var candidate in BuildAudienceCandidates())
        {
            var token = await RequestTokenAsync(request, candidate, cancellationToken);
            try
            {
                return await RequestBoardsAsync(token.AccessToken, userId, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // Try next audience candidate.
            }
        }

        // Retry with the original token to surface the original error details.
        return await RequestBoardsAsync(accessToken, userId, cancellationToken);
    }

    private static AutodartsProfile ParseUserInfo(JsonDocument? userInfo)
    {
        if (userInfo is null)
        {
            return new AutodartsProfile(null, null, null, null);
        }

        var root = userInfo.RootElement;
        return new AutodartsProfile(
            GetString(root, "sub") ?? GetString(root, "id"),
            GetString(root, "preferred_username", "name"),
            GetString(root, "country"),
            GetString(root, "email"));
    }

    private static AutodartsProfile MergeProfile(AutodartsProfile profile, AutodartsBoardUser? user)
    {
        if (user is null)
        {
            return profile;
        }

        return profile with
        {
            Id = profile.Id ?? user.Id,
            DisplayName = profile.DisplayName ?? user.Name,
            Country = profile.Country ?? user.Country,
            Email = profile.Email ?? user.Email
        };
    }

    private static JsonElement? GetBoardsElement(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root;
        }

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            return items;
        }

        return null;
    }

    private static AutodartsBoardUser? ParseBoardUser(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new AutodartsBoardUser(
            GetString(element, "id"),
            GetString(element, "name"),
            GetString(element, "country"),
            GetString(element, "email"));
    }

    private static string? NormalizeOwnership(string? role)
        => string.IsNullOrWhiteSpace(role) ? null : role.Equals("owner", StringComparison.OrdinalIgnoreCase) ? "owner" : "shared";

    private static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static JsonElement? GetElementOrNull(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined)
        {
            return value.Clone();
        }

        return null;
    }

    private IEnumerable<string> BuildAudienceCandidates()
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(_options.Audience))
        {
            candidates.Add(_options.Audience!);
        }

        if (_options.AudienceCandidates.Length > 0)
        {
            candidates.AddRange(_options.AudienceCandidates.Where(x => !string.IsNullOrWhiteSpace(x))!);
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildTokenDebugInfo(string accessToken)
    {
        try
        {
            var parts = accessToken.Split('.');
            if (parts.Length < 2)
            {
                return "token invalid";
            }

            var payloadJson = DecodeBase64Url(parts[1]);
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            var aud = root.TryGetProperty("aud", out var audElement)
                ? audElement.ValueKind == JsonValueKind.Array
                    ? string.Join(",", audElement.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)))
                    : audElement.GetString()
                : null;

            var azp = GetString(root, "azp");
            var scope = GetString(root, "scope");

            return $"aud={aud ?? "n/a"}; azp={azp ?? "n/a"}; scope={scope ?? "n/a"}";
        }
        catch
        {
            return "token parse failed";
        }
    }

    private static string DecodeBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        var bytes = Convert.FromBase64String(padded);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private sealed record AutodartsToken(string AccessToken, string? RefreshToken = null, int ExpiresIn = 0);
    private sealed record AutodartsBoardUser(string? Id, string? Name, string? Country, string? Email);
}