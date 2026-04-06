namespace ddpc.DartSuite.Web.Components;

public sealed class MatchCardViewSettings
{
    public string Layout { get; set; } = "Horizontal";
    public string ScoreMode { get; set; } = "LiveScores";
    public string PlayerNameMode { get; set; } = "Name";

    public bool ShowHeaderMatchNumber { get; set; } = true;
    public bool ShowHeaderMatchName { get; set; } = true;
    public bool ShowHeaderStartTime { get; set; } = true;
    public bool ShowHeaderStartTimeLock { get; set; }
    public bool ShowHeaderBoardName { get; set; }
    public bool ShowHeaderBoardLock { get; set; }
    public bool ShowHeaderDelay { get; set; }
    public bool ShowHeaderMatchStatus { get; set; } = true;
    public bool ShowHeaderLiveBadge { get; set; } = true;

    public bool ShowBodyStartTime { get; set; } = true;
    public bool ShowBodyStartTimeLock { get; set; }
    public bool ShowBodyDelay { get; set; }
    public bool ShowBodyMatchNumber { get; set; } = true;
    public bool ShowBodyBoardName { get; set; } = true;

    public bool LiveScoreEnabled { get; set; } = true;
    public bool ShowRuntimeBadge { get; set; } = true;
    public bool ShowMonitoringStatus { get; set; } = true;

    public bool ShowActionButtons { get; set; } = true;
    public bool ShowSyncAction { get; set; } = true;
    public bool ShowFollowAction { get; set; } = true;
    public bool ShowStartMatchAction { get; set; } = true;
    public bool ShowResetAction { get; set; }
    public bool ShowListenerAction { get; set; }
    public string ActionButtonsLocation { get; set; } = "Footer";

    public bool EnablePlayerLinks { get; set; } = true;
    public bool EnableBoardLink { get; set; } = true;

    public bool ShowMatchStatus { get; set; } = true;
    public bool ShowSchedulingStatus { get; set; } = true;
    public bool ShowAverage { get; set; }
    public bool ShowHighestPpr { get; set; }
    public bool ShowHighestCheckout { get; set; }
    public bool ShowAverageDartsPerLeg { get; set; }
    public bool ShowMinimalDartsPerLeg { get; set; }

    public bool CollapseDetailsOnMobile { get; set; } = true;
    public bool SuppressHeader { get; set; }

    public MatchCardViewSettings Clone() => new()
    {
        Layout = Layout,
        ScoreMode = ScoreMode,
        PlayerNameMode = PlayerNameMode,
        ShowHeaderMatchNumber = ShowHeaderMatchNumber,
        ShowHeaderMatchName = ShowHeaderMatchName,
        ShowHeaderStartTime = ShowHeaderStartTime,
        ShowHeaderStartTimeLock = ShowHeaderStartTimeLock,
        ShowHeaderBoardName = ShowHeaderBoardName,
        ShowHeaderBoardLock = ShowHeaderBoardLock,
        ShowHeaderDelay = ShowHeaderDelay,
        ShowHeaderMatchStatus = ShowHeaderMatchStatus,
        ShowHeaderLiveBadge = ShowHeaderLiveBadge,
        ShowBodyStartTime = ShowBodyStartTime,
        ShowBodyStartTimeLock = ShowBodyStartTimeLock,
        ShowBodyDelay = ShowBodyDelay,
        ShowBodyMatchNumber = ShowBodyMatchNumber,
        ShowBodyBoardName = ShowBodyBoardName,
        LiveScoreEnabled = LiveScoreEnabled,
        ShowRuntimeBadge = ShowRuntimeBadge,
        ShowMonitoringStatus = ShowMonitoringStatus,
        ShowActionButtons = ShowActionButtons,
        ShowSyncAction = ShowSyncAction,
        ShowFollowAction = ShowFollowAction,
        ShowStartMatchAction = ShowStartMatchAction,
        ShowResetAction = ShowResetAction,
        ShowListenerAction = ShowListenerAction,
        EnablePlayerLinks = EnablePlayerLinks,
        EnableBoardLink = EnableBoardLink,
        ShowMatchStatus = ShowMatchStatus,
        ShowSchedulingStatus = ShowSchedulingStatus,
        ShowAverage = ShowAverage,
        ShowHighestPpr = ShowHighestPpr,
        ShowHighestCheckout = ShowHighestCheckout,
        ShowAverageDartsPerLeg = ShowAverageDartsPerLeg,
        ShowMinimalDartsPerLeg = ShowMinimalDartsPerLeg,
        CollapseDetailsOnMobile = CollapseDetailsOnMobile,
        ActionButtonsLocation = ActionButtonsLocation,
        SuppressHeader = SuppressHeader,
    };

    public void Normalize()
    {
        Layout = NormalizeLayout(Layout);
        ScoreMode = NormalizeScoreMode(ScoreMode);
        PlayerNameMode = NormalizePlayerNameMode(PlayerNameMode);
        ActionButtonsLocation = NormalizeActionButtonsLocation(ActionButtonsLocation);
    }

    public static MatchCardViewSettings CreateDefault()
    {
        var settings = new MatchCardViewSettings();
        settings.Normalize();
        return settings;
    }

    public static string NormalizeLayout(string? layout)
    {
        var normalized = (layout ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "horizontal" => "Horizontal",
            "vertical" => "Vertical",
            "card" => "Card",
            "list" => "List",
            // Backward compatibility for older preferences.
            "mixed" => "Horizontal",
            _ => "Horizontal"
        };
    }

    public static string NormalizeScoreMode(string? scoreMode)
    {
        var normalized = (scoreMode ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "finalonly" => "FinalOnly",
            "currentnopoints" => "CurrentNoPoints",
            "livescores" => "LiveScores",
            _ => "LiveScores"
        };
    }

    public static string NormalizePlayerNameMode(string? playerNameMode)
    {
        var normalized = (playerNameMode ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "name" => "Name",
            "nameandppr" => "NameAndPpr",
            _ => "Name"
        };
    }

    public static string NormalizeActionButtonsLocation(string? location)
    {
        var normalized = (location ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "header" => "Header",
            "footer" => "Footer",
            _ => "Footer"
        };
    }
}

public sealed class MatchCardViewPreferencePayload
{
    public Dictionary<string, MatchCardViewSettings> Views { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
