namespace ddpc.DartSuite.Web.Components;

public sealed class MatchCardViewSettings
{
    private const string ScopeDelimiter = "::";

    private static readonly Dictionary<string, MatchCardViewSettings> ConfiguredDefaultsByScope = new(StringComparer.OrdinalIgnoreCase);

    public string Layout { get; set; } = "Horizontal";
    public string ScoreMode { get; set; } = "LiveScores";
    public string PlayerNameMode { get; set; } = "Name";
    public string SizeLevel { get; set; } = "100%";

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
    public bool ShowBodyMatchName { get; set; }
    public bool ShowBodyBoardName { get; set; } = true;
    public bool ShowBodyBoardLock { get; set; }
    public bool ShowBodyMatchStatus { get; set; }
    public bool ShowBodyLiveBadge { get; set; }

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
    public bool ShowDetailsMatchNumber { get; set; } = true;
    public bool ShowDetailsMatchName { get; set; } = true;
    public bool ShowDetailsStartTime { get; set; } = true;
    public bool ShowDetailsStartTimeLock { get; set; }
    public bool ShowDetailsBoardName { get; set; } = true;
    public bool ShowDetailsBoardLock { get; set; }
    public bool ShowDetailsDelay { get; set; }
    public bool ShowDetailsMatchStatus { get; set; } = true;
    public bool ShowDetailsLiveBadge { get; set; }
    public bool ShowDetailsRuntime { get; set; } = true;
    public bool ShowDetailsMonitoringStatus { get; set; } = true;
    public bool ShowDetailsGameplay { get; set; } = true;
    public bool ShowAverage { get; set; }
    public bool ShowHighestPpr { get; set; }
    public bool ShowHighestCheckout { get; set; }
    public bool ShowAverageDartsPerLeg { get; set; }
    public bool ShowMinimalDartsPerLeg { get; set; }
    public bool ShowMatchPrediction { get; set; }

    public bool EnableHighlighting { get; set; }
    public int HighlightCheckoutThreshold { get; set; } = 60;
    public string StatusBorderPreset { get; set; } = "secondary";
    public string StatusBackgroundPreset { get; set; } = "none";
    public string CreatedBorderPreset { get; set; } = "secondary";
    public string CreatedBackgroundPreset { get; set; } = "none";
    public string PlannedBorderPreset { get; set; } = "secondary";
    public string PlannedBackgroundPreset { get; set; } = "none";
    public string WaitingBorderPreset { get; set; } = "secondary";
    public string WaitingBackgroundPreset { get; set; } = "none";
    public string ActiveBorderPreset { get; set; } = "secondary";
    public string ActiveBackgroundPreset { get; set; } = "none";
    public string CompletedBorderPreset { get; set; } = "secondary";
    public string CompletedBackgroundPreset { get; set; } = "none";
    public string WalkOverBorderPreset { get; set; } = "secondary";
    public string WalkOverBackgroundPreset { get; set; } = "none";
    public string DelayedBorderPreset { get; set; } = "danger";
    public string DelayedBackgroundPreset { get; set; } = "danger-soft";
    public string AheadBorderPreset { get; set; } = "info";
    public string AheadBackgroundPreset { get; set; } = "info-soft";
    public string NoBoardBorderPreset { get; set; } = "dark";
    public string NoBoardBackgroundPreset { get; set; } = "dark-soft";
    public string HighlightPhaseBorderPreset { get; set; } = "warning";
    public string HighlightPhaseBackgroundPreset { get; set; } = "warning-soft";

    public bool CollapseDetailsOnMobile { get; set; } = true;
    public bool SuppressHeader { get; set; }

