using ddpc.DartSuite.ApiClient.Contracts;

namespace ddpc.DartSuite.ApiClient;

public interface IAutodartsClient
{
    Task<AutodartsLoginResult> LoginAsync(AutodartsAuthRequest request, CancellationToken cancellationToken = default);
    Task<AutodartsTokenResponse> ExchangeAuthorizationCodeAsync(string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken = default);
    Task<AutodartsTokenResponse> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<AutodartsProfile> GetProfileAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutodartsBoard>> GetBoardsAsync(string accessToken, string? userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutodartsFriend>> GetFriendsAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<AutodartsMatchDetail?> GetMatchAsync(string accessToken, string matchId, bool allowLobbyFallback = true, CancellationToken cancellationToken = default);
    Task<AutodartsLobby?> GetLobbyAsync(string accessToken, string lobbyId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<AutodartsEvent> ReadEventsAsync(string accessToken, string boardExternalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lightweight password-grant authentication with optional audience override.
    /// Returns a token suitable for API access.
    /// </summary>
    Task<AutodartsTokenResponse> AuthenticateAsync(string usernameOrEmail, string password, string? audience = null, CancellationToken cancellationToken = default);

    /// <summary>Returns the configured audience candidates for API token discovery.</summary>
    IEnumerable<string> GetAudienceCandidates();
}