using ddpc.DartSuite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ddpc.DartSuite.Api.Services;

public sealed class TournamentAuthorizationService(
    AutodartsSessionStore sessionStore,
    DartSuiteDbContext dbContext,
    IConfiguration configuration,
    ILogger<TournamentAuthorizationService> logger)
{
    private const string IntegrationHeaderName = "X-DartSuite-Integration-Key";
    private readonly string? _integrationApiKey = configuration["Security:IntegrationApiKey"];

    public async Task<AccessCheckResult> EnsureManagerOrIntegrationAsync(HttpContext context, Guid tournamentId, CancellationToken cancellationToken)
    {
        if (IsIntegrationRequest(context))
            return AccessCheckResult.Allow("integration");

        var actorName = GetActiveActorName();
        if (string.IsNullOrWhiteSpace(actorName))
            return AccessCheckResult.Unauthorized("Autodarts-Login erforderlich.");

        var tournament = await dbContext.Tournaments
            .AsNoTracking()
            .Where(x => x.Id == tournamentId)
            .Select(x => new { x.OrganizerAccount })
            .FirstOrDefaultAsync(cancellationToken);
        if (tournament is null)
            return AccessCheckResult.Forbidden("Kein Zugriff auf dieses Turnier.");

        if (string.Equals(tournament.OrganizerAccount, actorName, StringComparison.OrdinalIgnoreCase))
            return AccessCheckResult.Allow("organizer");

        var managerParticipant = await dbContext.Participants
            .AsNoTracking()
            .AnyAsync(x =>
                x.TournamentId == tournamentId
                && x.IsManager
                && (string.Equals(x.AccountName, actorName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.DisplayName, actorName, StringComparison.OrdinalIgnoreCase)),
                cancellationToken);

        return managerParticipant
            ? AccessCheckResult.Allow("manager")
            : AccessCheckResult.Forbidden("Manager-Berechtigung erforderlich.");
    }

    public async Task<AccessCheckResult> EnsureMemberOrManagerOrIntegrationAsync(HttpContext context, Guid tournamentId, CancellationToken cancellationToken)
    {
        if (IsIntegrationRequest(context))
            return AccessCheckResult.Allow("integration");

        var managerAccess = await EnsureManagerOrIntegrationAsync(context, tournamentId, cancellationToken);
        if (managerAccess.Allowed)
            return managerAccess;

        if (managerAccess.StatusCode == StatusCodes.Status401Unauthorized)
            return managerAccess;

        var actorName = GetActiveActorName();
        if (string.IsNullOrWhiteSpace(actorName))
            return AccessCheckResult.Unauthorized("Autodarts-Login erforderlich.");

        var isParticipant = await dbContext.Participants
            .AsNoTracking()
            .AnyAsync(x =>
                x.TournamentId == tournamentId
                && (string.Equals(x.AccountName, actorName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.DisplayName, actorName, StringComparison.OrdinalIgnoreCase)),
                cancellationToken);

        return isParticipant
            ? AccessCheckResult.Allow("participant")
            : AccessCheckResult.Forbidden("Kein Zugriff auf dieses Turnier.");
    }

    public async Task<AccessCheckResult> EnsureSelfOrManagerOrIntegrationAsync(HttpContext context, Guid tournamentId, string userAccountName, CancellationToken cancellationToken)
    {
        if (IsIntegrationRequest(context))
            return AccessCheckResult.Allow("integration");

        var managerAccess = await EnsureManagerOrIntegrationAsync(context, tournamentId, cancellationToken);
        if (managerAccess.Allowed)
            return managerAccess;

        if (managerAccess.StatusCode == StatusCodes.Status401Unauthorized)
            return managerAccess;

        var actorName = GetActiveActorName();
        if (string.IsNullOrWhiteSpace(actorName))
            return AccessCheckResult.Unauthorized("Autodarts-Login erforderlich.");

        return string.Equals(actorName, userAccountName, StringComparison.OrdinalIgnoreCase)
            ? AccessCheckResult.Allow("self")
            : AccessCheckResult.Forbidden("Nur eigene Daten sind erlaubt.");
    }

    public AccessCheckResult EnsureSelfOrIntegration(HttpContext context, string userAccountName)
    {
        if (IsIntegrationRequest(context))
            return AccessCheckResult.Allow("integration");

        var actorName = GetActiveActorName();
        if (string.IsNullOrWhiteSpace(actorName))
            return AccessCheckResult.Unauthorized("Autodarts-Login erforderlich.");

        return string.Equals(actorName, userAccountName, StringComparison.OrdinalIgnoreCase)
            ? AccessCheckResult.Allow("self")
            : AccessCheckResult.Forbidden("Nur eigene Daten sind erlaubt.");
    }

    public bool IsIntegrationRequest(HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(_integrationApiKey))
            return false;

        if (!context.Request.Headers.TryGetValue(IntegrationHeaderName, out var providedKey))
            return false;

        var valid = string.Equals(providedKey.ToString(), _integrationApiKey, StringComparison.Ordinal);
        if (!valid)
            logger.LogWarning("Rejected integration request due to invalid integration key.");

        return valid;
    }

    private string? GetActiveActorName()
    {
        var profile = sessionStore.GetActive()?.Profile;
        return profile?.DisplayName?.Trim();
    }
}

public sealed record AccessCheckResult(bool Allowed, int StatusCode, string Message, string? Reason = null)
{
    public static AccessCheckResult Allow(string reason) => new(true, StatusCodes.Status200OK, string.Empty, reason);
    public static AccessCheckResult Unauthorized(string message) => new(false, StatusCodes.Status401Unauthorized, message);
    public static AccessCheckResult Forbidden(string message) => new(false, StatusCodes.Status403Forbidden, message);
}