    public MatchCardViewSettings Clone() => new()
    {
        Layout = Layout,
        ScoreMode = ScoreMode,
        PlayerNameMode = PlayerNameMode,
        SizeLevel = SizeLevel,
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
        ShowBodyMatchName = ShowBodyMatchName,
        ShowBodyBoardName = ShowBodyBoardName,
        ShowBodyBoardLock = ShowBodyBoardLock,
        ShowBodyMatchStatus = ShowBodyMatchStatus,
        ShowBodyLiveBadge = ShowBodyLiveBadge,
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
        ShowDetailsMatchNumber = ShowDetailsMatchNumber,
        ShowDetailsMatchName = ShowDetailsMatchName,
        ShowDetailsStartTime = ShowDetailsStartTime,
        ShowDetailsStartTimeLock = ShowDetailsStartTimeLock,
        ShowDetailsBoardName = ShowDetailsBoardName,
        ShowDetailsBoardLock = ShowDetailsBoardLock,
        ShowDetailsDelay = ShowDetailsDelay,
        ShowDetailsMatchStatus = ShowDetailsMatchStatus,
        ShowDetailsLiveBadge = ShowDetailsLiveBadge,
        ShowDetailsRuntime = ShowDetailsRuntime,
        ShowDetailsMonitoringStatus = ShowDetailsMonitoringStatus,
        ShowDetailsGameplay = ShowDetailsGameplay,
        ShowAverage = ShowAverage,
        ShowHighestPpr = ShowHighestPpr,
        ShowHighestCheckout = ShowHighestCheckout,
        ShowAverageDartsPerLeg = ShowAverageDartsPerLeg,
        ShowMinimalDartsPerLeg = ShowMinimalDartsPerLeg,
        ShowMatchPrediction = ShowMatchPrediction,
        EnableHighlighting = EnableHighlighting,
        HighlightCheckoutThreshold = HighlightCheckoutThreshold,
        StatusBorderPreset = StatusBorderPreset,
        StatusBackgroundPreset = StatusBackgroundPreset,
        CreatedBorderPreset = CreatedBorderPreset,
        CreatedBackgroundPreset = CreatedBackgroundPreset,
        PlannedBorderPreset = PlannedBorderPreset,
        PlannedBackgroundPreset = PlannedBackgroundPreset,
        WaitingBorderPreset = WaitingBorderPreset,
        WaitingBackgroundPreset = WaitingBackgroundPreset,
        ActiveBorderPreset = ActiveBorderPreset,
        ActiveBackgroundPreset = ActiveBackgroundPreset,
        CompletedBorderPreset = CompletedBorderPreset,
        CompletedBackgroundPreset = CompletedBackgroundPreset,
        WalkOverBorderPreset = WalkOverBorderPreset,
        WalkOverBackgroundPreset = WalkOverBackgroundPreset,
        DelayedBorderPreset = DelayedBorderPreset,
        DelayedBackgroundPreset = DelayedBackgroundPreset,
        AheadBorderPreset = AheadBorderPreset,
        AheadBackgroundPreset = AheadBackgroundPreset,
        NoBoardBorderPreset = NoBoardBorderPreset,
        NoBoardBackgroundPreset = NoBoardBackgroundPreset,
        HighlightPhaseBorderPreset = HighlightPhaseBorderPreset,
        HighlightPhaseBackgroundPreset = HighlightPhaseBackgroundPreset,
        CollapseDetailsOnMobile = CollapseDetailsOnMobile,
        ActionButtonsLocation = ActionButtonsLocation,
        SuppressHeader = SuppressHeader,
    };

    public void Normalize()
    {
        Layout = NormalizeLayout(Layout);
        ScoreMode = NormalizeScoreMode(ScoreMode);
        PlayerNameMode = NormalizePlayerNameMode(PlayerNameMode);
        SizeLevel = NormalizeSizeLevel(SizeLevel);
        ActionButtonsLocation = NormalizeActionButtonsLocation(ActionButtonsLocation);
        HighlightCheckoutThreshold = Math.Clamp(HighlightCheckoutThreshold, 1, 501);
        StatusBorderPreset = NormalizeHighlightPreset(StatusBorderPreset);
        StatusBackgroundPreset = NormalizeHighlightPreset(StatusBackgroundPreset);
        CreatedBorderPreset = NormalizeHighlightPreset(CreatedBorderPreset);
        CreatedBackgroundPreset = NormalizeHighlightPreset(CreatedBackgroundPreset);
        PlannedBorderPreset = NormalizeHighlightPreset(PlannedBorderPreset);
        PlannedBackgroundPreset = NormalizeHighlightPreset(PlannedBackgroundPreset);
        WaitingBorderPreset = NormalizeHighlightPreset(WaitingBorderPreset);
        WaitingBackgroundPreset = NormalizeHighlightPreset(WaitingBackgroundPreset);
        ActiveBorderPreset = NormalizeHighlightPreset(ActiveBorderPreset);
        ActiveBackgroundPreset = NormalizeHighlightPreset(ActiveBackgroundPreset);
        CompletedBorderPreset = NormalizeHighlightPreset(CompletedBorderPreset);
        CompletedBackgroundPreset = NormalizeHighlightPreset(CompletedBackgroundPreset);
        WalkOverBorderPreset = NormalizeHighlightPreset(WalkOverBorderPreset);
        WalkOverBackgroundPreset = NormalizeHighlightPreset(WalkOverBackgroundPreset);
        DelayedBorderPreset = NormalizeHighlightPreset(DelayedBorderPreset);
        DelayedBackgroundPreset = NormalizeHighlightPreset(DelayedBackgroundPreset);
        AheadBorderPreset = NormalizeHighlightPreset(AheadBorderPreset);
        AheadBackgroundPreset = NormalizeHighlightPreset(AheadBackgroundPreset);
        NoBoardBorderPreset = NormalizeHighlightPreset(NoBoardBorderPreset);
        NoBoardBackgroundPreset = NormalizeHighlightPreset(NoBoardBackgroundPreset);
        HighlightPhaseBorderPreset = NormalizeHighlightPreset(HighlightPhaseBorderPreset);
        HighlightPhaseBackgroundPreset = NormalizeHighlightPreset(HighlightPhaseBackgroundPreset);
    }

