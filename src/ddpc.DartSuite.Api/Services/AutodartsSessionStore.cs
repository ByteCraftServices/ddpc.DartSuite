using System.Collections.Concurrent;
using ddpc.DartSuite.ApiClient.Contracts;

namespace ddpc.DartSuite.Api.Services;

public sealed class AutodartsSessionStore
{
    private readonly ConcurrentDictionary<string, AutodartsSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Active session id (most recent successful login).</summary>
    public string? ActiveSessionId { get; private set; }

    public AutodartsSession Create(string sessionId, string codeVerifier)
    {
        var session = new AutodartsSession(sessionId, codeVerifier, null, null, null, null);
        _sessions[sessionId] = session;
        return session;
    }

    public bool TryGet(string sessionId, out AutodartsSession session)
        => _sessions.TryGetValue(sessionId, out session!);

    public AutodartsSession? GetActive()
    {
        if (ActiveSessionId is not null && _sessions.TryGetValue(ActiveSessionId, out var session))
        {
            return session;
        }

        return null;
    }

    public void UpdateTokens(string sessionId, string accessToken, string? refreshToken, DateTimeOffset expiresAt)
    {
        _sessions.AddOrUpdate(sessionId,
            _ => new AutodartsSession(sessionId, null, accessToken, refreshToken, expiresAt, null),
            (_, existing) => existing with
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken ?? existing.RefreshToken,
                ExpiresAt = expiresAt
            });

        ActiveSessionId = sessionId;
    }

    public void SetProfile(string sessionId, AutodartsProfile profile)
    {
        if (_sessions.TryGetValue(sessionId, out var existing))
        {
            _sessions[sessionId] = existing with { Profile = profile };
        }
    }

    public bool IsTokenExpired(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return true;
        }

        // No ExpiresAt means we don't know the expiry — treat as expired to force refresh
        if (!session.ExpiresAt.HasValue)
        {
            return true;
        }

        return session.ExpiresAt.Value <= DateTimeOffset.UtcNow.AddSeconds(60);
    }

    public void SetCredentials(string sessionId, string usernameOrEmail, string password)
    {
        if (_sessions.TryGetValue(sessionId, out var existing))
        {
            _sessions[sessionId] = existing with { UsernameOrEmail = usernameOrEmail, Password = password };
        }
    }

    public void SetApiAudience(string sessionId, string audience)
    {
        if (_sessions.TryGetValue(sessionId, out var existing))
        {
            _sessions[sessionId] = existing with { ApiAudience = audience };
        }
    }

    public void Remove(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
        if (string.Equals(ActiveSessionId, sessionId, StringComparison.OrdinalIgnoreCase))
        {
            ActiveSessionId = null;
        }
    }
}

public sealed record AutodartsSession(
    string SessionId,
    string? CodeVerifier,
    string? AccessToken,
    string? RefreshToken,
    DateTimeOffset? ExpiresAt,
    AutodartsProfile? Profile,
    string? UsernameOrEmail = null,
    string? Password = null,
    string? ApiAudience = null);
