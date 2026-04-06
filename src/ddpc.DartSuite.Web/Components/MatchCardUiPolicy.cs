using ddpc.DartSuite.Application.Contracts.Matches;

namespace ddpc.DartSuite.Web.Components;

public static class MatchCardUiPolicy
{
    public static bool IsPushMonitoring(MatchListenerInfoDto? listener)
    {
        if (listener is null)
            return false;

        if (listener.IsWebSocketActive)
            return true;

        return string.Equals(listener.TransportMode, "websocket", StringComparison.OrdinalIgnoreCase)
            && !listener.IsFallbackActive;
    }

    public static bool IsPollMonitoring(MatchListenerInfoDto? listener)
    {
        if (listener is null)
            return false;

        if (listener.IsRunning)
            return true;

        return string.Equals(listener.TransportMode, "polling", StringComparison.OrdinalIgnoreCase)
            || listener.IsFallbackActive;
    }

    public static bool IsMonitored(MatchListenerInfoDto? listener)
        => IsPushMonitoring(listener) || IsPollMonitoring(listener);

    public static string MonitoringBadgeText(MatchListenerInfoDto? listener)
    {
        if (IsPushMonitoring(listener))
            return "Push";

        if (IsPollMonitoring(listener))
            return "Poll";

        return "keine Ueberwachung";
    }

    public static string MonitoringBadgeCss(MatchListenerInfoDto? listener)
    {
        if (IsPushMonitoring(listener))
            return "text-bg-success";

        if (IsPollMonitoring(listener))
            return "text-bg-primary";

        return "text-bg-danger";
    }

    public static bool ShowMonitoring(MatchCardViewSettings settings, bool isManager)
        => settings.ShowMonitoringStatus && isManager;

    public static bool ShowActionBar(MatchCardViewSettings settings)
        => settings.ShowActionButtons;

    public static bool ShowSyncAction(MatchCardViewSettings settings, bool isManager, string? externalMatchId)
        => settings.ShowActionButtons
           && settings.ShowSyncAction
           && isManager
           && !string.IsNullOrWhiteSpace(externalMatchId);

    public static bool ShowFollowAction(MatchCardViewSettings settings, bool isConnected)
        => settings.ShowActionButtons
           && settings.ShowFollowAction
           && isConnected;

    public static bool ShowFollowActionForUser(MatchCardViewSettings settings, bool isConnected, string? userAccountName)
        => ShowFollowAction(settings, IsConnectedUser(isConnected, userAccountName));

    public static bool CanSync(bool isManager, bool isSyncing, bool isWorking, string? externalMatchId)
        => isManager
           && !isSyncing
           && !isWorking
           && !string.IsNullOrWhiteSpace(externalMatchId);

    public static bool CanFollow(bool isConnected, bool isFollowBusy)
        => isConnected && !isFollowBusy;

    public static bool CanFollowForUser(bool isConnected, string? userAccountName, bool isFollowBusy)
        => CanFollow(IsConnectedUser(isConnected, userAccountName), isFollowBusy);

    public static bool CanOpenParticipant(Guid? participantId)
        => participantId.HasValue && participantId.Value != Guid.Empty;

    public static bool CanOpenBoard(Guid? boardId)
        => boardId.HasValue;

    public static bool IsConnectedUser(bool isConnected, string? userAccountName)
        => isConnected && !string.IsNullOrWhiteSpace(userAccountName);
}