    public static MatchCardViewSettings CreateDefault(string? scopeKey = null)
    {
        if (ConfiguredDefaultsByScope.Count > 0)
        {
            var resolved = ResolveConfiguredDefault(scopeKey);
            if (resolved is not null)
                return resolved.Clone();
        }

        var settings = new MatchCardViewSettings();
        settings.Normalize();
        return settings;
    }

    public static void ConfigureDefaults(IDictionary<string, MatchCardViewSettings>? defaultsByScope)
    {
        ConfiguredDefaultsByScope.Clear();
        if (defaultsByScope is null || defaultsByScope.Count == 0)
            return;

        foreach (var (scope, configured) in defaultsByScope)
        {
            if (string.IsNullOrWhiteSpace(scope) || configured is null)
                continue;

            var normalizedScope = NormalizeScopeKey(scope);
            var cloned = configured.Clone();
            cloned.Normalize();
            ConfiguredDefaultsByScope[normalizedScope] = cloned;
        }
    }

    public static bool IsExplicitlyConfiguredScope(string? scopeKey)
    {
        var normalizedScope = NormalizeScopeKey(scopeKey);
        return ConfiguredDefaultsByScope.ContainsKey(normalizedScope);
    }

    public static IEnumerable<string> GetExplicitlyConfiguredScopes()
        => ConfiguredDefaultsByScope.Keys;

    private static MatchCardViewSettings? ResolveConfiguredDefault(string? scopeKey)
    {
        var normalizedScope = NormalizeScopeKey(scopeKey);
        var candidates = new List<string> { normalizedScope };

        if (TryParseScopeKey(normalizedScope, out var page, out var section))
        {
            if (section.StartsWith("schedule-", StringComparison.OrdinalIgnoreCase))
                candidates.Add(BuildScopeKey(page, "schedule"));

            if (!string.Equals(section, "all", StringComparison.OrdinalIgnoreCase))
                candidates.Add(BuildScopeKey(page, "all"));
        }

        candidates.Add(BuildScopeKey("all", "all"));

        foreach (var candidate in candidates)
        {
            if (ConfiguredDefaultsByScope.TryGetValue(candidate, out var configured))
                return configured;
        }

        return null;
    }

    private static string NormalizeScopeKey(string? scopeKey)
    {
        if (!TryParseScopeKey(scopeKey, out var page, out var section))
            return BuildScopeKey("all", "all");

        return BuildScopeKey(page, section);
    }

    private static bool TryParseScopeKey(string? scopeKey, out string page, out string section)
    {
        page = "all";
        section = "all";

        if (string.IsNullOrWhiteSpace(scopeKey))
            return false;

        var split = scopeKey.Split(ScopeDelimiter, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (split.Length != 2)
            return false;

        page = string.IsNullOrWhiteSpace(split[0]) ? "all" : split[0].Trim().ToLowerInvariant();
        section = NormalizeScopeSection(split[1]);
        return true;
    }

    private static string BuildScopeKey(string page, string section)
        => $"{(string.IsNullOrWhiteSpace(page) ? "all" : page.Trim().ToLowerInvariant())}{ScopeDelimiter}{NormalizeScopeSection(section)}";

    private static string NormalizeScopeSection(string? section)
    {
        var normalized = (section ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "all" => "all",
            "general" => "general",
            "groups" => "groups",
            "knockout" => "knockout",
            "schedule" => "schedule",
            "schedule-boards-upcoming" => "schedule-boards-upcoming",
            "schedule-zeitplan" => "schedule-zeitplan",
            "schedule-queue" => "schedule-boards-upcoming",
            "schedule-timeline" => "schedule-zeitplan",
            "board-detail" => "board-detail",
            "detail" => "detail",
            _ => "all"
        };
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

    public static string NormalizeSizeLevel(string? sizeLevel)
    {
        var normalized = (sizeLevel ?? string.Empty).Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        return normalized switch
        {
            "50" or "50%" => "50%",
            "75" or "75%" => "75%",
            "100" or "100%" => "100%",
            "150" or "150%" => "150%",
            "200" or "200%" => "200%",
            "300" or "300%" => "300%",
            _ => "100%"
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

    public static string NormalizeHighlightPreset(string? preset)
    {
        var normalized = (preset ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "none" => "none",
            "primary" => "primary",
            "secondary" => "secondary",
            "success" => "success",
            "info" => "info",
            "warning" => "warning",
            "danger" => "danger",
            "dark" => "dark",
            "primary-soft" => "primary-soft",
            "secondary-soft" => "secondary-soft",
            "success-soft" => "success-soft",
            "info-soft" => "info-soft",
            "warning-soft" => "warning-soft",
            "danger-soft" => "danger-soft",
            "dark-soft" => "dark-soft",
            _ => "none"
        };
    }
}

public sealed class MatchCardViewPreferencePayload
{
    public Dictionary<string, MatchCardViewSettings> Views { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> DisabledScopes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MatchCardDefaultsOptions
{
    public Dictionary<string, MatchCardViewSettings> Scopes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
