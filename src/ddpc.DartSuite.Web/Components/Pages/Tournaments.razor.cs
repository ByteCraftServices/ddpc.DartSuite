using ddpc.DartSuite.Application.Contracts.Boards;
using ddpc.DartSuite.Application.Contracts.Matches;
using ddpc.DartSuite.Application.Contracts.Tournaments;
using ddpc.DartSuite.Web.Components;
using ddpc.DartSuite.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Hosting;
using Microsoft.JSInterop;
using System.Diagnostics;
using System.Text.Json;

namespace ddpc.DartSuite.Web.Components.Pages;

public partial class Tournaments : IAsyncDisposable
{
    [Inject] private DartSuiteApiService Api { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private AppStateService AppState { get; set; } = default!;
    [Inject] private BoardHubService BoardHubService { get; set; } = default!;
    [Inject] private TournamentHubService HubService { get; set; } = default!;
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "matchId")]
    public string? QueryMatchId { get; set; }

    [SupplyParameterFromQuery(Name = "boardId")]
    public string? QueryBoardId { get; set; }

    [SupplyParameterFromQuery(Name = "tab")]
    public string? QueryTab { get; set; }

    [SupplyParameterFromQuery(Name = "tournamentId")]
    public string? QueryTournamentId { get; set; }

    private Timer? _autoRefreshTimer;
    private int _autoRefreshInProgress;
    private bool isTournamentHubConnected;
    private bool isBoardHubConnected;
    private int fallbackPollingIntervalSeconds = 5;
    private bool isSavingRealtimePreference;
    private string? realtimePreferenceError;
    private bool isCompactViewport;
    private bool? forcedMatchDetailsExpanded;
    private bool tournamentListCollapsed = true;
    private bool tournamentListMobileOpen;
    private const string TournamentListCollapsedStorageKey = "ds-tournaments-list-collapsed";
    private bool _detailsExpandedLoadedFromStorage;
    private bool isSavingMatchCardPreferences;
    private string? matchCardPreferenceError;
    private string? tournamentHubConnectionError;
    private string? boardHubConnectionError;
    private readonly Dictionary<string, MatchCardViewSettings> matchCardSettingsByView = new(StringComparer.OrdinalIgnoreCase);

    // ─── Data State ───
    private List<TournamentDto> tournaments = [];
    private List<BoardDto> boards = [];
    private List<ParticipantDto> participants = [];
    private List<TeamDto> teams = [];
    private List<MatchDto> matches = [];
    private List<TournamentRoundDto> roundSettings = [];
    private List<GroupStandingDto> groupStandings = [];
    private TournamentDto? selectedTournament;
    private string activeTab = "general";
    private string activeBoardsParticipantsSubTab = "spieler";
    private bool isWorking;
    private bool showAdminTeamMembers;
    private bool showStatusDropdown;
    private bool showPageSettingsDropdown;
    private bool showPageSettingsModal;
    private bool showMatchCardScopeModal;
    private bool showLegacyMatchCardPanel = false;
    private bool showInactiveActionInfoModal;
    private string inactiveActionInfoTitle = "Aktion derzeit nicht verfügbar";
    private string inactiveActionInfoMessage = string.Empty;
    private string matchCardScopeModalKey = string.Empty;
    private DotNetObjectReference<Tournaments>? tournamentTabSwipeRef;
    private DotNetObjectReference<Tournaments>? teamDraftUiRef;
    private bool hasCompletedInitialLoad;
    private string? lastHandledQueryTab;
    private string? lastHandledQueryTournamentId;

    // ─── Create Wizard ───
    private bool isCreating;
    private int wizardStep = 1;
    private string wizardName = string.Empty;
    private string wizardOrganizer = "manager";
    private DateTime wizardStartDate = DateTime.Today;
    private DateTime? wizardEndDate;
    private string? wizardStartTime;
    private string wizardMode = "Knockout";
    private string wizardVariant = "OnSite";
    private bool wizardTeamplay;
    private string? wizardError;

    // ─── Edit State ───
    private string editName = string.Empty;
    private string editOrganizer = string.Empty;
    private DateTime editStartDate;
    private DateTime editEndDate;
    private string? editStartTime;
    private string editMode = "Knockout";
    private string editVariant = "OnSite";
    private bool editTeamplay;
    private bool editThirdPlaceMatch;
    private int editGroupCount = 2;
    private int editPlayoffAdvancers = 2;
    private int editKnockoutsPerRound = 1;
    private int editMatchesPerOpponent = 1;
    private string editGroupMode = "RoundRobin";
    private string editGroupDrawMode = "Random";
    private string editPlanningVariant = "RoundByRound";
    private string editGroupOrderMode = "ReverseEachRound";
    private int editWinPoints = 2;
    private int editLegFactor = 1;
    private readonly List<ScoringCriterionEditorItem> scoringCriteria = [];
    private bool isSavingScoringCriteria;
    private string? scoringCriteriaError;
    private int editPlayersPerTeam = 1;
    private bool editIsRegistrationOpen;
    private DateTime? editRegistrationStart;
    private DateTime? editRegistrationEnd;
    private string? editError;
    private string? editSuccess;

    // ─── Participant State ───
    private string participantName = string.Empty;
    private string participantAccount = string.Empty;
    private bool participantIsAutodarts = true;
    private bool participantIsManager;
    private string? participantError;
    private List<ParticipantDto> participantSuggestions = [];
    private bool showParticipantSuggestions;

    // ─── Participant Edit Modal ───
    private ParticipantDto? editingParticipant;
    private string editPDisplayName = string.Empty;
    private string editPAccountName = string.Empty;
    private bool editPIsAutodarts;
    private bool editPIsManager;
    private string? editPError;

    // ─── Board Drag-Drop ───
    private Guid? draggedBoardId;
    private Guid? dropTargetMatchId;
    private Guid? draggedScheduleMatchId;
    private Guid? dropTargetScheduleMatchId;
    private Guid? dropTargetScheduleBoardId;
    private string? activeScheduleInsertTargetKey;

    // ─── Match Detail / Result Edit Modal ───
    private MatchDto? detailMatch;
    private bool _initDetailSections;
    private int editHomeLegs;
    private int editAwayLegs;
    private int editHomeSets;
    private int editAwaySets;
    private string? resultError;
    private bool showWalkoverConfirm;
    private string walkoverWinnerId = string.Empty;
    private bool isSyncing;
    private bool detailMatchOpenedFromSchedule;

    // ─── Match Listeners ───
    private List<MatchListenerInfoDto> matchListeners = [];
    private readonly HashSet<Guid> _dismissedListenerErrors = new();
    private readonly Dictionary<Guid, DateTimeOffset> lastMatchDataEventUtcByMatch = [];
    private readonly Dictionary<Guid, DateTimeOffset> lastStatisticsEventUtcByMatch = [];
    private bool hasPendingVisibilityRefresh;

    // ─── Round Settings Editor ───
    private string newRoundPhase = "Knockout";
    private int newRoundNumber = 1;
    private int newRoundBaseScore = 501;
    private int newRoundLegs = 3;
    private string newRoundOutMode = "Double";
    private string newRoundInMode = "Straight";
    private string newRoundGameMode = "Legs";
    private int? newRoundSets;
    private int newRoundMaxRounds = 50;
    private string newRoundBullMode = "25/50";
    private string newRoundBullOffMode = "Normal";
    private int newRoundDuration;
    private int newRoundPause;
    private int newRoundPlayerPause;
    private string newRoundBoardAssignment = "Dynamic";
    private string? roundError;

    // ─── Confirmation Dialog ───
    private bool showConfirmation;
    private bool showConfirmationPlanImpact = true;
    private string confirmationMessage = string.Empty;
    private Func<Task>? confirmationAction;

    // ─── Registration-Draw Confirmation Dialog ───
    private bool showRegistrationDrawConfirmation;
    private Func<Task>? registrationDrawContinuation;

    // ─── Spielplan: Collapsed Groups ───
    private bool showGroupMatches;
    private string groupMatchesViewMode = "vertical"; // vertical | horizontal
    private bool hasLoadedGroupMatchesViewMode;

    // ─── Spielplan: Filters ───
    private bool scheduleHideFinished;
    private bool scheduleShowNoBoard;
    private string scheduleStatusFilter = "all"; // all | running | upcoming

    // ─── Board Detail Modal ───
    private BoardDto? detailBoard;
    private string? boardSyncInfo;
    private string? boardSyncError;
    private DartSuiteApiService.BoardExtensionSyncDebugDto? boardSyncDebug;
    private bool isBoardSyncDebugLoading;

    // ─── Player Detail Modal ───
    private ParticipantDto? detailParticipant;
    private string playerDetailTab = "info";

    // ─── Dialog Navigation Stack ───
    private readonly Stack<Action> _modalBackStack = new();

    // ─── Game Mode Lock ───
    private bool editAreGameModesLocked;

    // ─── KO View Mode ───
    private string koViewMode = "tree"; // tree | round | live

    // ─── Discord Webhook (#14) ───
    private string? editDiscordWebhookUrl;
    private string? editDiscordWebhookDisplayText;
    private string? discordTestResult;
    private bool discordTestSuccess;

    // ─── Seeding (#13) ───
    private bool editSeedingEnabled;
    private int editSeedTopCount;

    // ─── Match Statistics (#18) ───
    private List<MatchPlayerStatisticDto> detailMatchStatistics = [];
    private bool isLoadingStats;

    // ─── Match Followers (#14) ───
    private bool isFollowingDetailMatch;
    private readonly Dictionary<Guid, bool> followedMatchStatesById = [];
    private readonly HashSet<Guid> followOperationInProgressMatchIds = [];

    // ─── Blitztabelle ───
    private bool showFlashTable;

    // ─── Match Schedule Editing ───
    private Guid? editingMatchTimeId;
    private string editMatchTimeValue = string.Empty;

    // ─── Round Detail Modal ───
    private TournamentRoundDto? detailRound;
    private List<TournamentRoundDto> detailRoundGroup = [];

    // ─── Completed Rounds Filter ───
    private bool showCompletedRounds;

    // ─── Time helpers for <input type="time"> ───
    private TimeOnly? wizardStartTimeInput
    {
        get => string.IsNullOrEmpty(wizardStartTime) ? null : TimeOnly.TryParse(wizardStartTime, out var t) ? t : null;
        set => wizardStartTime = value?.ToString("HH:mm");
    }

    private TimeOnly? editStartTimeInput
    {
        get => string.IsNullOrEmpty(editStartTime) ? null : TimeOnly.TryParse(editStartTime, out var t) ? t : null;
        set => editStartTime = value?.ToString("HH:mm");
    }

    // ─── Computed ───
    private IEnumerable<TournamentDto> ActiveTournaments =>
        tournaments.Where(t => t.StartDate <= DateOnly.FromDateTime(DateTime.Today) && t.EndDate >= DateOnly.FromDateTime(DateTime.Today));

    private IEnumerable<TournamentDto> UpcomingTournaments =>
        tournaments.Where(t => t.StartDate > DateOnly.FromDateTime(DateTime.Today));

    private IEnumerable<TournamentDto> PastTournaments =>
        tournaments.Where(t => t.EndDate < DateOnly.FromDateTime(DateTime.Today));

    private IReadOnlyList<TournamentDto> TournamentStripItems =>
        [.. ActiveTournaments, .. UpcomingTournaments, .. PastTournaments];

    private int CurrentTournamentStripIndex
    {
        get
        {
            if (selectedTournament is null)
                return -1;

            var items = TournamentStripItems;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Id == selectedTournament.Id)
                    return i;
            }

            return -1;
        }
    }

    private bool CanSelectPreviousTournament => CurrentTournamentStripIndex > 0;

    private bool CanSelectNextTournament
    {
        get
        {
            var index = CurrentTournamentStripIndex;
            return index >= 0 && index < TournamentStripItems.Count - 1;
        }
    }

    private static string GetTournamentRailItemHeight(TournamentDto tournament)
    {
        var nameLength = tournament.Name?.Length ?? 0;
        var heightRem = Math.Clamp(2.8 + (nameLength * 0.24), 4.2, 11.0);
        return $"{heightRem:0.##}rem";
    }

    // ─── Autodarts Session ───
    private bool isAutodartsConnected;
    private string? autodartsDisplayName;
    private bool IsDevelopmentEnvironment => HostEnvironment.IsDevelopment();

    private sealed record TournamentRealtimeSettings(int FallbackPollingSeconds = 5);
    private sealed record MatchCardScopeOption(string ScopeKey, string Label);

    private const string RealtimePreferenceContext = "TournamentRealtime";
    private const string MatchCardPreferenceContext = "MatchCardDisplay";
    private const string MatchCardScopeDelimiter = "::";
    private const string MatchCardScopeGlobalPage = "all";
    private const string MatchCardScopePage = "tournaments";
    private const string MatchCardScopeSectionAll = "all";
    private const string MatchCardSectionGeneral = "general";
    private const string MatchCardSectionGroups = "groups";
    private const string MatchCardSectionKnockout = "knockout";
    private const string MatchCardSectionSchedule = "schedule";
    private const string MatchCardSectionScheduleBoardsUpcoming = "schedule-boards-upcoming";
    private const string MatchCardSectionScheduleZeitplan = "schedule-zeitplan";
    private const string MatchCardSectionScheduleQueueLegacy = "schedule-queue";
    private const string MatchCardSectionScheduleTimelineLegacy = "schedule-timeline";
    private const string MatchCardSectionBoardDetail = "board-detail";

    private static readonly IReadOnlyList<MatchCardScopeOption> MatchCardScopeOptions =
    [
        new(BuildMatchCardScopeKey(MatchCardScopeGlobalPage, MatchCardScopeSectionAll), "Alle Seiten / Alle Sektionen"),
        new(BuildMatchCardScopeKey(MatchCardScopePage, MatchCardScopeSectionAll), "Turniere / Alle Sektionen"),
        new(BuildMatchCardScopeKey(MatchCardScopePage, MatchCardSectionGeneral), "Turniere / Allgemein"),
        new(BuildMatchCardScopeKey(MatchCardScopePage, MatchCardSectionGroups), "Turniere / Gruppen"),
        new(BuildMatchCardScopeKey(MatchCardScopePage, MatchCardSectionKnockout), "Turniere / K.O."),
        new(BuildMatchCardScopeKey(MatchCardScopePage, MatchCardSectionSchedule), "Turniere / Spielplan (alle)"),
        new(BuildMatchCardScopeKey(MatchCardScopePage, MatchCardSectionScheduleBoardsUpcoming), "Turniere / Spielplan Boards & Anstehende Matches"),
        new(BuildMatchCardScopeKey(MatchCardScopePage, MatchCardSectionScheduleZeitplan), "Turniere / Spielplan Zeitplan"),
        new(BuildMatchCardScopeKey(MatchCardScopePage, MatchCardSectionBoardDetail), "Turniere / Board-Detail")
    ];
    private static readonly IReadOnlyList<string> AllScoringCriterionTypes =
    [
        "Points",
        "DirectDuel",
        "LegDifference",
        "WonLegs",
        "Average",
        "HighestAverage",
        "HighestCheckout",
        "AverageDartsPerLeg",
        "CheckoutPercentage",
        "Breaks",
        "LotDraw"
    ];

    private static readonly HashSet<string> DefaultEnabledScoringCriteria =
    [
        "Points",
        "DirectDuel"
    ];

    /// <summary>True if the current user has manager-level rights for the selected tournament.</summary>
    private bool IsCurrentUserManager =>
        AppState.IsAdmin
        || selectedTournament is not null && autodartsDisplayName is not null &&
        (string.Equals(selectedTournament.OrganizerAccount, autodartsDisplayName, StringComparison.OrdinalIgnoreCase)
         || participants.Any(p => p.IsManager && string.Equals(p.AccountName, autodartsDisplayName, StringComparison.OrdinalIgnoreCase))
         || participants.Any(p => p.IsManager && string.Equals(p.DisplayName, autodartsDisplayName, StringComparison.OrdinalIgnoreCase)));

    private string CurrentUserTournamentRoleLabel
        => AppState.IsAdmin
            ? "Admin"
            : IsCurrentUserManager
                ? "Spielleiter"
                : "Teilnehmer";

    private MatchListenerInfoDto? GetMatchListener(Guid matchId)
        => matchListeners.FirstOrDefault(l => l.MatchId == matchId);

    private bool IsMatchMonitored(MatchDto match)
    {
        var listener = GetMatchListener(match.Id);
        return MatchCardUiPolicy.IsMonitored(listener);
    }

    private string GetMonitoringBadgeText(MatchDto match)
    {
        var listener = GetMatchListener(match.Id);
        return MatchCardUiPolicy.MonitoringBadgeText(listener);
    }

    private string GetMonitoringBadgeCss(MatchDto match)
    {
        var listener = GetMatchListener(match.Id);
        return MatchCardUiPolicy.MonitoringBadgeCss(listener);
    }

    private bool CanStartListener(MatchDto match)
        => IsCurrentUserManager
           && match.StartedUtc is not null
           && match.FinishedUtc is null
           && !string.IsNullOrWhiteSpace(match.ExternalMatchId)
           && !IsMatchMonitored(match);

    private async Task ReconcileTournamentMonitoringForViewAsync()
    {
        if (!IsCurrentUserManager || selectedTournament is null)
            return;

        try
        {
            await Api.ReconcileMatchMonitoringAsync(selectedTournament.Id);
            await LoadMatchListenersAsync();
        }
        catch
        {
            // Monitoring reconciliation is best-effort.
        }
    }

    private async Task ReconcileBoardMonitoringForViewAsync(Guid boardId)
    {
        if (!IsCurrentUserManager)
            return;

        try
        {
            await Api.ReconcileBoardMonitoringAsync(boardId);
            await LoadMatchListenersAsync();
        }
        catch
        {
            // Monitoring reconciliation is best-effort.
        }
    }

    private string activeMatchCardConfigScopeKey = BuildMatchCardScopeKey(MatchCardScopePage, MatchCardScopeSectionAll);

    private string ActiveMatchCardContextSection => activeTab switch
    {
        "groups" => MatchCardSectionGroups,
        "knockout" => MatchCardSectionKnockout,
        "schedule" => MatchCardSectionSchedule,
        _ => MatchCardSectionGeneral
    };

    private string ActiveMatchCardContextScopeKey => BuildMatchCardScopeKey(MatchCardScopePage, ActiveMatchCardContextSection);

    private string ActiveMatchCardContextLabel => MatchCardScopeOptions
        .FirstOrDefault(x => x.ScopeKey == ActiveMatchCardContextScopeKey)
        ?.Label ?? ActiveMatchCardContextScopeKey;

    private string ActiveMatchCardConfigScopeLabel => MatchCardScopeOptions
        .FirstOrDefault(x => x.ScopeKey == activeMatchCardConfigScopeKey)
        ?.Label ?? activeMatchCardConfigScopeKey;

    private bool RequiresBoardRealtime => activeTab is "boards" or "participants" or "schedule" || detailBoard is not null;

    private bool IsRealtimeFallbackActive => !isTournamentHubConnected || (RequiresBoardRealtime && !isBoardHubConnected);

    private string RealtimeFallbackReason
    {
        get
        {
            if (!isTournamentHubConnected && RequiresBoardRealtime && !isBoardHubConnected)
                return "Turnier-Hub und benoetigter Board-Hub sind getrennt";
            if (!isTournamentHubConnected)
                return string.IsNullOrWhiteSpace(tournamentHubConnectionError)
                    ? "Turnier-Hub ist getrennt"
                    : $"Turnier-Hub ist getrennt ({tournamentHubConnectionError})";
            if (RequiresBoardRealtime && !isBoardHubConnected)
                return string.IsNullOrWhiteSpace(boardHubConnectionError)
                    ? "Fuer diese Ansicht ist der Board-Hub getrennt"
                    : $"Fuer diese Ansicht ist der Board-Hub getrennt ({boardHubConnectionError})";
            return "Fallback aktiv";
        }
    }

    private MatchCardViewSettings ActiveMatchCardSettings => GetMatchCardSettings(ActiveMatchCardContextSection);
    private MatchCardViewSettings ActiveMatchCardConfigSettings => GetEditableMatchCardSettings(activeMatchCardConfigScopeKey);
    private MatchCardViewSettings BoardDetailMatchCardSettings => GetMatchCardSettings(MatchCardSectionBoardDetail);

    private MatchCardViewSettings MatchCardSettingsForMatchList(MatchDto match)
    {
        if (activeTab == "schedule")
            return GetMatchCardSettings(MatchCardSectionScheduleZeitplan);

        if (activeTab == "groups" || string.Equals(match.Phase, "Group", StringComparison.OrdinalIgnoreCase))
            return GetMatchCardSettings(MatchCardSectionGroups);

        if (activeTab == "knockout" || string.Equals(match.Phase, "Knockout", StringComparison.OrdinalIgnoreCase))
            return GetMatchCardSettings(MatchCardSectionKnockout);

        return ActiveMatchCardSettings;
    }

    private bool MatchCardDetailsExpandedByDefault(MatchCardViewSettings settings)
        => !settings.CollapseDetailsOnMobile || !isCompactViewport;

    private string? MatchBoardName(MatchDto match)
        => boards.FirstOrDefault(b => b.Id == match.BoardId)?.Name;

    private bool ShowMatchCardMonitoring(MatchCardViewSettings settings)
        => MatchCardUiPolicy.ShowMonitoring(settings, IsCurrentUserManager);

    private bool ShowMatchCardActionBar(MatchCardViewSettings settings)
        => MatchCardUiPolicy.ShowActionBar(settings);

    private bool ShowMatchCardSyncAction(MatchCardViewSettings settings, MatchDto match)
        => MatchCardUiPolicy.ShowSyncAction(settings, IsCurrentUserManager, match.ExternalMatchId);

    private bool ShowMatchCardFollowAction(MatchCardViewSettings settings)
        => MatchCardUiPolicy.ShowFollowActionForUser(settings, isAutodartsConnected, autodartsDisplayName);

    private bool CanSyncFromMatchCard(MatchDto match)
        => MatchCardUiPolicy.CanSync(IsCurrentUserManager, isSyncing, isWorking, match.ExternalMatchId);

    private bool CanFollowFromMatchCard(MatchDto match)
        => MatchCardUiPolicy.CanFollowForUser(isAutodartsConnected, autodartsDisplayName, IsFollowOperationBusy(match.Id));

    private EventCallback BuildPlayerDetailCallback(Guid? participantId)
    {
        if (!MatchCardUiPolicy.CanOpenParticipant(participantId))
            return default;

        return EventCallback.Factory.Create(this, () => OpenPlayerDetail(participantId));
    }

    private EventCallback BuildMatchBoardDetailCallback(MatchDto match)
    {
        if (!MatchCardUiPolicy.CanOpenBoard(match.BoardId))
            return default;

        return EventCallback.Factory.Create(this, () => OpenBoardDetailFromMatch(match));
    }

    private EventCallback BuildBoardDetailCallback(Guid? boardId)
    {
        if (!MatchCardUiPolicy.CanOpenBoard(boardId))
            return default;

        return EventCallback.Factory.Create(this, () =>
        {
            var board = boards.FirstOrDefault(b => b.Id == boardId!.Value);
            if (board is not null)
                OpenBoardDetail(board);
        });
    }

    private Task OpenBoardDetailFromMatch(MatchDto match)
    {
        if (!match.BoardId.HasValue)
            return Task.CompletedTask;

        var board = boards.FirstOrDefault(b => b.Id == match.BoardId.Value);
        if (board is null)
            return Task.CompletedTask;

        OpenBoardDetail(board);
        return Task.CompletedTask;
    }

    private int EffectiveFallbackPollingIntervalSeconds => Math.Clamp(fallbackPollingIntervalSeconds, 2, 30);

    private bool IsMatchScoreVisible(Guid matchId)
    {
        if (detailMatch?.Id == matchId)
            return true;

        if (detailBoard?.CurrentMatchId == matchId)
            return true;

        var match = matches.FirstOrDefault(m => m.Id == matchId);
        if (match is null)
            return false;

        return activeTab switch
        {
            "schedule" => true,
            "groups" => match.Phase == "Group",
            "knockout" => match.Phase == "Knockout",
            _ => false
        };
    }

    private bool IsMatchStatisticsVisible(Guid matchId)
        => detailMatch?.Id == matchId;

    private const string TournamentStructureLockedMessage = "Die Turnierstruktur ist gesperrt, sobald ein Match gestartet oder beendet wurde.";

    private bool CanEditTournamentSettings => selectedTournament is not null && !selectedTournament.IsLocked && IsCurrentUserManager;

    /// <summary>Managers can edit basic settings while tournament is unlocked.</summary>
    private bool CanEditBasicSettings => CanEditTournamentSettings;

    private static bool IsWalkOverMatch(MatchDto match)
        => string.Equals(match.Status, "WalkOver", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when at least one non-walkover match has started or finished.</summary>
    private bool HasProgressedMatches => matches.Any(m => !IsWalkOverMatch(m) && (m.StartedUtc is not null || m.FinishedUtc is not null));

    /// <summary>Returns true when the selected tournament has its registration currently open.</summary>
    private bool IsRegistrationOpen => selectedTournament?.IsRegistrationOpen == true;

    /// <summary>Structure edits are allowed only before first active/finished match and while status is planned/created.</summary>
    private bool CanEditTournamentStructure =>
        CanEditTournamentSettings
        && selectedTournament?.Status is "Erstellt" or "Geplant"
        && !HasProgressedMatches;

    /// <summary>Human-readable reason why structure editing is disabled, for tooltip display.</summary>
    private string CannotEditStructureReason =>
        CanEditTournamentStructure ? string.Empty
        : !IsCurrentUserManager ? "Nur Spielleiter können die Turnierstruktur bearbeiten."
        : selectedTournament?.IsLocked == true ? "Turnier ist gesperrt."
        : selectedTournament?.Status is not ("Erstellt" or "Geplant") ? "Struktur ist nur änderbar wenn das Turnier noch nicht gestartet wurde."
        : HasProgressedMatches ? "Es gibt bereits gespielte Matches – Struktur kann nicht mehr geändert werden."
        : "Aktion derzeit nicht verfügbar.";

    private string DrawCreatePlanDisabledReason =>
        !CanEditTournamentStructure ? CannotEditStructureReason
        : selectedTournament?.Mode == "GroupAndKnockout" && UnassignedParticipants.Count > 0
            ? $"Alle {(IsTeamplayActive ? "Teams" : "Teilnehmer")} müssen einer Gruppe zugewiesen sein, bevor der Turnierplan erstellt werden kann. Noch {UnassignedParticipants.Count} nicht zugewiesen."
        : selectedTournament?.Mode == "Knockout" && !IsKnockoutDrawComplete
            ? $"Für die K.O.-Auslosung müssen alle {(IsTeamplayActive ? "Teams" : "Teilnehmer")} einem Match-Slot zugewiesen werden."
        : !CanProceedWithTeamDraw ? "Turnierplan kann erst erstellt werden, wenn die Teamzuordnung vollständig und gespeichert ist."
        : string.Empty;

    private void ShowInactiveActionInfo(string reason, string? title = null)
    {
        inactiveActionInfoTitle = string.IsNullOrWhiteSpace(title) ? "Aktion derzeit nicht verfügbar" : title;
        inactiveActionInfoMessage = string.IsNullOrWhiteSpace(reason)
            ? "Diese Aktion kann aktuell nicht ausgeführt werden."
            : reason;
        showInactiveActionInfoModal = true;
        _ = InvokeAsync(StateHasChanged);
    }

    private Task ShowInactiveActionInfoAsync(string reason, string? title = null)
    {
        ShowInactiveActionInfo(reason, title);
        return Task.CompletedTask;
    }

    private void CloseInactiveActionInfo()
    {
        showInactiveActionInfoModal = false;
    }

    /// <summary>Can edit participant-related settings (teamplay, seeding, registration): Status ≤ Geplant AND no plan.</summary>
    private bool CanEditParticipantSettings =>
        CanEditBasicSettings && !matches.Any();

    /// <summary>Can edit draw/group config only while tournament structure is editable.</summary>
    private bool CanEditGroupConfig =>
        CanEditTournamentStructure;

    /// <summary>Can edit draw mode only while tournament structure is editable.</summary>
    private bool CanEditDrawMode =>
        CanEditTournamentStructure;

    /// <summary>Can edit scoring only while tournament structure is editable.</summary>
    private bool CanEditScoring =>
        CanEditTournamentSettings
        && string.Equals(selectedTournament?.Mode, "GroupAndKnockout", StringComparison.OrdinalIgnoreCase)
        && !HasStartedGroupMatch;

    private bool HasStartedGroupMatch => matches.Any(m =>
        string.Equals(m.Phase, "Group", StringComparison.OrdinalIgnoreCase)
        && !IsWalkOverMatch(m)
        && (m.StartedUtc is not null || m.FinishedUtc is not null));

    private string EffectiveGroupMatchesViewMode
        => isCompactViewport ? "vertical" : groupMatchesViewMode;

    private bool EnsureTournamentStructureEditable(Action<string> setError)
    {
        if (CanEditTournamentStructure)
            return true;

        setError(TournamentStructureLockedMessage);
        return false;
    }

    private bool IsEffectiveDrawParticipant(ParticipantDto participant)
        => !IsTeamplayActive || string.Equals(participant.Type, "TeamMember", StringComparison.OrdinalIgnoreCase);

    private static string TeamDrawKey(ParticipantDto participant)
    {
        return participant.TeamId.HasValue
            ? $"TEAM:{participant.TeamId.Value:D}"
            : NormalizeDisplayName(participant.DisplayName);
    }

    private static string NormalizeDisplayName(string? value)
    {
        var source = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (source.Length == 0)
            return string.Empty;

        var filtered = source.Where(char.IsLetterOrDigit).ToArray();
        return filtered.Length > 0 ? new string(filtered) : source;
    }

    private List<ParticipantDto> EffectiveDrawParticipants
    {
        get
        {
            if (!IsTeamplayActive)
            {
                return participants
                    .Where(IsEffectiveDrawParticipant)
                    .ToList();
            }

            // In teamplay, draw logic must treat one team as one assignable entity.
            return participants
                .Where(p => string.Equals(p.Type, "TeamMember", StringComparison.OrdinalIgnoreCase))
                .GroupBy(TeamDrawKey)
                .Select(group =>
                {
                    var representative = group
                        .OrderBy(p => p.Seed > 0 ? p.Seed : int.MaxValue)
                        .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .First();

                    var assignedGroup = group
                        .Select(p => p.GroupNumber)
                        .FirstOrDefault(g => g.HasValue && g.Value > 0);

                    return representative with { GroupNumber = assignedGroup };
                })
                .ToList();
        }
    }

    /// <summary>Returns true if any participant has been assigned to a group.</summary>
    private bool HasDrawAssignments() => EffectiveDrawParticipants.Any(p => p.GroupNumber.HasValue && p.GroupNumber > 0);

    /// <summary>Returns unassigned participants (no group number set).</summary>
    private List<ParticipantDto> UnassignedParticipants
    {
        get
        {
            var assignedKeys = EffectiveDrawParticipants
                .Where(p => p.GroupNumber.HasValue && p.GroupNumber > 0)
                .Select(TeamDrawKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var assignedDisplayNames = Enumerable.Range(1, editGroupCount)
                .SelectMany(GroupParticipants)
                .Select(p => NormalizeDisplayName(p.DisplayName))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return EffectiveDrawParticipants
                .Where(p => !p.GroupNumber.HasValue || p.GroupNumber == 0)
                .Where(p => !assignedKeys.Contains(TeamDrawKey(p)))
                .Where(p => !assignedDisplayNames.Contains(NormalizeDisplayName(p.DisplayName)))
                .ToList();
        }
    }

    /// <summary>Returns participants assigned to a specific group number.</summary>
    private List<ParticipantDto> GroupParticipants(int groupNumber) =>
        EffectiveDrawParticipants
            .Where(p => p.GroupNumber == groupNumber)
            .GroupBy(TeamDrawKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(p => p.Seed > 0 ? p.Seed : int.MaxValue)
                .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderBy(p => p.Seed > 0 ? p.Seed : int.MaxValue)
            .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Ideal group size for even distribution.</summary>
    private int IdealGroupSize => editGroupCount > 0 ? (int)Math.Ceiling((double)EffectiveDrawParticipants.Count / editGroupCount) : EffectiveDrawParticipants.Count;

    /// <summary>Responsive columns for group dropzones: 2 groups stay 2-column on md+, narrow screens collapse to 1.</summary>
    private string GroupDropzoneColumnCss => editGroupCount switch
    {
        <= 1 => "col-12",
        2 => "col-12 col-md-6",
        _ => "col-12 col-md-6 col-xl-4"
    };

    private bool IsTeamplayActive => selectedTournament?.TeamplayEnabled == true;

    private bool IsTeamMember(ParticipantDto participant)
        => string.Equals(participant.Type, "TeamMember", StringComparison.OrdinalIgnoreCase);

    private List<ParticipantDto> EffectivePlayerParticipants => participants
        .Where(p => !IsTeamMember(p))
        .ToList();

    private int EffectivePlayerCount => EffectivePlayerParticipants.Count;

    private int RequiredTeamCount
        => editPlayersPerTeam > 0 ? EffectivePlayerCount / editPlayersPerTeam : 0;

    private bool TeamSizeDividesParticipants
        => editPlayersPerTeam > 0 && EffectivePlayerCount % editPlayersPerTeam == 0;

    private int TeamFormationProgressPercent
    {
        get
        {
            var total = EffectiveDrawParticipants.Count;
            if (total <= 0)
                return 0;

            var assigned = EffectiveDrawParticipants.Count(p => p.GroupNumber.HasValue && p.GroupNumber > 0);
            var percent = (int)Math.Round((double)assigned * 100d / total, MidpointRounding.AwayFromZero);
            return Math.Clamp(percent, 0, 100);
        }
    }

    private HashSet<Guid> DraftAssignedParticipantIds => teamDrafts
        .SelectMany(t => t.MemberParticipantIds)
        .ToHashSet();

    private List<ParticipantDto> TeamDraftUnassignedParticipants => participants
        .Where(p => !IsTeamMember(p))
        .Where(p => !DraftAssignedParticipantIds.Contains(p.Id))
        .OrderBy(p => p.Seed > 0 ? p.Seed : int.MaxValue)
        .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private bool IsTeamFormationComplete
    {
        get
        {
            if (!IsTeamplayActive)
                return true;

            if (!TeamSizeDividesParticipants || editPlayersPerTeam < 1)
                return false;

            if (teams.Count != RequiredTeamCount)
                return false;

            if (teams.Any(t => string.IsNullOrWhiteSpace(t.Name) || t.Members.Count != editPlayersPerTeam))
                return false;

            var assignedIds = teams
                .SelectMany(t => t.Members)
                .Select(m => m.Id)
                .Distinct()
                .Count();

            return assignedIds == EffectivePlayerCount;
        }
    }

    private bool IsTeamDraftUnassignedEmpty => TeamDraftUnassignedParticipants.Count == 0;

    private string TeamDraftLayoutCss
        => IsTeamDraftUnassignedEmpty
            ? "row g-3 team-draft-layout is-unassigned-empty"
            : "row g-3 team-draft-layout";

    private string TeamDraftUnassignedColumnCss
        => IsTeamDraftUnassignedEmpty
            ? "col-lg-auto d-none d-lg-block team-draft-unassigned-col team-draft-unassigned-col-desktop is-collapsed"
            : "col-lg-3 d-none d-lg-block team-draft-unassigned-col team-draft-unassigned-col-desktop";

    private string TeamDraftUnassignedMobileColumnCss
        => "col-12 d-lg-none team-draft-unassigned-col team-draft-unassigned-col-mobile";

    private string TeamDraftTeamsColumnCss
        => IsTeamDraftUnassignedEmpty
            ? "col-lg team-draft-teams-col"
            : "col-lg-9 team-draft-teams-col";

    private string TeamDraftUnassignedCardCss
        => IsTeamDraftUnassignedEmpty
            ? "card team-unassigned-card is-collapsed"
            : "card team-unassigned-card";

    private bool CanSeedTeams
        => IsTeamplayActive && editSeedingEnabled && teamDrafts.Any();

    private ParticipantDto? TeamMemberForDraft(TeamDraftItem draft)
    {
        if (draft.TeamId.HasValue)
        {
            return participants
                .Where(p => IsTeamMember(p) && p.TeamId == draft.TeamId)
                .OrderBy(p => p.Seed > 0 ? p.Seed : int.MaxValue)
                .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        return null;
    }

    private int TeamSeedForDraft(TeamDraftItem draft, int fallback)
    {
        if (!editSeedingEnabled)
            return fallback;

        var member = TeamMemberForDraft(draft);
        if (member is null || member.Seed <= 0)
            return fallback;

        if (editSeedingEnabled && editSeedTopCount > 0 && member.Seed > editSeedTopCount)
            return fallback;

        return member.Seed;
    }

    private int? VisibleTeamSeedForDraft(TeamDraftItem draft)
    {
        var seed = TeamSeedForDraft(draft, 0);
        return seed > 0 ? seed : null;
    }

    private void SortTeamDraftsBySeedInMemory()
    {
        if (teamDrafts.Count < 2)
            return;

        teamDrafts = teamDrafts
            .Select((draft, index) => new
            {
                Draft = draft,
                Index = index,
                Seed = TeamSeedForDraft(draft, int.MaxValue)
            })
            .OrderBy(x => x.Seed)
            .ThenBy(x => x.Index)
            .Select(x => x.Draft)
            .ToList();
    }

    private bool CanProceedWithTeamDraw
        => !IsTeamplayActive || IsTeamFormationComplete;

    private bool CanShowDrawCreatePlanButton
        => selectedTournament is not null
           && !matches.Any()
           && participants.Count >= 2
           && (selectedTournament.Mode != "Knockout"
               ? IsGroupDrawComplete
               : IsKnockoutDrawComplete)
           && !selectedTournament.IsLocked
           && IsCurrentUserManager;

    private bool CanShowDrawDeletePlanButton
        => selectedTournament is not null
           && matches.Any()
           && !selectedTournament.IsLocked
           && IsCurrentUserManager
           && !CanShowDrawCreatePlanButton;

    private bool CanExecuteDrawCreatePlan
        => CanShowDrawCreatePlanButton
           && string.IsNullOrWhiteSpace(DrawCreatePlanDisabledReason);

    private bool IsGroupDrawComplete
        => EffectiveDrawParticipants.Count > 0 && UnassignedParticipants.Count == 0;

    private bool IsDrawCompleted
        => selectedTournament?.Mode == "GroupAndKnockout"
            ? IsGroupDrawComplete
            : IsKnockoutDrawComplete;

    private string DrawStatusBadgeCss
        => matches.Any()
            ? "text-bg-success"
            : IsDrawCompleted
                ? "text-bg-warning"
                : "text-bg-danger";

    private int UnassignedDrawCount
        => selectedTournament?.Mode == "GroupAndKnockout"
            ? UnassignedParticipants.Count
            : KnockoutUnassignedParticipants.Count;

    private int DrawTotalCount
        => selectedTournament?.Mode is "GroupAndKnockout" or "Knockout"
            ? EffectiveDrawParticipants.Count
            : 0;

    private int DrawAssignedCount
        => selectedTournament?.Mode == "GroupAndKnockout"
            ? Math.Max(0, DrawTotalCount - UnassignedParticipants.Count)
            : KnockoutAssignedParticipants.Count;

    private int DrawProgressPercent
        => DrawTotalCount <= 0
            ? 0
            : (int)Math.Round((double)DrawAssignedCount * 100 / DrawTotalCount, MidpointRounding.AwayFromZero);

    private string DrawProgressText => $"{DrawProgressPercent}%";

    private string DrawProgressTitle
        => $"Auslosungsfortschritt: {DrawAssignedCount}/{DrawTotalCount} {(IsTeamplayActive ? "Teams" : "Teilnehmer")} zugewiesen ({DrawProgressPercent}%).";

    private bool ShouldShowDrawTeamFormationWarning
        => IsTeamplayActive
           && !CanProceedWithTeamDraw
           && CanShowDrawCreatePlanButton;

    private string? GetTeamNameForParticipant(Guid? teamId)
        => teamId.HasValue ? teams.FirstOrDefault(t => t.Id == teamId.Value)?.Name : null;

    // ─── Drag & Drop: Draw (Participant → Group) ───
    private Guid? draggedParticipantId;
    private int? dropTargetGroupNumber;

    // ─── Seeding Drag ───
    private int? draggedSeedIndex;

    // ─── Draw Animation ───
    private string drawAnimationMode = "Off"; // Off | Exciting | Moderate
    private bool isDrawAnimating;
    private bool keepDrawViewportStable;
    private Guid? drawCandidateParticipantId;
    private Guid? drawWinnerParticipantId;
    private Guid? drawArrivingParticipantId;
    private int? drawHighlightedGroupNumber;
    private int? drawSourcePotNumber;
    private int? drawHighlightedKoMatchNumber;
    private bool? drawHighlightedKoHomeSlot;
    private bool showKoDrawToken;
    private string koDrawTokenStyle = string.Empty;
    private const string KoDrawContainerId = "ko-draw-grid";

    // ─── Teamplay Draft (Issue #11) ───
    private sealed class TeamDraftItem
    {
        public string UiKey { get; set; } = Guid.NewGuid().ToString("N");
        public Guid? TeamId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsAutoName { get; set; } = true;
        public List<Guid> MemberParticipantIds { get; set; } = [];
    }

    private List<TeamDraftItem> teamDrafts = [];
    private Guid? draggedTeamParticipantId;
    private Guid? selectedTeamParticipantId;
    private int? draggedTeamSeedIndex;
    private int? selectedTeamSeedIndex;
    private CancellationTokenSource? teamSeedLongPressCts;
    private int? editingTeamNameIndex;
    private string editingTeamNameValue = string.Empty;
    private int? pendingTeamNameFocusIndex;
    private int? openTeamNameMenuIndex;
    private int? teamSeedInsertTargetIndex;
    private bool isTeamSeedDragging;
    private int? dropTargetTeamIndex;
    private int? drawHighlightedTeamIndex;
    private bool hasUnsavedTeamDraftChanges;
    private string? teamDraftError;
    private const string TeamSeedGridId = "team-seed-grid";

    /// <summary>Sentinel date used as "no automatic close" when registration is opened manually (= open until explicitly closed).</summary>
    private static readonly DateTime MaxRegistrationDate = new DateTime(9999, 12, 31, 23, 59, 59);
    private const int TeamSeedLongPressMs = 380;

    private List<KnockoutDrawCard> knockoutDrawCards = [];
    private Guid? draggedKnockoutParticipantId;

    private bool HasGeneralRequiredMissing()
        => string.IsNullOrWhiteSpace(editName)
            || string.IsNullOrWhiteSpace(editOrganizer)
            || editEndDate < editStartDate
            || string.IsNullOrWhiteSpace(editMode)
            || string.IsNullOrWhiteSpace(editVariant);

    private bool HasRegistrationRequiredMissing()
        => editIsRegistrationOpen
            && (!editRegistrationStart.HasValue || !editRegistrationEnd.HasValue || editRegistrationEnd <= editRegistrationStart);

    private bool HasTeamplayRequiredMissing()
        => editTeamplay && editPlayersPerTeam < 2;

    private bool HasSeedingRequiredMissing()
        => editSeedingEnabled && (editSeedTopCount <= 0 || editSeedTopCount > EffectiveDrawParticipants.Count);

    private bool HasGroupSettingsRequiredMissing()
        => editMode == "GroupAndKnockout"
            && (editGroupCount < 1 || editPlayoffAdvancers < 1 || editMatchesPerOpponent < 1 || editKnockoutsPerRound < 0);

    private bool HasScoringRequiredMissing()
        => editMode == "GroupAndKnockout"
            && (editWinPoints < 1 || editLegFactor < 0);

    private bool ShowGroupSettingsSection()
        => selectedTournament?.Mode == "GroupAndKnockout" || editMode == "GroupAndKnockout";

    private string GeneralSettingsSummary()
        => $"{editStartDate:dd.MM.yyyy} • {ModeDisplay(editMode)} • {VariantDisplay(editVariant)}";

    private string RegistrationSummary()
        => editIsRegistrationOpen
            ? $"Offen{(editRegistrationStart.HasValue ? $" ab {editRegistrationStart.Value:dd.MM HH:mm}" : string.Empty)}"
            : "Geschlossen";

    private string TeamplaySeedingSummary()
        => $"Teamplay: {(editTeamplay ? $"Ja ({editPlayersPerTeam}/Team)" : "Nein")} • Setzliste: {(editSeedingEnabled ? $"Aktiv ({editSeedTopCount})" : "Aus")}";

    private string GroupSettingsSummary()
        => $"{editGroupCount} Gruppen • {editPlayoffAdvancers} Aufsteiger • {editMatchesPerOpponent} Match/Gegner";

    private string ScoringSummary()
        => $"Siegpunkte: {editWinPoints} • Leg-Faktor: {editLegFactor}";

    private static string ModeDisplay(string mode)
        => mode switch
        {
            "Knockout" => "K.O.-Modus",
            "GroupAndKnockout" => "Gruppe + K.O.",
            _ => mode
        };

    private static string VariantDisplay(string variant)
        => variant switch
        {
            "OnSite" => "Vor-Ort",
            "Online" => "Online",
            _ => variant
        };

    private void ToggleStatusDropdown() => showStatusDropdown = !showStatusDropdown;

    private void CloseStatusDropdown() => showStatusDropdown = false;

    private async Task UpdateTournamentStatusAndCloseAsync(string status)
    {
        CloseStatusDropdown();
        await UpdateTournamentStatusAsync(status);
    }

    private IReadOnlyList<SplitButton.SplitButtonItem> BuildTournamentStatusItems()
    {
        if (selectedTournament is null)
            return [];

        var statuses = new[] { "Erstellt", "Geplant", "Gestartet", "Beendet", "Abgebrochen" };
        return statuses.Select(status => new SplitButton.SplitButtonItem
        {
            Text = status,
            IsDisabled = string.Equals(selectedTournament.Status, status, StringComparison.OrdinalIgnoreCase),
            Title = string.Equals(selectedTournament.Status, status, StringComparison.OrdinalIgnoreCase) 
                ? $"Turnier ist bereits im Status '{status}'"
                : null,
            IsDanger = status == "Abgebrochen",
            OnClick = EventCallback.Factory.Create(this, () => UpdateTournamentStatusAsync(status))
        }).ToList();
    }

    private string ParticipantDisplayName(ParticipantDto participant)
    {
        var name = participant.DisplayName.ToUpperInvariant();
        if (!editSeedingEnabled || participant.Seed <= 0 || participant.Seed > editSeedTopCount)
            return name;
        return $"{name} (#{participant.Seed})";
    }

    private string ParticipantSeedLabel(ParticipantDto participant)
    {
        if (!editSeedingEnabled || participant.Seed <= 0 || participant.Seed > editSeedTopCount)
            return "—";
        return $"#{participant.Seed}";
    }

    // ─── Lifecycle ───
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await EnsureTabSwipeInteropAsync();
        await EnsureTouchDragDropInteropAsync();
        await EnsureTeamDraftOutsideClickInteropAsync();

        if (pendingTeamNameFocusIndex.HasValue)
        {
            var index = pendingTeamNameFocusIndex.Value;
            pendingTeamNameFocusIndex = null;
            try
            {
                await JS.InvokeVoidAsync("dartSuiteUi.focusAndSelect", $"#{TeamNameEditInputId(index)}");
            }
            catch
            {
                // ignore in prerender or when script is not yet available
            }
        }

        if (firstRender && !_detailsExpandedLoadedFromStorage)
        {
            _detailsExpandedLoadedFromStorage = true;
            try
            {
                var stored = await JS.InvokeAsync<string?>("dartSuiteUi.localStorageGet", "ds-match-details-expanded");
                if (!string.IsNullOrEmpty(stored))
                {
                    forcedMatchDetailsExpanded = stored == "null" ? (bool?)null
                        : stored == "true" ? true
                        : false;
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch { /* best-effort */ }

            tournamentListCollapsed = true;

            if (!hasLoadedGroupMatchesViewMode)
            {
                hasLoadedGroupMatchesViewMode = true;
                try
                {
                    var storedGroupViewMode = await JS.InvokeAsync<string?>("dartSuiteUi.localStorageGet", "ds-groups-match-layout");
                    if (string.Equals(storedGroupViewMode, "horizontal", StringComparison.OrdinalIgnoreCase))
                        groupMatchesViewMode = "horizontal";
                    else
                        groupMatchesViewMode = "vertical";
                    await InvokeAsync(StateHasChanged);
                }
                catch
                {
                    groupMatchesViewMode = "vertical";
                }
            }
        }

        if (_initDetailSections && detailMatch is not null)
        {
            _initDetailSections = false;
            try
            {
                await JS.InvokeVoidAsync("dartSuiteUi.initDetailsStorage", "detailSection-planung", "detailMatchSection_planung");
                await JS.InvokeVoidAsync("dartSuiteUi.initDetailsStorage", "detailSection-allgemein", "detailMatchSection_allgemein");
            }
            catch { /* ignore JS interop errors during prerender */ }
        }

        if (keepDrawViewportStable)
        {
            try
            {
                await JS.InvokeVoidAsync("dartSuiteUi.restoreScrollY");
            }
            catch
            {
                // ignore JS interop errors during prerender
            }
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(LoadTournamentsAsync(), LoadBoardsAsync(), TryLoadAutodartsSessionAsync());
        await LoadRealtimeSettingsAsync();
        await LoadMatchCardSettingsAsync();
        await TryLoadCompactViewportAsync();

        forcedMatchDetailsExpanded = isCompactViewport ? null : true;

        if (!string.IsNullOrWhiteSpace(QueryTournamentId)
            && Guid.TryParse(QueryTournamentId, out var requestedTournamentId))
        {
            var requestedTournament = tournaments.FirstOrDefault(t => t.Id == requestedTournamentId);
            if (requestedTournament is not null)
                await SelectTournamentAsync(requestedTournament);
        }
        else if (AppState.SelectedTournament is not null)
        {
            var activeTournament = tournaments.FirstOrDefault(t => t.Id == AppState.SelectedTournament.Id);
            if (activeTournament is not null)
                await SelectTournamentAsync(activeTournament);
        }

        var openedMatchFromQuery = false;

        // If a matchId was passed via query string (e.g. from Boards page), open that match
        if (!string.IsNullOrEmpty(QueryMatchId) && Guid.TryParse(QueryMatchId, out var qMatchId))
        {
            // Find which tournament this match belongs to
            foreach (var t in tournaments)
            {
                var tMatches = (await Api.GetMatchesAsync(t.Id)).ToList();
                var found = tMatches.FirstOrDefault(m => m.Id == qMatchId);
                if (found is not null)
                {
                    await SelectTournamentAsync(t);
                    matches = tMatches;
                    OpenMatchDetail(found);
                    openedMatchFromQuery = true;
                    break;
                }
            }
        }

        if (!openedMatchFromQuery)
            await TryOpenBoardDetailFromQueryAsync();

        // If a tab query param was passed, switch to that tab (and auto-select tournament if needed)
        if (!string.IsNullOrEmpty(QueryTab))
        {
            if (selectedTournament is null && AppState.SelectedTournament is not null)
            {
                var t = tournaments.FirstOrDefault(t => t.Id == AppState.SelectedTournament.Id);
                if (t is not null)
                    await SelectTournamentAsync(t);
            }

            await SwitchTabAsync(QueryTab, forceRefresh: true);
        }

        lastHandledQueryTournamentId = NormalizeQueryValue(QueryTournamentId);
        lastHandledQueryTab = NormalizeQueryTab(QueryTab);
        hasCompletedInitialLoad = true;

        // Connect to SignalR hubs
        await ConnectToHubAsync();
        await ConnectToBoardHubAsync();
        UpdateAutoRefreshTimerMode();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!hasCompletedInitialLoad)
            return;

        var normalizedTournamentId = NormalizeQueryValue(QueryTournamentId);
        var normalizedTab = NormalizeQueryTab(QueryTab);

        var tournamentChanged = !string.Equals(lastHandledQueryTournamentId, normalizedTournamentId, StringComparison.OrdinalIgnoreCase);
        var tabChanged = !string.Equals(lastHandledQueryTab, normalizedTab, StringComparison.OrdinalIgnoreCase);

        if (!tournamentChanged && !tabChanged)
            return;

        lastHandledQueryTournamentId = normalizedTournamentId;
        lastHandledQueryTab = normalizedTab;

        if (tournamentChanged && normalizedTournamentId is not null && Guid.TryParse(normalizedTournamentId, out var requestedTournamentId))
        {
            if (selectedTournament?.Id != requestedTournamentId)
            {
                var requestedTournament = tournaments.FirstOrDefault(t => t.Id == requestedTournamentId);
                if (requestedTournament is null)
                {
                    await LoadTournamentsAsync();
                    requestedTournament = tournaments.FirstOrDefault(t => t.Id == requestedTournamentId);
                }

                if (requestedTournament is not null)
                    await SelectTournamentAsync(requestedTournament);
            }
        }
        else if (selectedTournament is null && AppState.SelectedTournament is not null)
        {
            var appStateTournament = tournaments.FirstOrDefault(t => t.Id == AppState.SelectedTournament.Id);
            if (appStateTournament is not null)
                await SelectTournamentAsync(appStateTournament);
        }

        if (selectedTournament is not null)
            await SwitchTabAsync(normalizedTab, forceRefresh: true);
    }

    private static string NormalizeQueryTab(string? queryTab)
        => string.IsNullOrWhiteSpace(queryTab) ? "general" : queryTab.Trim();

    private static string? NormalizeQueryValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task SwitchTabAsync(string tab, bool forceRefresh)
    {
        if (string.IsNullOrWhiteSpace(tab))
            return;

        if (!forceRefresh && string.Equals(activeTab, tab, StringComparison.Ordinal))
            return;

        activeTab = tab;

        if (selectedTournament is null)
            return;

        if (tab is "groups" or "knockout" or "schedule")
        {
            matches = (await Api.GetMatchesAsync(selectedTournament.Id)).ToList();
            CleanupLiveSnapshots();
        }

        if (tab == "draw")
            await LoadTeamsAsync(selectedTournament.Id);

        if (tab == "groups")
            groupStandings = (await Api.GetGroupStandingsAsync(selectedTournament.Id)).ToList();

        if (tab == "schedule")
            await LoadMatchListenersAsync();

        if (tab is "groups" or "knockout" or "schedule" or "boards" or "participants")
            await ReconcileTournamentMonitoringForViewAsync();

        if (tab == "rounds")
            await LoadRoundsAsync();

        await InvokeAsync(StateHasChanged);
    }

    private Task SwitchTabAsync(string tab)
        => SwitchTabAsync(tab, forceRefresh: false);

    private IReadOnlyList<string> VisibleTabSequence
    {
        get
        {
            // GroupAndKnockout: groups → schedule → knockout gives the DS-024 special flow
            // (Gruppenphase → Spielplan one-click, then Spielplan → K.O. for the final phase).
            if (selectedTournament?.Mode == "GroupAndKnockout")
                return ["general", "boards", "participants", "draw", "rounds", "groups", "schedule", "knockout"];
            return ["general", "boards", "participants", "draw", "rounds", "knockout", "schedule"];
        }
    }

    private int ActiveTabSequenceIndex => GetVisibleTabIndex(activeTab);

    private bool IsFirstVisibleTab => selectedTournament is not null && ActiveTabSequenceIndex == 0;

    private bool CanGoToPreviousTab => selectedTournament is not null && ActiveTabSequenceIndex > 0;

    private bool IsTournamentListPanelOpen
        => selectedTournament is not null && (!tournamentListCollapsed || tournamentListMobileOpen);

    private bool CanUsePreviousFooterAction
        => selectedTournament is not null && (CanGoToPreviousTab || (IsFirstVisibleTab && !IsTournamentListPanelOpen));

    private bool CanGoToNextTab => selectedTournament is not null && ActiveTabSequenceIndex >= 0 && ActiveTabSequenceIndex < VisibleTabSequence.Count - 1;

    private string ActiveTabDisplayName => TabDisplayName(activeTab);

    private bool ShowSpecialGroupScheduleFlowHint
        => string.Equals(selectedTournament?.Mode, "GroupAndKnockout", StringComparison.Ordinal)
           && activeTab is "groups" or "schedule" or "knockout";

    private string SpecialGroupScheduleFlowHint
        => activeTab switch
        {
            "groups" => "Gruppenphase",
            "schedule" => "Spezial-Flow: Gruppenphase <-> Spielplan <-> K.O.-Phase",
            "knockout" => "Spezial-Flow: Spielplan -> K.O.-Phase",
            _ => ActiveTabDisplayName
        };

    private string PreviousTabDisplayName
        => CanGoToPreviousTab
            ? TabDisplayName(VisibleTabSequence[ActiveTabSequenceIndex - 1])
            : IsFirstVisibleTab
                ? "Turnierliste"
                : "Vorheriger Tab";

    private string NextTabDisplayName
        => CanGoToNextTab ? TabDisplayName(VisibleTabSequence[ActiveTabSequenceIndex + 1]) : "Nächster Tab";

    private bool HasDelayedPlannedMatches
        => matches.Any(m => m.FinishedUtc is null
                            && (m.DelayMinutes > 0 || string.Equals(m.SchedulingStatus, "Delayed", StringComparison.OrdinalIgnoreCase)));

    private bool HasRunningScheduleMatches
        => matches.Any(m => m.StartedUtc is not null && m.FinishedUtc is null);

    private bool HasPlannedScheduleMatches
        => matches.Any(m => m.FinishedUtc is null);

    private bool PreviousTabIsSchedule
        => CanGoToPreviousTab
           && string.Equals(VisibleTabSequence[ActiveTabSequenceIndex - 1], "schedule", StringComparison.Ordinal);

    private bool NextTabIsSchedule
        => CanGoToNextTab
           && string.Equals(VisibleTabSequence[ActiveTabSequenceIndex + 1], "schedule", StringComparison.Ordinal);

    private string PreviousTabButtonCssClass
        => PreviousTabIsSchedule
            ? ScheduleNavButtonCssClass
            : "btn btn-outline-secondary btn-sm";

    private string NextTabButtonCssClass
        => NextTabIsSchedule
            ? ScheduleNavButtonCssClass
            : "btn btn-primary btn-sm";

    private string PreviousTabButtonTitle
        => CanGoToPreviousTab
            ? (PreviousTabIsSchedule ? ScheduleNavButtonTitle : "Zum vorherigen Tab wechseln")
            : IsFirstVisibleTab
                ? "Turnierliste einblenden"
                : "Zum vorherigen Tab wechseln";

    private string NextTabButtonTitle
        => NextTabIsSchedule ? ScheduleNavButtonTitle : "Zum nächsten Tab wechseln";

    private string ScheduleNavButtonCssClass
        => HasDelayedPlannedMatches
            ? "btn btn-warning btn-sm"
            : HasRunningScheduleMatches
                ? "btn btn-success btn-sm"
                : HasPlannedScheduleMatches
                    ? "btn btn-info btn-sm"
                    : "btn btn-outline-secondary btn-sm";

    private string ScheduleNavButtonTitle
        => HasDelayedPlannedMatches
            ? "Spielplan enthält Verzögerungen"
            : HasRunningScheduleMatches
                ? "Spielplan enthält laufende Matches"
                : HasPlannedScheduleMatches
                    ? "Spielplan mit geplanten Matches"
                    : "Spielplan ohne geplante Matches";

    private static string TabDisplayName(string tab)
        => tab switch
        {
            "general" => "Allgemein",
            "boards" => "Boards",
            "participants" => "Teilnehmer",
            "draw" => "Auslosung",
            "rounds" => "Spielmodus",
            "groups" => "Gruppenphase",
            "knockout" => "K.O.-Phase",
            "schedule" => "Spielplan",
            _ => tab
        };

    private int GetVisibleTabIndex(string tab)
    {
        var tabs = VisibleTabSequence;
        for (var i = 0; i < tabs.Count; i++)
        {
            if (string.Equals(tabs[i], tab, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private async Task HandlePreviousFooterActionAsync()
    {
        if (CanGoToPreviousTab)
        {
            await NavigateTabRelativeAsync(-1);
            return;
        }

        if (IsFirstVisibleTab && !IsTournamentListPanelOpen)
            await SetTournamentListCollapsedAsync(false);
    }

    private Task GoToPreviousTabAsync() => NavigateTabRelativeAsync(-1);

    private Task GoToNextTabAsync() => NavigateTabRelativeAsync(1);

    private Task HandleTabSelectionAsync(string tab)
        => string.Equals(tab, "rounds", StringComparison.Ordinal)
            ? SwitchToRoundsTabAsync()
            : SwitchTabAsync(tab);

    private async Task OnEditNameChangedAsync(string value)
    {
        editName = value;
        await AutoSaveSettingAsync();
    }

    private async Task OnEditOrganizerChangedAsync(string value)
    {
        editOrganizer = value;
        await AutoSaveSettingAsync();
    }

    private async Task OnEditStartDateChangedAsync(DateTime value)
    {
        editStartDate = value;
        await AutoSaveSettingAsync();
    }

    private async Task OnEditEndDateChangedAsync(DateTime value)
    {
        editEndDate = value;
        await AutoSaveSettingAsync();
    }

    private async Task OnEditStartTimeInputChangedAsync(TimeOnly? value)
    {
        editStartTimeInput = value;
        await AutoSaveSettingAsync();
    }

    private async Task OnEditModeChangedAsync(string value)
    {
        editMode = value;
        await AutoSaveSettingAsync();
    }

    private async Task OnEditVariantChangedAsync(string value)
    {
        editVariant = value;
        await AutoSaveSettingAsync();
    }

    private async Task OnEditThirdPlaceMatchChangedAsync(bool value)
    {
        editThirdPlaceMatch = value;
        await AutoSaveSettingAsync();
    }

    private async Task OnEditDiscordWebhookUrlChangedAsync(string? value)
    {
        editDiscordWebhookUrl = value;
        await AutoSaveSettingAsync();
    }

    private async Task OnEditDiscordWebhookDisplayTextChangedAsync(string? value)
    {
        editDiscordWebhookDisplayText = value;
        await AutoSaveSettingAsync();
    }

    private async Task OnEditGroupCountChangedAsync(int value)
    {
        editGroupCount = value;
        await AutoSaveSettingAsync();
    }

    private async Task OnEditPlayoffAdvancersChangedAsync(int value)
    {
        editPlayoffAdvancers = value;
        await AutoSaveSettingAsync();
    }

    private async Task OnEditMatchesPerOpponentChangedAsync(int value)
    {
        editMatchesPerOpponent = value;
        await AutoSaveSettingAsync();
    }

    private async Task OnEditKnockoutsPerRoundChangedAsync(int value)
    {
        editKnockoutsPerRound = value;
        await AutoSaveSettingAsync();
    }

    private async Task OnEditWinPointsChangedAsync(int value)
    {
        editWinPoints = value;
        await AutoSaveSettingAsync();
    }

    private async Task OnEditLegFactorChangedAsync(int value)
    {
        editLegFactor = value;
        await AutoSaveSettingAsync();
    }

    private async Task OnEditGroupModeChangedAsync(string value)
    {
        editGroupMode = value;
        await AutoSaveSettingAsync();
    }

    private async Task OnEditGroupDrawModeChangedAsync(string value)
    {
        if (string.Equals(editGroupDrawMode, value, StringComparison.OrdinalIgnoreCase))
            return;

        var hasGroupAssignments = EffectiveDrawParticipants.Any(p => p.GroupNumber.HasValue && p.GroupNumber > 0);
        var hasKnockoutAssignments = knockoutDrawCards.Any(c => c.HomeParticipantId.HasValue || c.AwayParticipantId.HasValue);
        var hasDrawAssignments = selectedTournament?.Mode == "Knockout" ? hasKnockoutAssignments : hasGroupAssignments;
        var changingToSeededPots = string.Equals(value, "SeededPots", StringComparison.OrdinalIgnoreCase);

        if (hasDrawAssignments)
        {
            confirmationMessage = changingToSeededPots
                ? "Beim Wechsel auf Modus 'Lostopf' wird die aktuelle Auslosung zurückgesetzt. Möchten Sie fortfahren?"
                : "Beim Wechsel des Auslosungsmodus wird die aktuelle Auslosung zurückgesetzt. Möchten Sie fortfahren?";
            showConfirmationPlanImpact = false;
            confirmationAction = async () =>
            {
                if (selectedTournament?.Mode == "Knockout")
                {
                    ResetKnockoutDrawCards();
                    await InvokeAsync(StateHasChanged);
                }
                else
                {
                    await ResetDrawAsync();
                }

                editGroupDrawMode = value;
                await AutoSaveSettingAsync();
            };
            showConfirmation = true;
            return;
        }

        editGroupDrawMode = value;
        await AutoSaveSettingAsync();
    }

    private async Task OnEditPlanningVariantChangedAsync(string value)
    {
        editPlanningVariant = value;
        await AutoSaveSettingAsync();
    }

    private async Task OnEditGroupOrderModeChangedAsync(string value)
    {
        editGroupOrderMode = value;
        await AutoSaveSettingAsync();
    }

    private Task OnDrawAnimationModeChangedAsync(string value)
    {
        drawAnimationMode = value;
        return Task.CompletedTask;
    }

    private Task OnShowCompletedRoundsChangedAsync(bool value)
    {
        showCompletedRounds = value;
        return Task.CompletedTask;
    }

    private Task OnNewRoundPhaseChangedAsync(string value)
    {
        newRoundPhase = value;
        return Task.CompletedTask;
    }

    private Task OnNewRoundNumberChangedAsync(int value)
    {
        newRoundNumber = value;
        return Task.CompletedTask;
    }

    private Task OnNewRoundBaseScoreChangedAsync(int value)
    {
        newRoundBaseScore = value;
        return Task.CompletedTask;
    }

    private Task OnNewRoundGameModeChangedAsync(string value)
    {
        newRoundGameMode = value;
        return Task.CompletedTask;
    }

    private Task OnNewRoundLegsChangedAsync(int value)
    {
        newRoundLegs = value;
        return Task.CompletedTask;
    }

    private Task OnNewRoundSetsChangedAsync(int? value)
    {
        newRoundSets = value;
        return Task.CompletedTask;
    }

    private Task OnNewRoundInModeChangedAsync(string value)
    {
        newRoundInMode = value;
        return Task.CompletedTask;
    }

    private Task OnNewRoundOutModeChangedAsync(string value)
    {
        newRoundOutMode = value;
        return Task.CompletedTask;
    }

    private Task OnNewRoundMaxRoundsChangedAsync(int value)
    {
        newRoundMaxRounds = value;
        return Task.CompletedTask;
    }

    private Task OnNewRoundBullModeChangedAsync(string value)
    {
        newRoundBullMode = value;
        return Task.CompletedTask;
    }

    private Task OnNewRoundBullOffModeChangedAsync(string value)
    {
        newRoundBullOffMode = value;
        return Task.CompletedTask;
    }

    private Task OnNewRoundDurationChangedAsync(int value)
    {
        newRoundDuration = value;
        return Task.CompletedTask;
    }

    private Task OnNewRoundPauseChangedAsync(int value)
    {
        newRoundPause = value;
        return Task.CompletedTask;
    }

    private Task OnNewRoundPlayerPauseChangedAsync(int value)
    {
        newRoundPlayerPause = value;
        return Task.CompletedTask;
    }

    private Task OnNewRoundBoardAssignmentChangedAsync(string value)
    {
        newRoundBoardAssignment = value;
        return Task.CompletedTask;
    }

    private Task OnShowFlashTableChangedAsync(bool value)
    {
        showFlashTable = value;
        return Task.CompletedTask;
    }

    private Task OnShowGroupMatchesChangedAsync(bool value)
    {
        showGroupMatches = value;
        return Task.CompletedTask;
    }

    private Task OpenMatchCardScopeEditorAsync(string scopeKey)
    {
        OpenMatchCardScopeEditor(scopeKey);
        return Task.CompletedTask;
    }

    private Task OpenMatchDetailAsync(MatchDto match)
    {
        OpenMatchDetail(match);
        return Task.CompletedTask;
    }

    private Task OpenScheduledMatchDetailAsync(MatchDto match)
    {
        OpenMatchDetail(match, true);
        return Task.CompletedTask;
    }

    private Task SetKoViewModeAsync(string mode)
    {
        koViewMode = mode;
        return Task.CompletedTask;
    }

    private Task SetDropTargetGroupNumberAsync(int? groupNumber)
    {
        dropTargetGroupNumber = groupNumber;
        return Task.CompletedTask;
    }

    private bool IsGroupDragActive => draggedParticipantId.HasValue;

    private void StartParticipantDrag(Guid participantId)
    {
        draggedParticipantId = participantId;
        _ = InvokeAsync(StateHasChanged);
    }

    private void EndParticipantDrag()
    {
        draggedParticipantId = null;
        dropTargetGroupNumber = null;
        _ = InvokeAsync(StateHasChanged);
    }

    private Task SetDropTargetMatchIdAsync(Guid? matchId)
    {
        if (draggedBoardId.HasValue && matchId.HasValue)
        {
            var target = matches.FirstOrDefault(m => m.Id == matchId.Value);
            if (target is null || !CanAssignBoardToMatch(target))
                return Task.CompletedTask;
        }

        dropTargetMatchId = matchId;
        return Task.CompletedTask;
    }

    private Task OpenBoardDetailFromMatchAsync(MatchDto match)
    {
        OpenBoardDetailFromMatch(match);
        return Task.CompletedTask;
    }

    private Task SetDraggedBoardIdAsync(Guid boardId)
    {
        if (!CanManageScheduleInteractions || isWorking)
            return Task.CompletedTask;

        if (!boards.Any(b => b.Id == boardId))
            return Task.CompletedTask;

        draggedScheduleMatchId = null;
        dropTargetScheduleMatchId = null;
        dropTargetScheduleBoardId = null;
        activeScheduleInsertTargetKey = null;
        draggedBoardId = boardId;
        return Task.CompletedTask;
    }

    private Task ClearDraggedBoardIdAsync()
    {
        draggedBoardId = null;
        dropTargetMatchId = null;
        activeScheduleInsertTargetKey = null;
        return Task.CompletedTask;
    }

    private Task OnHideFinishedChangedAsync(bool value)
    {
        scheduleHideFinished = value;
        return Task.CompletedTask;
    }

    private Task OnShowNoBoardChangedAsync(bool value)
    {
        scheduleShowNoBoard = value;
        return Task.CompletedTask;
    }

    private Task OnStatusFilterChangedAsync(string value)
    {
        scheduleStatusFilter = value;
        return Task.CompletedTask;
    }

    private Task StartScheduleMatchDragAsync(Guid matchId)
    {
        StartScheduleMatchDrag(matchId);
        return Task.CompletedTask;
    }

    private Task EndScheduleMatchDragAsync()
    {
        EndScheduleMatchDrag();
        return Task.CompletedTask;
    }

    private Task MarkScheduleDropTargetMatchAsync(Guid matchId)
    {
        MarkScheduleDropTargetMatch(matchId);
        return Task.CompletedTask;
    }

    private Task ClearScheduleDropTargetMatchAsync(Guid matchId)
    {
        ClearScheduleDropTargetMatch(matchId);
        return Task.CompletedTask;
    }

    private Task MarkScheduleDropTargetBoardAsync(Guid boardId)
    {
        MarkScheduleDropTargetBoard(boardId);
        return Task.CompletedTask;
    }

    private Task ClearScheduleDropTargetBoardAsync(Guid boardId)
    {
        ClearScheduleDropTargetBoard(boardId);
        return Task.CompletedTask;
    }

    private Task MarkScheduleInsertTargetAsync(ScheduleDropRequest request)
    {
        MarkScheduleInsertTarget(request);
        return Task.CompletedTask;
    }

    private Task ClearScheduleInsertTargetAsync(ScheduleDropRequest request)
    {
        ClearScheduleInsertTarget(request);
        return Task.CompletedTask;
    }

    private Task DropOnScheduleMarkerCallbackAsync(ScheduleDropRequest request)
        => DropOnScheduleMarkerAsync(request);

    private Task OnEditMatchTimeValueChangedAsync(string value)
    {
        editMatchTimeValue = value;
        return Task.CompletedTask;
    }

    private async Task NavigateTabRelativeAsync(int delta)
    {
        if (selectedTournament is null || delta == 0)
            return;

        var tabs = VisibleTabSequence;
        var current = GetVisibleTabIndex(activeTab);
        if (current < 0)
            return;

        var target = current + delta;
        if (target < 0 || target >= tabs.Count)
            return;

        await SwitchTabAsync(tabs[target]);
    }

    [JSInvokable]
    public Task HandleTournamentTabsSwipeAsync(string direction)
    {
        if (!isCompactViewport)
            return Task.CompletedTask;

        if (string.Equals(direction, "left", StringComparison.OrdinalIgnoreCase))
            return NavigateTabRelativeAsync(1);

        if (string.Equals(direction, "right", StringComparison.OrdinalIgnoreCase))
            return NavigateTabRelativeAsync(-1);

        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task HandleTournamentListSwipeAsync(string direction)
    {
        if (!isCompactViewport || !tournamentListMobileOpen)
            return Task.CompletedTask;

        if (string.Equals(direction, "left", StringComparison.OrdinalIgnoreCase))
            return CloseTournamentListMobileAsync();

        if (string.Equals(direction, "right", StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        return Task.CompletedTask;
    }

    private Task SelectPreviousTournamentAsync() => SelectAdjacentTournamentAsync(-1);

    private Task SelectNextTournamentAsync() => SelectAdjacentTournamentAsync(1);

    private async Task SelectAdjacentTournamentAsync(int delta)
    {
        if (delta == 0)
            return;

        var currentIndex = CurrentTournamentStripIndex;
        if (currentIndex < 0)
            return;

        var targetIndex = currentIndex + delta;
        var items = TournamentStripItems;
        if (targetIndex < 0 || targetIndex >= items.Count)
            return;

        await SelectTournamentAsync(items[targetIndex]);
    }

    private async Task ToggleTournamentListCollapsedAsync()
    {
        if (isCompactViewport)
        {
            tournamentListMobileOpen = !tournamentListMobileOpen;
            return;
        }

        tournamentListCollapsed = !tournamentListCollapsed;
        await PersistTournamentListCollapsedAsync();
    }

    private async Task SetTournamentListCollapsedAsync(bool collapsed)
    {
        if (isCompactViewport)
        {
            tournamentListMobileOpen = !collapsed;
            return;
        }

        if (tournamentListCollapsed == collapsed)
            return;

        tournamentListCollapsed = collapsed;
        await PersistTournamentListCollapsedAsync();
    }

    private async Task PersistTournamentListCollapsedAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("dartSuiteUi.localStorageSet", TournamentListCollapsedStorageKey, tournamentListCollapsed ? "true" : "false");
        }
        catch
        {
            // best-effort persistence
        }
    }

    private async Task EnsureTabSwipeInteropAsync()
    {
        if (!isCompactViewport)
            return;

        tournamentTabSwipeRef ??= DotNetObjectReference.Create(this);

        try
        {
            await JS.InvokeVoidAsync("dartSuiteUi.registerHorizontalSwipe", "tournament-tab-content", tournamentTabSwipeRef, nameof(HandleTournamentTabsSwipeAsync));
            await JS.InvokeVoidAsync("dartSuiteUi.registerHorizontalSwipe", "tournament-list-panel", tournamentTabSwipeRef, nameof(HandleTournamentListSwipeAsync));
        }
        catch
        {
            // ignore in prerender or when script is not yet available
        }
    }

    private async Task EnsureTouchDragDropInteropAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("dartSuiteUi.registerTouchDragDrop", "tournament-tab-content");
        }
        catch
        {
            // ignore in prerender or when script is not yet available
        }
    }

    private async Task EnsureTeamDraftOutsideClickInteropAsync()
    {
        teamDraftUiRef ??= DotNetObjectReference.Create(this);

        try
        {
            await JS.InvokeVoidAsync("dartSuiteUi.registerOutsideClick", "tournaments-teamdraft-outside", teamDraftUiRef, nameof(HandleTeamDraftUiOutsideClickAsync), ".team-name-actions-menu,.team-name-menu-toggle,.team-name-edit-input,.dropdown-menu,.dropdown-toggle,.dropdown-item");
        }
        catch
        {
            // ignore in prerender or when script is not yet available
        }
    }

    private async Task DetachTabSwipeInteropAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("dartSuiteUi.unregisterHorizontalSwipe", "tournament-tab-content");
            await JS.InvokeVoidAsync("dartSuiteUi.unregisterHorizontalSwipe", "tournament-list-panel");
        }
        catch
        {
            // ignore dispose-time JS interop errors
        }
    }

    private async Task CloseTournamentListMobileAsync()
    {
        tournamentListMobileOpen = false;
        await SetTournamentListCollapsedAsync(true);
    }

    private async Task DetachTouchDragDropInteropAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("dartSuiteUi.unregisterTouchDragDrop", "tournament-tab-content");
        }
        catch
        {
            // ignore dispose-time JS interop errors
        }
    }

    private async Task DetachTeamDraftOutsideClickInteropAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("dartSuiteUi.unregisterOutsideClick", "tournaments-teamdraft-outside");
        }
        catch
        {
            // ignore dispose-time JS interop errors
        }
    }

    private async Task LoadRealtimeSettingsAsync()
    {
        fallbackPollingIntervalSeconds = 5;
        realtimePreferenceError = null;

        if (string.IsNullOrWhiteSpace(autodartsDisplayName))
            return;

        try
        {
            var preference = await Api.GetViewPreferenceAsync(autodartsDisplayName, RealtimePreferenceContext);
            if (preference is null || string.IsNullOrWhiteSpace(preference.SettingsJson))
                return;

            var parsed = JsonSerializer.Deserialize<TournamentRealtimeSettings>(preference.SettingsJson);
            if (parsed is null)
                return;

            fallbackPollingIntervalSeconds = Math.Clamp(parsed.FallbackPollingSeconds, 2, 30);
        }
        catch
        {
            fallbackPollingIntervalSeconds = 5;
        }
    }

    private async Task TryLoadCompactViewportAsync()
    {
        try
        {
            isCompactViewport = await JS.InvokeAsync<bool>("dartSuiteUi.isCompactViewport");
        }
        catch
        {
            isCompactViewport = false;
        }
    }

    private async Task<bool> IsDocumentVisibleAsync()
    {
        try
        {
            return await JS.InvokeAsync<bool>("dartSuiteUi.isDocumentVisible");
        }
        catch
        {
            return true;
        }
    }

    private async Task LoadMatchCardSettingsAsync()
    {
        matchCardPreferenceError = null;
        matchCardSettingsByView.Clear();
        activeMatchCardConfigScopeKey = BuildMatchCardScopeKey(MatchCardScopePage, MatchCardScopeSectionAll);

        matchCardSettingsByView[BuildMatchCardScopeKey(MatchCardScopeGlobalPage, MatchCardScopeSectionAll)] = MatchCardViewSettings.CreateDefault();
        matchCardSettingsByView[BuildMatchCardScopeKey(MatchCardScopePage, MatchCardScopeSectionAll)] = MatchCardViewSettings.CreateDefault();

        if (string.IsNullOrWhiteSpace(autodartsDisplayName))
            return;

        try
        {
            var preference = await Api.GetViewPreferenceAsync(autodartsDisplayName, MatchCardPreferenceContext);
            if (preference is null || string.IsNullOrWhiteSpace(preference.SettingsJson))
                return;

            var parsed = JsonSerializer.Deserialize<MatchCardViewPreferencePayload>(preference.SettingsJson);
            if (parsed?.Views is null || parsed.Views.Count == 0)
                return;

            foreach (var (key, value) in parsed.Views)
            {
                if (string.IsNullOrWhiteSpace(key) || value is null)
                    continue;

                var normalizedKey = key.Contains(MatchCardScopeDelimiter, StringComparison.Ordinal)
                    ? NormalizeMatchCardScopeKey(key)
                    : BuildMatchCardScopeKey(MatchCardScopePage, NormalizeLegacyMatchCardViewKey(key));

                value.Normalize();
                matchCardSettingsByView[normalizedKey] = value;
            }
        }
        catch
        {
            // Fallback to defaults.
        }
    }

    private async Task PersistMatchCardSettingsAsync()
    {
        if (string.IsNullOrWhiteSpace(autodartsDisplayName))
            return;

        var payload = new MatchCardViewPreferencePayload
        {
            Views = matchCardSettingsByView.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Clone(),
                StringComparer.OrdinalIgnoreCase)
        };

        var json = JsonSerializer.Serialize(payload);
        await Api.SaveViewPreferenceAsync(autodartsDisplayName, MatchCardPreferenceContext, json);
    }

    private static string BuildMatchCardScopeKey(string page, string section)
    {
        var normalizedPage = string.IsNullOrWhiteSpace(page) ? MatchCardScopePage : page.Trim().ToLowerInvariant();
        var normalizedSection = NormalizeMatchCardSection(section);
        return $"{normalizedPage}{MatchCardScopeDelimiter}{normalizedSection}";
    }

    private static bool TryParseMatchCardScopeKey(string scopeKey, out string page, out string section)
    {
        page = MatchCardScopePage;
        section = MatchCardSectionGeneral;

        if (string.IsNullOrWhiteSpace(scopeKey))
            return false;

        var split = scopeKey.Split(MatchCardScopeDelimiter, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (split.Length != 2)
            return false;

        page = string.IsNullOrWhiteSpace(split[0]) ? MatchCardScopePage : split[0].Trim().ToLowerInvariant();
        section = NormalizeMatchCardSection(split[1]);
        return true;
    }

    private static string NormalizeMatchCardScopeKey(string scopeKey)
    {
        if (TryParseMatchCardScopeKey(scopeKey, out var page, out var section))
            return BuildMatchCardScopeKey(page, section);

        return BuildMatchCardScopeKey(MatchCardScopePage, NormalizeLegacyMatchCardViewKey(scopeKey));
    }

    private static string NormalizeLegacyMatchCardViewKey(string key)
    {
        var normalized = key.Trim().ToLowerInvariant();
        return normalized switch
        {
            MatchCardSectionGeneral => MatchCardSectionGeneral,
            MatchCardSectionGroups => MatchCardSectionGroups,
            MatchCardSectionKnockout => MatchCardSectionKnockout,
            MatchCardSectionSchedule => MatchCardSectionSchedule,
            MatchCardSectionScheduleQueueLegacy => MatchCardSectionScheduleBoardsUpcoming,
            MatchCardSectionScheduleTimelineLegacy => MatchCardSectionScheduleZeitplan,
            MatchCardSectionBoardDetail => MatchCardSectionBoardDetail,
            _ => MatchCardSectionGeneral
        };
    }

    private static string NormalizeMatchCardSection(string? section)
    {
        var normalized = (section ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            MatchCardScopeSectionAll => MatchCardScopeSectionAll,
            MatchCardSectionGeneral => MatchCardSectionGeneral,
            MatchCardSectionGroups => MatchCardSectionGroups,
            MatchCardSectionKnockout => MatchCardSectionKnockout,
            MatchCardSectionSchedule => MatchCardSectionSchedule,
            MatchCardSectionScheduleBoardsUpcoming => MatchCardSectionScheduleBoardsUpcoming,
            MatchCardSectionScheduleZeitplan => MatchCardSectionScheduleZeitplan,
            MatchCardSectionScheduleQueueLegacy => MatchCardSectionScheduleBoardsUpcoming,
            MatchCardSectionScheduleTimelineLegacy => MatchCardSectionScheduleZeitplan,
            MatchCardSectionBoardDetail => MatchCardSectionBoardDetail,
            _ => MatchCardSectionGeneral
        };
    }

    private MatchCardViewSettings ResolveMatchCardSettingsForSection(string sectionKey)
    {
        var normalizedSection = NormalizeMatchCardSection(sectionKey);
        var candidates = new List<string>
        {
            BuildMatchCardScopeKey(MatchCardScopePage, normalizedSection)
        };

        if (normalizedSection.StartsWith("schedule-", StringComparison.OrdinalIgnoreCase))
            candidates.Add(BuildMatchCardScopeKey(MatchCardScopePage, MatchCardSectionSchedule));

        if (!string.Equals(normalizedSection, MatchCardScopeSectionAll, StringComparison.OrdinalIgnoreCase))
            candidates.Add(BuildMatchCardScopeKey(MatchCardScopePage, MatchCardScopeSectionAll));

        candidates.Add(BuildMatchCardScopeKey(MatchCardScopeGlobalPage, MatchCardScopeSectionAll));

        foreach (var candidate in candidates)
        {
            if (matchCardSettingsByView.TryGetValue(candidate, out var found))
                return found;
        }

        return MatchCardViewSettings.CreateDefault();
    }

    private MatchCardViewSettings GetEditableMatchCardSettings(string scopeKey)
    {
        var normalizedScopeKey = NormalizeMatchCardScopeKey(scopeKey);
        if (!matchCardSettingsByView.TryGetValue(normalizedScopeKey, out var settings))
        {
            if (TryParseMatchCardScopeKey(normalizedScopeKey, out var page, out var section)
                && string.Equals(page, MatchCardScopePage, StringComparison.OrdinalIgnoreCase))
            {
                settings = ResolveMatchCardSettingsForSection(section).Clone();
            }
            else
            {
                settings = MatchCardViewSettings.CreateDefault();
            }

            matchCardSettingsByView[normalizedScopeKey] = settings;
        }

        settings.Normalize();
        return settings;
    }

    private MatchCardViewSettings GetMatchCardSettings(string sectionKey)
        => ResolveMatchCardSettingsForSection(sectionKey);

    private async Task SetGlobalDetailsExpansionAsync(bool? expanded)
    {
        forcedMatchDetailsExpanded = expanded;
        try
        {
            var val = expanded.HasValue ? expanded.Value.ToString().ToLowerInvariant() : "null";
            await JS.InvokeVoidAsync("dartSuiteUi.localStorageSet", "ds-match-details-expanded", val);
        }
        catch { /* best-effort */ }
        await InvokeAsync(StateHasChanged);
    }

    private void DismissListenerError(Guid matchId)
    {
        _dismissedListenerErrors.Add(matchId);
    }

    private void OpenMatchCardScopeEditor(string scopeKey)
    {
        var normalizedScope = NormalizeMatchCardScopeKey(scopeKey);
        matchCardScopeModalKey = normalizedScope;
        // Keep parent modal context aligned with the scope edited in the child modal.
        activeMatchCardConfigScopeKey = normalizedScope;
        showMatchCardScopeModal = true;
        _ = InvokeAsync(StateHasChanged);
    }

    private async Task OnMatchCardScopeModalClosedAsync()
    {
        var editedScope = string.IsNullOrWhiteSpace(matchCardScopeModalKey)
            ? activeMatchCardConfigScopeKey
            : NormalizeMatchCardScopeKey(matchCardScopeModalKey);

        showMatchCardScopeModal = false;
        await LoadMatchCardSettingsAsync();
        activeMatchCardConfigScopeKey = editedScope;
        _ = GetEditableMatchCardSettings(activeMatchCardConfigScopeKey);
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnMatchCardConfigScopeChanged(ChangeEventArgs args)
    {
        var raw = args.Value?.ToString();
        if (string.IsNullOrWhiteSpace(raw))
            return;

        activeMatchCardConfigScopeKey = NormalizeMatchCardScopeKey(raw);
        _ = GetEditableMatchCardSettings(activeMatchCardConfigScopeKey);
        await InvokeAsync(StateHasChanged);
    }

    private async Task UpdateActiveMatchCardSettingsAsync(Action<MatchCardViewSettings> update)
    {
        matchCardPreferenceError = null;

        var settings = ActiveMatchCardConfigSettings;
        update(settings);
        settings.Normalize();

        if (string.IsNullOrWhiteSpace(autodartsDisplayName))
        {
            await InvokeAsync(StateHasChanged);
            return;
        }

        try
        {
            isSavingMatchCardPreferences = true;
            await PersistMatchCardSettingsAsync();
        }
        catch (Exception ex)
        {
            matchCardPreferenceError = $"MatchCard-Einstellungen konnten nicht gespeichert werden: {ex.Message}";
        }
        finally
        {
            isSavingMatchCardPreferences = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private Task UpdateActiveMatchCardLayoutAsync(ChangeEventArgs args)
        => UpdateActiveMatchCardSettingsAsync(s => s.Layout = args.Value?.ToString() ?? "Mixed");

    private static bool ParseBoolChangeEvent(ChangeEventArgs args)
        => args.Value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var parsed) => parsed,
            _ => false
        };

    private async Task OnFallbackPollingIntervalChanged(ChangeEventArgs args)
    {
        if (!int.TryParse(args.Value?.ToString(), out var parsed))
            return;

        fallbackPollingIntervalSeconds = Math.Clamp(parsed, 2, 30);
        realtimePreferenceError = null;
        UpdateAutoRefreshTimerMode();

        if (string.IsNullOrWhiteSpace(autodartsDisplayName))
        {
            await InvokeAsync(StateHasChanged);
            return;
        }

        try
        {
            isSavingRealtimePreference = true;
            var payload = JsonSerializer.Serialize(new TournamentRealtimeSettings(fallbackPollingIntervalSeconds));
            await Api.SaveViewPreferenceAsync(autodartsDisplayName, RealtimePreferenceContext, payload);
        }
        catch (Exception ex)
        {
            realtimePreferenceError = $"Polling-Einstellung konnte nicht gespeichert werden: {ex.Message}";
        }
        finally
        {
            isSavingRealtimePreference = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void CleanupLiveSnapshots()
    {
        var activeMatchIds = matches.Select(m => m.Id).ToHashSet();
        var obsoleteIds = lastMatchDataEventUtcByMatch.Keys.Where(id => !activeMatchIds.Contains(id)).ToList();
        foreach (var obsoleteId in obsoleteIds)
            lastMatchDataEventUtcByMatch.Remove(obsoleteId);

        var obsoleteStatisticsIds = lastStatisticsEventUtcByMatch.Keys.Where(id => !activeMatchIds.Contains(id)).ToList();
        foreach (var obsoleteId in obsoleteStatisticsIds)
            lastStatisticsEventUtcByMatch.Remove(obsoleteId);

        foreach (var finishedMatchId in matches.Where(m => m.FinishedUtc is not null).Select(m => m.Id).ToList())
        {
            lastMatchDataEventUtcByMatch.Remove(finishedMatchId);
            lastStatisticsEventUtcByMatch.Remove(finishedMatchId);
        }

        var obsoleteFollowStateIds = followedMatchStatesById.Keys.Where(id => !activeMatchIds.Contains(id)).ToList();
        foreach (var obsoleteId in obsoleteFollowStateIds)
            followedMatchStatesById.Remove(obsoleteId);

        followOperationInProgressMatchIds.RemoveWhere(id => !activeMatchIds.Contains(id));
    }

    private async Task TryOpenBoardDetailFromQueryAsync()
    {
        if (string.IsNullOrWhiteSpace(QueryBoardId) || !Guid.TryParse(QueryBoardId, out var boardId))
            return;

        var board = boards.FirstOrDefault(b => b.Id == boardId);
        if (board is null)
        {
            board = await Api.GetBoardAsync(boardId);
            if (board is null)
                return;
        }

        if (board is null)
            return;

        if (board.CurrentMatchId.HasValue)
        {
            foreach (var t in tournaments)
            {
                var tMatches = (await Api.GetMatchesAsync(t.Id)).ToList();
                if (tMatches.Any(m => m.Id == board.CurrentMatchId.Value))
                {
                    await SelectTournamentAsync(t);
                    matches = tMatches;
                    OpenBoardDetail(board);
                    return;
                }
            }
        }

        if (selectedTournament is null && AppState.SelectedTournament is not null)
        {
            var appStateTournament = tournaments.FirstOrDefault(t => t.Id == AppState.SelectedTournament.Id);
            if (appStateTournament is not null)
                await SelectTournamentAsync(appStateTournament);
        }

        if (selectedTournament is null)
        {
            var firstTournament = tournaments.FirstOrDefault();
            if (firstTournament is not null)
                await SelectTournamentAsync(firstTournament);
        }

        OpenBoardDetail(board);
    }

    private async Task ConnectToHubAsync()
    {
        try
        {
            HubService.OnConnectionChanged += OnTournamentHubConnectionChanged;
            HubService.OnMatchUpdated += OnHubMatchUpdated;
            HubService.OnBoardsUpdated += OnHubBoardsUpdated;
            HubService.OnParticipantsUpdated += OnHubParticipantsUpdated;
            HubService.OnTournamentUpdated += OnHubTournamentUpdated;
            HubService.OnScheduleUpdated += OnHubScheduleUpdated;
            HubService.OnMatchDataReceived += OnHubMatchDataReceived;
            HubService.OnMatchStatisticsUpdated += OnHubMatchStatisticsUpdated;
            HubService.OnReconnected += OnHubReconnected;
            await HubService.StartAsync();
            isTournamentHubConnected = HubService.IsConnected;
            tournamentHubConnectionError = null;

            // If tournament already selected, join the group
            if (selectedTournament is not null)
                await HubService.JoinTournamentAsync(selectedTournament.Id.ToString());

            UpdateAutoRefreshTimerMode();
        }
        catch (Exception ex)
        {
            isTournamentHubConnected = false;
            tournamentHubConnectionError = ex.Message;
            UpdateAutoRefreshTimerMode();
            // Hub connection is optional — timer fallback handles it.
        }
    }

    private async Task ConnectToBoardHubAsync()
    {
        try
        {
            BoardHubService.OnConnectionChanged += OnBoardHubConnectionChanged;
            BoardHubService.OnBoardAdded += OnBoardChanged;
            BoardHubService.OnBoardStatusChanged += OnBoardChanged;
            BoardHubService.OnBoardConnectionChanged += OnBoardChanged;
            BoardHubService.OnBoardExtensionStatusChanged += OnBoardChanged;
            BoardHubService.OnBoardCurrentMatchChanged += OnBoardChanged;
            BoardHubService.OnBoardManagedModeChanged += OnBoardChanged;
            BoardHubService.OnBoardRemoved += OnBoardRemoved;

            await BoardHubService.StartAsync();
            isBoardHubConnected = BoardHubService.IsConnected;
            boardHubConnectionError = null;
            UpdateAutoRefreshTimerMode();
        }
        catch (Exception ex)
        {
            isBoardHubConnected = false;
            boardHubConnectionError = ex.Message;
            UpdateAutoRefreshTimerMode();
            // Hub connection is optional — timer fallback handles it.
        }
    }

    private async Task OnTournamentHubConnectionChanged(bool connected)
    {
        isTournamentHubConnected = connected;
        if (connected)
            tournamentHubConnectionError = null;
        UpdateAutoRefreshTimerMode();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnBoardHubConnectionChanged(bool connected)
    {
        isBoardHubConnected = connected;
        if (connected)
            boardHubConnectionError = null;
        UpdateAutoRefreshTimerMode();
        await InvokeAsync(StateHasChanged);
    }

    private void UpdateAutoRefreshTimerMode()
    {
        var interval = TimeSpan.FromSeconds(EffectiveFallbackPollingIntervalSeconds);
        _autoRefreshTimer ??= new Timer(OnAutoRefresh, null, interval, interval);
        _autoRefreshTimer.Change(interval, interval);
    }

    private async Task OnBoardChanged(BoardDto board)
    {
        await InvokeAsync(() =>
        {
            var index = boards.FindIndex(b => b.Id == board.Id);
            if (index >= 0)
                boards[index] = board;
            else
                boards.Add(board);

            if (detailBoard?.Id == board.Id)
                detailBoard = board;

            StateHasChanged();
        });
    }

    private async Task OnBoardRemoved(Guid boardId)
    {
        await InvokeAsync(() =>
        {
            boards.RemoveAll(b => b.Id == boardId);
            if (detailBoard?.Id == boardId)
                detailBoard = null;
            StateHasChanged();
        });
    }

    private async Task OnHubMatchUpdated(string tournamentId)
    {
        if (selectedTournament is null || selectedTournament.Id.ToString() != tournamentId) return;

        if (!await IsDocumentVisibleAsync())
        {
            hasPendingVisibilityRefresh = true;
            return;
        }

        await InvokeAsync(async () =>
        {
            matches = (await Api.GetMatchesAsync(selectedTournament.Id)).ToList();
            CleanupLiveSnapshots();

            if (activeTab == "groups")
                groupStandings = (await Api.GetGroupStandingsAsync(selectedTournament.Id)).ToList();
            StateHasChanged();
        });
    }

    private async Task OnHubBoardsUpdated(string _)
    {
        if (!await IsDocumentVisibleAsync())
        {
            hasPendingVisibilityRefresh = true;
            return;
        }

        await InvokeAsync(async () =>
        {
            await LoadBoardsAsync();
            StateHasChanged();
        });
    }

    private async Task OnHubParticipantsUpdated(string tournamentId)
    {
        if (selectedTournament is null || selectedTournament.Id.ToString() != tournamentId) return;

        if (!await IsDocumentVisibleAsync())
        {
            hasPendingVisibilityRefresh = true;
            return;
        }

        await InvokeAsync(async () =>
        {
            await LoadParticipantsAsync(selectedTournament.Id);
            if (IsTeamplayActive)
            {
                await LoadTeamsAsync(selectedTournament.Id);
                BuildTeamDraftsFromServerState();
            }
            StateHasChanged();
        });
    }

    private async Task OnHubTournamentUpdated(string tournamentId)
    {
        if (selectedTournament is null || selectedTournament.Id.ToString() != tournamentId) return;

        if (!await IsDocumentVisibleAsync())
        {
            hasPendingVisibilityRefresh = true;
            return;
        }

        await InvokeAsync(async () =>
        {
            await LoadTournamentsAsync();
            var updated = tournaments.FirstOrDefault(t => t.Id == selectedTournament.Id);
            if (updated is not null) selectedTournament = updated;
            StateHasChanged();
        });
    }

    private async Task OnHubScheduleUpdated(string tournamentId)
    {
        if (selectedTournament is null || selectedTournament.Id.ToString() != tournamentId) return;

        if (!await IsDocumentVisibleAsync())
        {
            hasPendingVisibilityRefresh = true;
            return;
        }

        await InvokeAsync(async () =>
        {
            matches = (await Api.GetMatchesAsync(selectedTournament.Id)).ToList();
            StateHasChanged();
        });
    }

    private async Task OnHubMatchDataReceived(TournamentHubService.MatchDataReceivedDto payload)
    {
        if (selectedTournament is null || payload.TournamentId != selectedTournament.Id)
            return;

        if (!await IsDocumentVisibleAsync())
        {
            hasPendingVisibilityRefresh = true;
            return;
        }

        var effectiveTimestampUtc = (payload.SourceTimestamp ?? payload.Timestamp).ToUniversalTime();

        if (lastMatchDataEventUtcByMatch.TryGetValue(payload.MatchId, out var previousEventUtc)
            && effectiveTimestampUtc <= previousEventUtc)
            return;

        lastMatchDataEventUtcByMatch[payload.MatchId] = effectiveTimestampUtc;

        await InvokeAsync(async () =>
        {
            var current = matches.FirstOrDefault(m => m.Id == payload.MatchId);
            if (current is not null)
            {
                var updated = current with
                {
                    HomeLegs = payload.HomeLegs,
                    AwayLegs = payload.AwayLegs,
                    HomeSets = payload.HomeSets,
                    AwaySets = payload.AwaySets
                };
                var index = matches.FindIndex(m => m.Id == payload.MatchId);
                if (index >= 0)
                    matches[index] = updated;

                if (detailMatch?.Id == payload.MatchId)
                {
                    detailMatch = updated;
                    editHomeLegs = updated.HomeLegs;
                    editAwayLegs = updated.AwayLegs;
                    editHomeSets = updated.HomeSets;
                    editAwaySets = updated.AwaySets;
                }
            }

            if (payload.Finished)
            {
                lastMatchDataEventUtcByMatch.Remove(payload.MatchId);
                lastStatisticsEventUtcByMatch.Remove(payload.MatchId);
            }

            if (payload.StatisticsChanged)
            {
                lastStatisticsEventUtcByMatch[payload.MatchId] = effectiveTimestampUtc;
            }

            var shouldRefreshVisibleStatistics = IsMatchStatisticsVisible(payload.MatchId);
            if (shouldRefreshVisibleStatistics)
                await LoadMatchStatisticsAsync(payload.MatchId);

            if (IsMatchScoreVisible(payload.MatchId) || shouldRefreshVisibleStatistics)
                StateHasChanged();
        });
    }

    private async Task OnHubMatchStatisticsUpdated(TournamentHubService.MatchStatisticsUpdatedDto payload)
    {
        if (selectedTournament is null || payload.TournamentId != selectedTournament.Id)
            return;

        if (!await IsDocumentVisibleAsync())
        {
            hasPendingVisibilityRefresh = true;
            return;
        }

        var effectiveTimestampUtc = (payload.SourceTimestamp ?? payload.Timestamp).ToUniversalTime();

        if (lastStatisticsEventUtcByMatch.TryGetValue(payload.MatchId, out var previousStatisticsEventUtc)
            && effectiveTimestampUtc <= previousStatisticsEventUtc)
            return;

        lastStatisticsEventUtcByMatch[payload.MatchId] = effectiveTimestampUtc;

        if (!IsMatchStatisticsVisible(payload.MatchId))
            return;

        await InvokeAsync(async () =>
        {
            await LoadMatchStatisticsAsync(payload.MatchId);
            StateHasChanged();
        });
    }

    private async Task OnHubReconnected()
    {
        // Re-join tournament group after reconnection
        if (selectedTournament is not null)
        {
            await HubService.JoinTournamentAsync(selectedTournament.Id.ToString());
            // Refresh data after reconnection gap — events may have been missed
            try
            {
                matches = (await Api.GetMatchesAsync(selectedTournament.Id)).ToList();
                var freshTournament = (await Api.GetTournamentsAsync())
                    .FirstOrDefault(t => t.Id == selectedTournament.Id);
                if (freshTournament is not null)
                    selectedTournament = freshTournament;
                await InvokeAsync(StateHasChanged);
            }
            catch { /* Refresh is best-effort; polling will recover */ }
        }
    }

    private async Task TryLoadAutodartsSessionAsync()
    {
        try
        {
            var status = await Api.GetAutodartsStatusAsync();
            isAutodartsConnected = status.IsConnected;
            autodartsDisplayName = status.Profile?.DisplayName;
        }
        catch { /* API might not be reachable */ }
    }

    private async void OnAutoRefresh(object? state)
    {
        if (Interlocked.Exchange(ref _autoRefreshInProgress, 1) == 1)
            return;

        var shouldRunRefresh = IsRealtimeFallbackActive || hasPendingVisibilityRefresh;
        if (!shouldRunRefresh)
        {
            Interlocked.Exchange(ref _autoRefreshInProgress, 0);
            return;
        }

        try
        {
            if (selectedTournament is null) return;

            var pageVisible = await IsDocumentVisibleAsync();
            if (!pageVisible)
                return;

            await InvokeAsync(async () =>
            {
                var forceVisibleRefresh = hasPendingVisibilityRefresh;
                var shouldRefreshBoards = forceVisibleRefresh || activeTab is "boards" or "participants" or "schedule" or "knockout" || detailBoard is not null;
                var shouldRefreshParticipants = forceVisibleRefresh || activeTab is "boards" or "participants" or "draw" or "groups" || detailMatch is not null;
                var shouldRefreshMatches = forceVisibleRefresh || activeTab is "schedule" or "knockout" or "groups" || detailMatch is not null || detailBoard is not null;
                var shouldRefreshGroupStandings = forceVisibleRefresh || activeTab == "groups";

                await LoadMatchListenersAsync();

                if (shouldRefreshBoards)
                    await LoadBoardsAsync();

                if (shouldRefreshParticipants)
                    await LoadParticipantsAsync(selectedTournament.Id);

                if (shouldRefreshMatches)
                {
                    matches = (await Api.GetMatchesAsync(selectedTournament.Id)).ToList();
                    CleanupLiveSnapshots();
                }

                if (shouldRefreshGroupStandings)
                    groupStandings = (await Api.GetGroupStandingsAsync(selectedTournament.Id)).ToList();

                // Auto-sync live match data if detail modal is open with a running external match
                if (detailMatch is not null && !string.IsNullOrEmpty(detailMatch.ExternalMatchId) && detailMatch.FinishedUtc is null && !isSyncing)
                {
                    // Only sync manually if no realtime monitor is handling it.
                    var hasMonitor = matchListeners.Any(l => l.MatchId == detailMatch.Id && (l.IsRunning || l.IsWebSocketActive || string.Equals(l.TransportMode, "websocket", StringComparison.OrdinalIgnoreCase)));
                    if (!hasMonitor)
                    {
                        try
                        {
                            var synced = await Api.SyncMatchFromExternalAsync(detailMatch.Id);
                            if (synced is not null)
                            {
                                detailMatch = synced;
                                editHomeLegs = synced.HomeLegs;
                                editAwayLegs = synced.AwayLegs;
                                matches = (await Api.GetMatchesAsync(selectedTournament.Id)).ToList();
                                CleanupLiveSnapshots();
                            }

                            detailMatchStatistics = (await Api.SyncMatchStatisticsAsync(detailMatch.Id)).ToList();
                        }
                        catch { /* silent — sync may fail if not connected */ }
                    }
                    else
                    {
                        // Listener is active — just refresh the match data from DB
                        try
                        {
                            matches = (await Api.GetMatchesAsync(selectedTournament.Id)).ToList();
                            var updated = matches.FirstOrDefault(m => m.Id == detailMatch.Id);
                            if (updated is not null)
                            {
                                detailMatch = updated;
                                editHomeLegs = updated.HomeLegs;
                                editAwayLegs = updated.AwayLegs;
                            }

                            detailMatchStatistics = (await Api.GetMatchStatisticsAsync(detailMatch.Id)).ToList();
                        }
                        catch { /* silent */ }
                    }
                }

                if (forceVisibleRefresh)
                    hasPendingVisibilityRefresh = false;

                if (shouldRefreshBoards || shouldRefreshParticipants || shouldRefreshMatches || detailMatch is not null)
                    StateHasChanged();
            });
        }
        catch { /* suppress — component may be disposed */ }
        finally
        {
            Interlocked.Exchange(ref _autoRefreshInProgress, 0);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _autoRefreshTimer?.Dispose();

        await DetachTabSwipeInteropAsync();
        await DetachTouchDragDropInteropAsync();
        await DetachTeamDraftOutsideClickInteropAsync();
        tournamentTabSwipeRef?.Dispose();
        tournamentTabSwipeRef = null;
        teamDraftUiRef?.Dispose();
        teamDraftUiRef = null;

        HubService.OnConnectionChanged -= OnTournamentHubConnectionChanged;
        HubService.OnMatchUpdated -= OnHubMatchUpdated;
        HubService.OnBoardsUpdated -= OnHubBoardsUpdated;
        HubService.OnParticipantsUpdated -= OnHubParticipantsUpdated;
        HubService.OnTournamentUpdated -= OnHubTournamentUpdated;
        HubService.OnScheduleUpdated -= OnHubScheduleUpdated;
        HubService.OnMatchDataReceived -= OnHubMatchDataReceived;
        HubService.OnMatchStatisticsUpdated -= OnHubMatchStatisticsUpdated;
        HubService.OnReconnected -= OnHubReconnected;

        BoardHubService.OnConnectionChanged -= OnBoardHubConnectionChanged;
        BoardHubService.OnBoardAdded -= OnBoardChanged;
        BoardHubService.OnBoardStatusChanged -= OnBoardChanged;
        BoardHubService.OnBoardConnectionChanged -= OnBoardChanged;
        BoardHubService.OnBoardExtensionStatusChanged -= OnBoardChanged;
        BoardHubService.OnBoardCurrentMatchChanged -= OnBoardChanged;
        BoardHubService.OnBoardManagedModeChanged -= OnBoardChanged;
        BoardHubService.OnBoardRemoved -= OnBoardRemoved;

        if (selectedTournament is not null)
        {
            try { await HubService.LeaveTournamentAsync(selectedTournament.Id.ToString()); }
            catch { /* suppress */ }
        }
    }

    private string TournamentLink => selectedTournament is not null
        ? $"{Navigation.BaseUri}tournaments?tournamentId={selectedTournament.Id}"
        : string.Empty;

    private async Task CopyTournamentLink()
    {
        await JS.InvokeVoidAsync("navigator.clipboard.writeText", TournamentLink);
    }

    private async Task DeleteTournamentAsync()
    {
        if (selectedTournament is null) return;
        try
        {
            isWorking = true;
            await Api.DeleteTournamentAsync(selectedTournament.Id);
            await LoadTournamentsAsync();
            selectedTournament = tournaments.FirstOrDefault();
            if (selectedTournament is not null)
                await SelectTournamentAsync(selectedTournament);
        }
        catch (Exception ex) { editError = ex.Message; }
        finally { isWorking = false; }
    }

    private void RequestDeleteTournamentAsync()
    {
        if (selectedTournament is null)
            return;

        confirmationMessage = $"Turnier \"{selectedTournament.Name}\" wirklich dauerhaft löschen?";
        showConfirmationPlanImpact = false;
        confirmationAction = DeleteTournamentAsync;
        showConfirmation = true;
    }

    private bool IsParticipantReferencedInMatches(ParticipantDto participant)
        => matches.Any(m => m.HomeParticipantId == participant.Id
            || m.AwayParticipantId == participant.Id
            || m.WinnerParticipantId == participant.Id);

    private bool CanRemoveParticipant(ParticipantDto participant)
        => !isWorking
        && selectedTournament is not null
        && !selectedTournament.IsLocked
        && !IsParticipantReferencedInMatches(participant);

    private string RemoveParticipantDisabledReason(ParticipantDto participant)
    {
        if (IsParticipantReferencedInMatches(participant))
            return "Teilnehmer ist bereits in Matches referenziert und kann nicht gelöscht werden.";

        return "Teilnehmer löschen";
    }

    private async Task LoadTournamentsAsync()
        => tournaments = (await Api.GetTournamentsAsync()).ToList();

    private async Task LoadBoardsAsync(Guid? tournamentId = null)
    {
        var effectiveTournamentId = tournamentId ?? selectedTournament?.Id;
        if (effectiveTournamentId.HasValue)
        {
            boards = (await Api.GetBoardsByTournamentAsync(effectiveTournamentId.Value)).ToList();
            return;
        }

        boards = (await Api.GetBoardsAsync())
            .Where(b => !b.TournamentId.HasValue)
            .ToList();
    }

    private async Task LoadMatchListenersAsync()
    {
        try
        {
            matchListeners = (await Api.GetMatchListenersAsync()).ToList();
            // Remove dismissed entries that no longer have the error.
            _dismissedListenerErrors.RemoveWhere(id =>
                matchListeners.All(l => l.MatchId != id || l.LastError is null));
        }
        catch { /* API might not be reachable */ }
    }

    // ─── Wizard ───
    private void StartWizard()
    {
        isCreating = true;
        wizardStep = 1;
        wizardName = string.Empty;
        wizardOrganizer = autodartsDisplayName ?? "manager";
        wizardStartDate = DateTime.Today;
        wizardEndDate = null;
        wizardStartTime = null;
        wizardMode = "Knockout";
        wizardVariant = "OnSite";
        wizardTeamplay = false;
        wizardError = null;
    }

    private void CancelWizard() { isCreating = false; wizardError = null; }

    private void WizardNext()
    {
        if (string.IsNullOrWhiteSpace(wizardName)) { wizardError = "Turniername ist erforderlich."; return; }
        wizardError = null;
        wizardStep = 2;
    }

    private async Task FinishWizardAsync()
    {
        try
        {
            isWorking = true;
            wizardError = null;
            var start = DateOnly.FromDateTime(wizardStartDate);
            var end = wizardEndDate.HasValue ? DateOnly.FromDateTime(wizardEndDate.Value) : start;
            var created = await Api.CreateTournamentAsync(new CreateTournamentRequest(
                wizardName.Trim(), wizardOrganizer.Trim(), start, end, wizardTeamplay, wizardMode, wizardVariant, wizardStartTime));
            isCreating = false;
            await LoadTournamentsAsync();
            var t = tournaments.FirstOrDefault(x => x.Id == created.Id);
            if (t is not null) await SelectTournamentAsync(t);
        }
        catch (Exception ex) { wizardError = ex.Message; }
        finally { isWorking = false; }
    }

    // ─── Tournament Selection ───
    private async Task SelectTournamentAsync(TournamentDto tournament)
    {
        tournamentListMobileOpen = false;
        tournamentListCollapsed = true;

        // Leave previous tournament hub group
        if (selectedTournament is not null && selectedTournament.Id != tournament.Id)
        {
            try { await HubService.LeaveTournamentAsync(selectedTournament.Id.ToString()); }
            catch { /* suppress */ }
        }

        selectedTournament = tournament;
        AppState.SetSelectedTournament(tournament);
        activeTab = "general";
        editError = null;
        editSuccess = null;
        participantError = null;
        detailMatch = null;
        lastMatchDataEventUtcByMatch.Clear();
        lastStatisticsEventUtcByMatch.Clear();
        hasPendingVisibilityRefresh = false;
        PopulateEditFields(tournament);
        await Task.WhenAll(
            LoadParticipantsAsync(tournament.Id),
            LoadTeamsAsync(tournament.Id),
            LoadMatchesAsync(tournament.Id),
            LoadBoardsAsync(tournament.Id),
            LoadRoundsAsync(),
            LoadScoringCriteriaAsync(tournament.Id));

        // Backend enforces authorization for cleanup.
        try
        {
            _ = Api.CleanupStaleMatchesAsync(tournament.Id)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        ConsoleWarn($"AutoCleanup konnte nicht ausgeführt werden: {t.Exception?.GetBaseException().Message}");
                    else
                        ConsoleLog("AutoCleanup für Matches ausgeführt.");
                });
        }
        catch (Exception ex)
        {
            ConsoleWarn($"AutoCleanup konnte nicht ausgeführt werden: {ex.Message}");
        }

        if (tournament.Mode == "Knockout")
            EnsureKnockoutDrawCards();
        if (tournament.Mode == "GroupAndKnockout" && matches.Any(m => m.Phase == "Group"))
            groupStandings = (await Api.GetGroupStandingsAsync(tournament.Id)).ToList();

        // Join the new tournament hub group
        try { await HubService.JoinTournamentAsync(tournament.Id.ToString()); }
        catch { /* suppress */ }

        // Hilfsfunktionen für Konsolen-Logging
        void ConsoleWarn(string msg) => _ = JS.InvokeVoidAsync("console.warn", msg);
        void ConsoleLog(string msg) => _ = JS.InvokeVoidAsync("console.log", msg);
    }

    private void PopulateEditFields(TournamentDto t)
    {
        editName = t.Name;
        editOrganizer = t.OrganizerAccount;
        editStartDate = t.StartDate.ToDateTime(TimeOnly.MinValue);
        editEndDate = t.EndDate.ToDateTime(TimeOnly.MinValue);
        editStartTime = t.StartTime;
        editMode = t.Mode;
        editVariant = t.Variant;
        editTeamplay = t.TeamplayEnabled;
        editThirdPlaceMatch = t.ThirdPlaceMatch;
        editGroupCount = t.GroupCount > 0 ? t.GroupCount : 2;
        editPlayoffAdvancers = t.PlayoffAdvancers;
        editKnockoutsPerRound = t.KnockoutsPerRound;
        editMatchesPerOpponent = t.MatchesPerOpponent;
        editGroupMode = t.GroupMode;
        editGroupDrawMode = t.GroupDrawMode;
        editPlanningVariant = t.PlanningVariant;
        editGroupOrderMode = t.GroupOrderMode;
        editWinPoints = t.WinPoints;
        editLegFactor = t.LegFactor;
        editPlayersPerTeam = t.PlayersPerTeam;
        editAreGameModesLocked = t.AreGameModesLocked;
        editIsRegistrationOpen = t.IsRegistrationOpen;
        editRegistrationStart = t.RegistrationStartUtc?.LocalDateTime;
        editRegistrationEnd = t.RegistrationEndUtc?.LocalDateTime;
        editDiscordWebhookUrl = t.DiscordWebhookUrl;
        editDiscordWebhookDisplayText = t.DiscordWebhookDisplayText;
        editSeedingEnabled = t.SeedingEnabled;
        editSeedTopCount = t.SeedTopCount;
        scoringCriteriaError = null;
    }

    private async Task LoadScoringCriteriaAsync(Guid tournamentId)
    {
        var configured = (await Api.GetScoringCriteriaAsync(tournamentId)).ToList();
        scoringCriteria.Clear();

        if (configured.Count == 0)
        {
            for (var i = 0; i < AllScoringCriterionTypes.Count; i++)
            {
                var type = AllScoringCriterionTypes[i];
                scoringCriteria.Add(new ScoringCriterionEditorItem
                {
                    Type = type,
                    Priority = i + 1,
                    IsEnabled = DefaultEnabledScoringCriteria.Contains(type)
                });
            }

            return;
        }

        var configuredByType = configured
            .GroupBy(c => c.Type, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Priority).First(), StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < AllScoringCriterionTypes.Count; i++)
        {
            var type = AllScoringCriterionTypes[i];
            if (configuredByType.TryGetValue(type, out var existing))
            {
                scoringCriteria.Add(new ScoringCriterionEditorItem
                {
                    Type = type,
                    Priority = existing.Priority,
                    IsEnabled = existing.IsEnabled
                });
            }
            else
            {
                scoringCriteria.Add(new ScoringCriterionEditorItem
                {
                    Type = type,
                    Priority = i + 100,
                    IsEnabled = false
                });
            }
        }

        NormalizeScoringCriteriaPriorities();
    }

    private static string ScoringCriterionLabel(string type)
        => type switch
        {
            "Points" => "Punkte",
            "DirectDuel" => "Direktes Duell",
            "LegDifference" => "Leg-Differenz",
            "WonLegs" => "Gewonnene Legs",
            "Average" => "Tournament Average",
            "HighestAverage" => "Highest Average",
            "HighestCheckout" => "Highest Checkout",
            "AverageDartsPerLeg" => "Average Darts/Leg",
            "CheckoutPercentage" => "Checkout-%",
            "Breaks" => "Breaks",
            "LotDraw" => "Losentscheid",
            _ => type
        };

    private void MoveScoringCriterionUp(int index)
    {
        if (index <= 0 || index >= scoringCriteria.Count)
            return;

        (scoringCriteria[index - 1], scoringCriteria[index]) = (scoringCriteria[index], scoringCriteria[index - 1]);
        NormalizeScoringCriteriaPriorities();
    }

    private void MoveScoringCriterionDown(int index)
    {
        if (index < 0 || index >= scoringCriteria.Count - 1)
            return;

        (scoringCriteria[index], scoringCriteria[index + 1]) = (scoringCriteria[index + 1], scoringCriteria[index]);
        NormalizeScoringCriteriaPriorities();
    }

    private void NormalizeScoringCriteriaPriorities()
    {
        for (var i = 0; i < scoringCriteria.Count; i++)
            scoringCriteria[i].Priority = i + 1;
    }

    private async Task SaveScoringCriteriaAsync()
    {
        if (selectedTournament is null)
            return;

        if (!EnsureTournamentStructureEditable(message => scoringCriteriaError = message))
            return;

        try
        {
            isSavingScoringCriteria = true;
            scoringCriteriaError = null;

            NormalizeScoringCriteriaPriorities();

            var request = new SaveScoringCriteriaRequest(
                selectedTournament.Id,
                scoringCriteria
                    .Select(c => new ScoringCriterionDto(Guid.Empty, c.Type, c.Priority, c.IsEnabled))
                    .ToList());

            await Api.SaveScoringCriteriaAsync(selectedTournament.Id, request);
            await LoadScoringCriteriaAsync(selectedTournament.Id);

            if (selectedTournament.Mode == "GroupAndKnockout")
                groupStandings = (await Api.GetGroupStandingsAsync(selectedTournament.Id)).ToList();
        }
        catch (Exception ex)
        {
            scoringCriteriaError = ex.Message;
        }
        finally
        {
            isSavingScoringCriteria = false;
        }
    }

    // ─── Save Tournament (auto-save: each setting change triggers this) ───
    private async Task SaveTournamentAsync()
    {
        if (selectedTournament is null) return;
        editError = null;
        editSuccess = null;

        var t = selectedTournament;
        var reasons = new List<string>();
        var newStart = DateOnly.FromDateTime(editStartDate);
        var newEnd = DateOnly.FromDateTime(editEndDate);
        if (newStart != t.StartDate || newEnd != t.EndDate) reasons.Add("Datum");
        if (editMode != t.Mode) reasons.Add("Modus");
        if (editTeamplay != t.TeamplayEnabled) reasons.Add("Teamplay");

        var teamplayBeingDisabled = editTeamplay != t.TeamplayEnabled && !editTeamplay;

        if (reasons.Count > 0 && matches.Any())
        {
            var planMessage = $"Die Änderung von {string.Join(", ", reasons)} wirkt sich auf den bestehenden Turnierplan aus.";
            if (teamplayBeingDisabled && teams.Any())
                planMessage += $" Alle {teams.Count} Team-Zuordnung(en) werden dabei unwiderruflich gelöscht.";
            confirmationMessage = planMessage;
            confirmationAction = ExecuteSaveTournamentAsync;
            showConfirmation = true;
            return;
        }

        // Teamplay deaktivieren mit bestehenden Teams — auch ohne Matches warnen.
        if (teamplayBeingDisabled && teams.Any())
        {
            confirmationMessage = $"Teamplay deaktivieren löscht alle {teams.Count} bestehenden Team-Zuordnung(en) unwiderruflich.";
            confirmationAction = ExecuteSaveTournamentAsync;
            showConfirmationPlanImpact = false;
            showConfirmation = true;
            return;
        }

        await ExecuteSaveTournamentAsync();
    }

    /// <summary>Auto-save: fired on every individual setting change.</summary>
    private async Task AutoSaveSettingAsync()
    {
        await SaveTournamentAsync();
    }

    /// <summary>Fired when the "Registrierung offen" checkbox changes.
    /// Auto-fills start = now and end = MaxDate when registration is activated.</summary>
    private async Task OnRegistrationOpenChangedAsync()
    {
        if (editIsRegistrationOpen)
        {
            editRegistrationStart = DateTime.Now;
            editRegistrationEnd = MaxRegistrationDate;
        }
        await AutoSaveSettingAsync();
    }

    /// <summary>Closes the registration (sets IsRegistrationOpen = false) and auto-saves.</summary>
    private async Task CloseRegistrationAsync()
    {
        editIsRegistrationOpen = false;
        await AutoSaveSettingAsync();
    }

    /// <summary>User accepted the registration-close confirmation before proceeding with draw/plan.</summary>
    private async Task AcceptRegistrationDrawConfirmationAsync()
    {
        showRegistrationDrawConfirmation = false;
        var continuation = registrationDrawContinuation;
        registrationDrawContinuation = null;
        await CloseRegistrationAsync();
        if (continuation is not null)
            await continuation();
    }

    /// <summary>User rejected the registration-close confirmation (cancelled the draw/plan action).</summary>
    private void RejectRegistrationDrawConfirmation()
    {
        showRegistrationDrawConfirmation = false;
        registrationDrawContinuation = null;
    }

    private async Task OnSeedingEnabledChangedAsync()
    {
        var maxSeedCount = Math.Max(0, EffectiveDrawParticipants.Count);
        editSeedTopCount = Math.Clamp(editSeedTopCount, 0, maxSeedCount);

        if (!editSeedingEnabled)
            editSeedTopCount = 0;

        await AutoSaveSettingAsync();

        if (editSeedingEnabled)
        {
            await NormalizeSeedRanksAsync();
            return;
        }

        selectedTeamSeedIndex = null;
        await ResetAllSeedRanksAsync();
    }

    private async Task OnSeedTopCountChangedAsync()
    {
        var maxSeedCount = Math.Max(0, EffectiveDrawParticipants.Count);
        editSeedTopCount = Math.Clamp(editSeedTopCount, 0, maxSeedCount);
        await AutoSaveSettingAsync();

        if (!editSeedingEnabled)
            return;

        await NormalizeSeedRanksAsync();
    }

    private async Task OnPlayersPerTeamChangedAsync()
    {
        editPlayersPerTeam = Math.Max(1, editPlayersPerTeam);
        await AutoSaveSettingAsync();

        if (!IsTeamplayActive)
            return;

        EnsureTeamDraftSlots();
        await InvokeAsync(StateHasChanged);
    }

    private async Task ExecuteSaveTournamentAsync()
    {
        if (selectedTournament is null) return;
        var teamplayChanged = editTeamplay != selectedTournament.TeamplayEnabled;
        try
        {
            isWorking = true;
            var start = DateOnly.FromDateTime(editStartDate);
            var end = DateOnly.FromDateTime(editEndDate);
            var updated = await Api.UpdateTournamentAsync(new UpdateTournamentRequest(
                selectedTournament.Id, editName.Trim(), editOrganizer.Trim(), start, end,
                editTeamplay, editMode, editVariant, editStartTime,
                editGroupCount, editPlayoffAdvancers, editKnockoutsPerRound, editMatchesPerOpponent,
                editGroupMode, editGroupDrawMode, editPlanningVariant, editGroupOrderMode,
                editThirdPlaceMatch, editPlayersPerTeam, editWinPoints, editLegFactor, editAreGameModesLocked,
                editIsRegistrationOpen,
                editRegistrationStart.HasValue ? new DateTimeOffset(editRegistrationStart.Value) : null,
                editRegistrationEnd.HasValue ? new DateTimeOffset(editRegistrationEnd.Value) : null,
                editDiscordWebhookUrl, editDiscordWebhookDisplayText,
                editSeedingEnabled, editSeedTopCount));
            await LoadTournamentsAsync();
            selectedTournament = tournaments.FirstOrDefault(x => x.Id == updated.Id) ?? updated;
            PopulateEditFields(selectedTournament);
            if (teamplayChanged)
            {
                await LoadParticipantsAsync(selectedTournament.Id);
                await LoadTeamsAsync(selectedTournament.Id);
            }
        }
        catch (Exception ex) { editError = ex.Message; }
        finally { isWorking = false; }
    }

    // ─── Confirmation Dialog ───
    private async Task AcceptConfirmation()
    {
        showConfirmation = false;
        showConfirmationPlanImpact = true;
        if (confirmationAction is not null) await confirmationAction();
        confirmationAction = null;
    }

    private void RejectConfirmation()
    {
        showConfirmation = false;
        showConfirmationPlanImpact = true;
        confirmationAction = null;
        if (selectedTournament is not null) PopulateEditFields(selectedTournament);
    }

    // ─── Lock / Unlock ───
    private async Task ToggleLockAsync()
    {
        if (selectedTournament is null) return;
        try
        {
            isWorking = true;
            var updated = await Api.SetTournamentLockedAsync(selectedTournament.Id, !selectedTournament.IsLocked);
            await LoadTournamentsAsync();
            selectedTournament = tournaments.FirstOrDefault(x => x.Id == updated.Id) ?? updated;
            PopulateEditFields(selectedTournament);
        }
        finally { isWorking = false; }
    }

    // ─── Participants ───
    private async Task LoadParticipantsAsync(Guid tournamentId)
    {
        participants = (await Api.GetParticipantsAsync(tournamentId)).ToList();
        var maxSeedCount = Math.Max(0, EffectiveDrawParticipants.Count);
        editSeedTopCount = Math.Clamp(editSeedTopCount, 0, maxSeedCount);
        if (selectedTournament?.Mode == "Knockout")
            EnsureKnockoutDrawCards();
        CleanupKnockoutDrawCards();
        EnsureTeamDraftSlots();
    }

    private async Task LoadTeamsAsync(Guid tournamentId)
    {
        teams = (await Api.GetTeamsAsync(tournamentId)).ToList();
        BuildTeamDraftsFromServerState();
    }

    private void BuildTeamDraftsFromServerState()
    {
        teamDrafts = teams
            .Select(t =>
            {
                var memberIds = t.Members.Select(m => m.Id).ToList();
                var autoName = t.Members.Count > 0
                    ? string.Join("/", t.Members.Select(m => m.DisplayName))
                    : "Team";
                return new TeamDraftItem
                {
                    UiKey = t.Id.ToString("N"),
                    TeamId = t.Id,
                    Name = t.Name,
                    IsAutoName = string.Equals(t.Name, autoName, StringComparison.OrdinalIgnoreCase),
                    MemberParticipantIds = memberIds
                };
            })
            .ToList();

        SortTeamDraftsBySeedInMemory();
        EnsureTeamDraftSlots();
        selectedTeamParticipantId = null;
        draggedTeamParticipantId = null;
        dropTargetTeamIndex = null;
        hasUnsavedTeamDraftChanges = false;
        teamDraftError = null;
    }

    private void EnsureTeamDraftSlots()
    {
        if (!IsTeamplayActive)
        {
            teamDrafts = [];
            return;
        }

        var targetSlots = Math.Max(1, RequiredTeamCount);
        while (teamDrafts.Count < targetSlots)
        {
            teamDrafts.Add(new TeamDraftItem
            {
                UiKey = Guid.NewGuid().ToString("N"),
                Name = $"Team {teamDrafts.Count + 1}",
                IsAutoName = true
            });
        }

        if (teamDrafts.Count > targetSlots)
            teamDrafts = teamDrafts.Take(targetSlots).ToList();
    }

    private string BuildAutoTeamName(IEnumerable<Guid> memberIds, int? teamIndex = null)
    {
        var names = memberIds
            .Select(id => participants.FirstOrDefault(p => p.Id == id)?.DisplayName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToList();

        if (names.Count > 0)
            return string.Join("/", names);

        return teamIndex.HasValue ? $"Team {teamIndex.Value + 1}" : "Team";
    }

    private string TeamDraftDisplayName(TeamDraftItem draft, int teamIndex)
        => string.IsNullOrWhiteSpace(draft.Name) ? $"Team {teamIndex + 1}" : draft.Name;

    private string TeamDraftNameHint(TeamDraftItem draft)
        => draft.IsAutoName
            ? "Automatischer Name aus Mitgliedern. Klicken zum Bearbeiten."
            : "Benutzerdefinierter Name. Klicken zum Bearbeiten.";

    private string TeamSeedCardDragCss(int index)
    {
        if (!isTeamSeedDragging || !draggedTeamSeedIndex.HasValue)
            return string.Empty;

        if (draggedTeamSeedIndex.Value == index)
            return "team-seed-card-drag-source";

        return "team-seed-card-drag-hover";
    }

    private string TeamSeedInsertMarkerCss(int index)
        => isTeamSeedDragging && teamSeedInsertTargetIndex == index
            ? "team-seed-insert-marker active"
            : "team-seed-insert-marker";

    private void CancelTeamSeedLongPress()
    {
        if (teamSeedLongPressCts is null)
            return;

        try
        {
            teamSeedLongPressCts.Cancel();
        }
        catch
        {
            // ignore cancellation races
        }
        finally
        {
            teamSeedLongPressCts.Dispose();
            teamSeedLongPressCts = null;
        }
    }

    private async Task OnTeamSeedPointerDownAsync(int teamIndex, PointerEventArgs e)
    {
        if (!isCompactViewport || !CanSeedTeams || !CanEditTournamentStructure)
            return;

        if (!string.Equals(e.PointerType, "touch", StringComparison.OrdinalIgnoreCase))
            return;

        CancelTeamSeedLongPress();
        var cts = new CancellationTokenSource();
        teamSeedLongPressCts = cts;

        try
        {
            await Task.Delay(TeamSeedLongPressMs, cts.Token);
            if (cts.IsCancellationRequested)
                return;

            selectedTeamSeedIndex = teamIndex;
            await InvokeAsync(StateHasChanged);
        }
        catch (TaskCanceledException)
        {
            // expected when pointer is released before long-press threshold
        }
        finally
        {
            if (ReferenceEquals(teamSeedLongPressCts, cts))
            {
                teamSeedLongPressCts.Dispose();
                teamSeedLongPressCts = null;
            }
        }
    }

    private void OnTeamSeedPointerUp(PointerEventArgs e)
    {
        if (!string.Equals(e.PointerType, "touch", StringComparison.OrdinalIgnoreCase))
            return;

        CancelTeamSeedLongPress();
    }

    private void OnTeamSeedPointerCancel(PointerEventArgs e)
    {
        if (!string.Equals(e.PointerType, "touch", StringComparison.OrdinalIgnoreCase))
            return;

        CancelTeamSeedLongPress();
    }

    private async Task OnTeamSeedBadgeClickAsync(int teamIndex)
    {
        if (!isCompactViewport || !CanSeedTeams || !CanEditTournamentStructure)
            return;

        if (!selectedTeamSeedIndex.HasValue)
        {
            selectedTeamSeedIndex = teamIndex;
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (selectedTeamSeedIndex.Value == teamIndex)
        {
            selectedTeamSeedIndex = null;
            await InvokeAsync(StateHasChanged);
            return;
        }

        draggedTeamSeedIndex = selectedTeamSeedIndex.Value;
        isTeamSeedDragging = true;
        await DropTeamSeedAtAsync(teamIndex);
        selectedTeamSeedIndex = null;
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnTeamSeedCardTapAsync(int teamIndex)
    {
        if (!isCompactViewport || !CanSeedTeams || !CanEditTournamentStructure)
            return;

        if (!selectedTeamSeedIndex.HasValue)
            return;

        if (selectedTeamSeedIndex.Value == teamIndex)
        {
            selectedTeamSeedIndex = null;
            await InvokeAsync(StateHasChanged);
            return;
        }

        draggedTeamSeedIndex = selectedTeamSeedIndex.Value;
        isTeamSeedDragging = true;
        await DropTeamSeedAtAsync(teamIndex);
        selectedTeamSeedIndex = null;
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadMatchesAsync(Guid tournamentId)
        => matches = (await Api.GetMatchesAsync(tournamentId)).ToList();

    private async Task HandleParticipantKeyAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await AddParticipantAsync();
    }

    private async Task OnParticipantNameInput(ChangeEventArgs e)
    {
        participantName = e.Value?.ToString() ?? string.Empty;
        if (participantName.Length >= 2)
        {
            try
            {
                participantSuggestions = (await Api.SearchParticipantsAsync(participantName)).ToList();
                showParticipantSuggestions = participantSuggestions.Count > 0;
            }
            catch { showParticipantSuggestions = false; }
        }
        else
        {
            showParticipantSuggestions = false;
            participantSuggestions = [];
        }
    }

    private void SelectParticipantSuggestion(ParticipantDto suggestion)
    {
        participantName = suggestion.DisplayName;
        participantAccount = suggestion.AccountName;
        participantIsAutodarts = suggestion.IsAutodartsAccount;
        participantIsManager = suggestion.IsManager;
        showParticipantSuggestions = false;
    }

    private void OnAutodartsCheckChanged(ChangeEventArgs e)
    {
        participantIsAutodarts = e.Value is true;
        if (participantIsAutodarts)
        {
            // Auto-fill account name from display name
            participantAccount = string.Empty;
        }
    }

    private async Task AddParticipantAsync()
    {
        if (selectedTournament is null || string.IsNullOrWhiteSpace(participantName)) return;
        var account = participantIsAutodarts
            ? participantName.Trim().ToLowerInvariant().Replace(" ", ".")
            : string.IsNullOrWhiteSpace(participantAccount)
                ? participantName.Trim().ToLowerInvariant().Replace(" ", ".")
                : participantAccount.Trim();
        try
        {
            isWorking = true;
            participantError = null;
            await Api.AddParticipantAsync(new AddParticipantRequest(
                selectedTournament.Id, participantName.Trim(), account,
                participantIsAutodarts, participantIsManager, participants.Count + 1));
            participantName = string.Empty;
            participantAccount = string.Empty;
            participantIsManager = false;
            await LoadParticipantsAsync(selectedTournament.Id);
            await LoadTournamentsAsync();
            selectedTournament = tournaments.FirstOrDefault(x => x.Id == selectedTournament.Id) ?? selectedTournament;
        }
        catch (InvalidOperationException ex) { participantError = ex.Message; }
        catch { participantError = "Ein unerwarteter Fehler ist aufgetreten."; }
        finally { isWorking = false; }
    }

    private async Task RemoveParticipantAsync(ParticipantDto p)
    {
        if (selectedTournament is null) return;

        if (IsParticipantReferencedInMatches(p))
        {
            participantError = $"Teilnehmer \"{p.DisplayName}\" ist bereits in Matches referenziert und kann nicht gelöscht werden. Bitte stattdessen bearbeiten/ersetzen.";
            return;
        }

        if (matches.Any())
        {
            confirmationMessage = $"Durch das Entfernen von \"{p.DisplayName}\" wird der bestehende Turnierplan ungültig.";
            showConfirmationPlanImpact = true;
            confirmationAction = async () => await ExecuteRemoveParticipantAsync(p);
            showConfirmation = true;
            return;
        }
        await ExecuteRemoveParticipantAsync(p);
    }

    private async Task ExecuteRemoveParticipantAsync(ParticipantDto p)
    {
        if (selectedTournament is null) return;
        try
        {
            isWorking = true;
            await Api.RemoveParticipantAsync(selectedTournament.Id, p.Id);
            await LoadParticipantsAsync(selectedTournament.Id);
            await LoadTournamentsAsync();
            selectedTournament = tournaments.FirstOrDefault(x => x.Id == selectedTournament.Id) ?? selectedTournament;
        }
        catch (Exception ex) { participantError = ex.Message; }
        finally { isWorking = false; }
    }

    // ─── Participant Edit ───
    private void StartEditParticipant(ParticipantDto p)
    {
        editingParticipant = p;
        editPDisplayName = p.DisplayName;
        editPAccountName = p.AccountName;
        editPIsAutodarts = p.IsAutodartsAccount;
        editPIsManager = p.IsManager;
        editPError = null;
    }

    private void CancelEditParticipant() { editingParticipant = null; editPError = null; }

    private async Task SaveParticipantEditAsync()
    {
        if (selectedTournament is null || editingParticipant is null) return;
        try
        {
            isWorking = true;
            editPError = null;
            await Api.UpdateParticipantAsync(selectedTournament.Id, new UpdateParticipantRequest(
                selectedTournament.Id, editingParticipant.Id,
                editPDisplayName.Trim(), editPAccountName.Trim(),
                editPIsAutodarts, editPIsManager, editingParticipant.Seed, editingParticipant.SeedPot, editingParticipant.GroupNumber));
            editingParticipant = null;
            await LoadParticipantsAsync(selectedTournament.Id);
        }
        catch (Exception ex) { editPError = ex.Message; }
        finally { isWorking = false; }
    }

    // ─── Board Delete ───
    private async Task DeleteBoardAsync(Guid boardId)
    {
        try
        {
            isWorking = true;
            await Api.DeleteBoardAsync(boardId);
            await LoadBoardsAsync();
        }
        finally { isWorking = false; }
    }

    // ─── Match Generation ───
    private async Task GenerateMatchesAsync()
    {
        if (selectedTournament is null) return;
        try
        {
            isWorking = true;
            matches = (await Api.GenerateMatchesAsync(selectedTournament.Id)).ToList();
            activeTab = selectedTournament.Mode == "GroupAndKnockout" ? "draw" : "knockout";
        }
        finally { isWorking = false; }
    }

    private async Task GenerateGroupMatchesAsync()
    {
        if (selectedTournament is null) return;
        try
        {
            isWorking = true;
            await Api.GenerateGroupMatchesAsync(selectedTournament.Id);
            matches = (await Api.GetMatchesAsync(selectedTournament.Id)).ToList();
            groupStandings = (await Api.GetGroupStandingsAsync(selectedTournament.Id)).ToList();
            activeTab = "groups";
        }
        finally { isWorking = false; }
    }

    private async Task RegenerateMatchesAsync()
    {
        if (!EnsureTournamentStructureEditable(message => editError = message))
            return;

        confirmationMessage = HasPlayedMatches()
            ? "⚠ Es wurden bereits Matches gespielt. Alle Ergebnisse werden gelöscht und der Turnierplan komplett neu generiert. Wirklich fortfahren?"
            : "Der bestehende Turnierplan wird gelöscht und neu generiert.";
        confirmationAction = async () =>
        {
            if (selectedTournament is null) return;
            try
            {
                isWorking = true;
                if (selectedTournament.Mode == "GroupAndKnockout")
                {
                    await Api.GenerateGroupMatchesAsync(selectedTournament.Id);
                    groupStandings = (await Api.GetGroupStandingsAsync(selectedTournament.Id)).ToList();
                }
                matches = (await Api.GenerateMatchesAsync(selectedTournament.Id)).ToList();
                activeTab = selectedTournament.Mode == "GroupAndKnockout" ? "draw" : "knockout";
            }
            finally { isWorking = false; }
        };
        showConfirmation = true;
    }

    // ─── Schedule ───
    private async Task GenerateScheduleAsync()
    {
        if (selectedTournament is null) return;
        try
        {
            isWorking = true;
            matches = (await Api.GenerateScheduleAsync(selectedTournament.Id)).ToList();
            activeTab = "schedule";
        }
        finally { isWorking = false; }
    }

    private bool CanManageScheduleInteractions =>
        selectedTournament is not null
        && !selectedTournament.IsLocked
        && IsCurrentUserManager;

    private bool IsScheduleDragActive => draggedScheduleMatchId.HasValue;

    private bool IsScheduleDragLocked(MatchDto match)
    {
        // Lock wenn Start-Zeit explizit gesperrt ist
        if (match.IsStartTimeLocked)
            return true;

        // Lock wenn Match bereits gestartet oder beendet ist
        if (match.StartedUtc is not null || match.FinishedUtc is not null)
            return true;

        // Lock nur wenn Match aktiv lauft oder beendet ist (nicht beim Warten/Geplant)
        return string.Equals(match.Status, "Beendet", StringComparison.OrdinalIgnoreCase)
            || string.Equals(match.Status, "Aktiv", StringComparison.OrdinalIgnoreCase);
    }

    private bool CanDragScheduleMatch(MatchDto match)
        => CanManageScheduleInteractions && !isWorking && !IsScheduleDragLocked(match);

    private bool CanAssignBoardToMatch(MatchDto match)
        => CanManageScheduleInteractions
           && !isWorking
           && !match.IsBoardLocked
           && !IsScheduleDragLocked(match);

    private void StartScheduleMatchDrag(Guid matchId)
    {
        var match = matches.FirstOrDefault(m => m.Id == matchId);
        if (match is null || !CanDragScheduleMatch(match))
            return;

        draggedScheduleMatchId = matchId;
        draggedBoardId = null;
        dropTargetMatchId = null;
        dropTargetScheduleMatchId = null;
        dropTargetScheduleBoardId = null;
        activeScheduleInsertTargetKey = null;
    }

    private void EndScheduleMatchDrag()
    {
        draggedScheduleMatchId = null;
        dropTargetScheduleMatchId = null;
        dropTargetScheduleBoardId = null;
        activeScheduleInsertTargetKey = null;
    }

    private bool CanDropScheduleOnMatch(MatchDto target)
    {
        if (draggedBoardId.HasValue)
            return CanAssignBoardToMatch(target);

        if (draggedScheduleMatchId is null)
            return false;

        var dragged = matches.FirstOrDefault(m => m.Id == draggedScheduleMatchId.Value);
        if (dragged is null || !CanDragScheduleMatch(dragged))
            return false;

        if (dragged.Id == target.Id)
            return false;

        return !IsScheduleDragLocked(target);
    }

    private bool CanDropScheduleOnBoard(Guid boardId)
    {
        if (draggedScheduleMatchId is null)
            return false;

        var dragged = matches.FirstOrDefault(m => m.Id == draggedScheduleMatchId.Value);
        if (dragged is null || !CanDragScheduleMatch(dragged))
            return false;

        if (!boards.Any(b => b.Id == boardId))
            return false;

        return HasValidScheduleInsertMarkerForBoard(boardId);
    }

    private void MarkScheduleDropTargetMatch(Guid matchId)
    {
        var target = matches.FirstOrDefault(m => m.Id == matchId);
        if (target is null)
            return;

        if (!CanDropScheduleOnMatch(target))
            return;

        dropTargetScheduleMatchId = matchId;
        dropTargetScheduleBoardId = null;
    }

    private void MarkScheduleDropTargetBoard(Guid boardId)
    {
        if (!CanDropScheduleOnBoard(boardId))
            return;

        dropTargetScheduleBoardId = boardId;
        dropTargetScheduleMatchId = null;
    }

    private void ClearScheduleDropTargetMatch(Guid matchId)
    {
        if (dropTargetScheduleMatchId == matchId)
            dropTargetScheduleMatchId = null;
    }

    private void ClearScheduleDropTargetBoard(Guid boardId)
    {
        if (dropTargetScheduleBoardId == boardId)
            dropTargetScheduleBoardId = null;
    }

    private bool HasValidScheduleInsertMarkerForBoard(Guid boardId)
    {
        var queue = BoardQueues.GetValueOrDefault(boardId) ?? [];
        if (queue.Count == 0)
            return CanDropScheduleAtMarker(null, null, boardId);

        if (CanDropScheduleAtMarker(null, queue[0].Id, boardId))
            return true;

        for (var index = 0; index < queue.Count - 1; index++)
        {
            if (CanDropScheduleAtMarker(queue[index].Id, queue[index + 1].Id, boardId))
                return true;
        }

        return CanDropScheduleAtMarker(queue[^1].Id, null, boardId);
    }

    private bool CanDropScheduleAtMarker(Guid? previousMatchId, Guid? nextMatchId, Guid? boardId)
    {
        if (draggedScheduleMatchId is null)
            return false;

        var dragged = FindScheduledMatch(draggedScheduleMatchId);
        if (dragged is null || !CanDragScheduleMatch(dragged))
            return false;

        var previous = FindScheduledMatch(previousMatchId);
        var next = FindScheduledMatch(nextMatchId);

        if (previous?.Id == dragged.Id || next?.Id == dragged.Id)
            return false;

        var effectiveBoardId = boardId ?? dragged.BoardId;
        if (dragged.IsBoardLocked && effectiveBoardId != dragged.BoardId)
            return false;

        if (previous is not null && !CanScheduleMatchesBeAdjacent(previous, dragged))
            return false;

        if (next is not null && !CanScheduleMatchesBeAdjacent(dragged, next))
            return false;

        return true;
    }

    private static bool CanScheduleMatchesBeAdjacent(MatchDto left, MatchDto right)
    {
        if (!string.Equals(left.Phase, right.Phase, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.Equals(left.Phase, "Group", StringComparison.OrdinalIgnoreCase))
            return true;

        return left.Round == right.Round;
    }

    private MatchDto? FindScheduledMatch(Guid? matchId)
        => matchId.HasValue ? matches.FirstOrDefault(m => m.Id == matchId.Value) : null;

    private string BuildScheduleInsertMarkerCss(Guid? previousMatchId, Guid? nextMatchId, Guid? boardId)
    {
        var request = new ScheduleDropRequest(previousMatchId, nextMatchId, boardId);
        var classes = new List<string> { "schedule-insert-marker" };

        if (CanDropScheduleAtMarker(previousMatchId, nextMatchId, boardId))
            classes.Add("is-valid");
        else
            classes.Add("is-invalid");

        if (activeScheduleInsertTargetKey == BuildScheduleInsertTargetKey(request))
            classes.Add("is-active");

        return string.Join(' ', classes);
    }

    private string BuildScheduleInsertMarkerLabel(Guid? nextMatchId)
    {
        var next = FindScheduledMatch(nextMatchId);
        if (next?.PlannedStartUtc is not null)
            return $"vor {next.PlannedStartUtc.Value.LocalDateTime:HH:mm}";

        return "danach";
    }

    private void MarkScheduleInsertTarget(ScheduleDropRequest request)
    {
        if (!CanDropScheduleAtMarker(request.PreviousMatchId, request.NextMatchId, request.BoardId))
            return;

        activeScheduleInsertTargetKey = BuildScheduleInsertTargetKey(request);
    }

    private void ClearScheduleInsertTarget(ScheduleDropRequest request)
    {
        var key = BuildScheduleInsertTargetKey(request);
        if (activeScheduleInsertTargetKey == key)
            activeScheduleInsertTargetKey = null;
    }

    private async Task DropOnScheduleMarkerAsync(ScheduleDropRequest request)
    {
        if (selectedTournament is null || draggedScheduleMatchId is null)
            return;

        var dragged = FindScheduledMatch(draggedScheduleMatchId);
        if (dragged is null || !CanDropScheduleAtMarker(request.PreviousMatchId, request.NextMatchId, request.BoardId))
            return;

        try
        {
            isWorking = true;

            var targetBoardId = request.BoardId ?? dragged.BoardId;
            var seedTime = CalculateScheduleInsertSeed(request.PreviousMatchId, request.NextMatchId);

            await Api.UpdateMatchScheduleAsync(dragged.Id, seedTime, dragged.IsStartTimeLocked, targetBoardId, dragged.IsBoardLocked);
            matches = (await Api.GenerateScheduleAsync(selectedTournament.Id)).ToList();
        }
        finally
        {
            isWorking = false;
            EndScheduleMatchDrag();
            dropTargetMatchId = null;
            dropTargetScheduleMatchId = null;
            dropTargetScheduleBoardId = null;
            activeScheduleInsertTargetKey = null;
        }
    }

    private DateTimeOffset CalculateScheduleInsertSeed(Guid? previousMatchId, Guid? nextMatchId)
    {
        var previous = FindScheduledMatch(previousMatchId);
        var next = FindScheduledMatch(nextMatchId);

        if (previous?.PlannedStartUtc is not null && next?.PlannedStartUtc is not null)
        {
            var previousUtc = previous.PlannedStartUtc.Value.ToUniversalTime();
            var nextUtc = next.PlannedStartUtc.Value.ToUniversalTime();
            if (nextUtc > previousUtc)
            {
                var midpointTicks = previousUtc.UtcTicks + ((nextUtc.UtcTicks - previousUtc.UtcTicks) / 2);
                if (midpointTicks > previousUtc.UtcTicks && midpointTicks < nextUtc.UtcTicks)
                    return new DateTimeOffset(midpointTicks, TimeSpan.Zero);
            }

            return nextUtc.AddSeconds(-1);
        }

        if (next?.PlannedStartUtc is not null)
            return next.PlannedStartUtc.Value.ToUniversalTime().AddMinutes(-1);

        if (previous?.PlannedStartUtc is not null)
            return previous.PlannedStartUtc.Value.ToUniversalTime().AddMinutes(1);

        if (selectedTournament is not null && TimeOnly.TryParse(selectedTournament.StartTime, out var tournamentStartTime))
        {
            var startDateTime = selectedTournament.StartDate.ToDateTime(tournamentStartTime);
            var localOffset = TimeZoneInfo.Local.GetUtcOffset(startDateTime);
            return new DateTimeOffset(startDateTime, localOffset).ToUniversalTime();
        }

        return DateTimeOffset.UtcNow;
    }

    private static string BuildScheduleInsertTargetKey(ScheduleDropRequest request)
        => $"{request.PreviousMatchId?.ToString() ?? "null"}|{request.NextMatchId?.ToString() ?? "null"}|{request.BoardId?.ToString() ?? "null"}";

    private string ScheduleMatchDragCss(MatchDto match)
    {
        if (draggedScheduleMatchId == match.Id)
            return "schedule-dnd-source";

        if (IsScheduleDragLocked(match))
            return "schedule-dnd-locked";

        return string.Empty;
    }

    private string ScheduleMatchDropZoneCss(MatchDto target)
    {
        if (!IsScheduleDragActive || !CanDropScheduleOnMatch(target))
            return string.Empty;

        if (dropTargetScheduleMatchId == target.Id)
            return "schedule-dnd-drop-target";

        return "schedule-dnd-drop-zone";
    }

    private string ScheduleBoardDropZoneCss(Guid boardId)
    {
        if (!IsScheduleDragActive || !CanDropScheduleOnBoard(boardId))
            return string.Empty;

        if (dropTargetScheduleBoardId == boardId)
            return "schedule-dnd-drop-target";

        return "schedule-dnd-drop-zone";
    }

    private string BuildScheduleBoardHeaderCss(Guid boardId)
    {
        var classes = "card-header py-1 d-flex justify-content-between align-items-center schedule-board-queue-header";
        var dropZone = ScheduleBoardDropZoneCss(boardId);
        if (!string.IsNullOrWhiteSpace(dropZone))
            classes += $" {dropZone}";
        return classes;
    }

    private string BuildScheduleMatchCardCss(MatchDto match, bool includeBoardDropTarget)
    {
        var classes = new List<string> { "schedule-dnd-card" };

        if (includeBoardDropTarget && dropTargetMatchId == match.Id)
            classes.Add("border border-primary rounded");

        var dragStateCss = ScheduleMatchDragCss(match);
        if (!string.IsNullOrWhiteSpace(dragStateCss))
            classes.Add(dragStateCss);

        var dropZoneCss = ScheduleMatchDropZoneCss(match);
        if (!string.IsNullOrWhiteSpace(dropZoneCss))
            classes.Add(dropZoneCss);

        return string.Join(' ', classes);
    }

    private bool ShowScheduleLockedMarker(MatchDto match)
        => CanManageScheduleInteractions && IsScheduleDragLocked(match);

    private async Task DropOnTimelineMatchAsync(Guid targetMatchId)
    {
        if (draggedBoardId is not null)
        {
            await AssignBoardToMatchAsync(targetMatchId);
            return;
        }

        if (selectedTournament is null || draggedScheduleMatchId is null)
            return;

        var dragged = matches.FirstOrDefault(m => m.Id == draggedScheduleMatchId.Value);
        var target = matches.FirstOrDefault(m => m.Id == targetMatchId);
        if (dragged is null || target is null || !CanDropScheduleOnMatch(target))
            return;

        try
        {
            isWorking = true;

            var targetStart = target.PlannedStartUtc ?? dragged.PlannedStartUtc;
            var lockTime = dragged.IsStartTimeLocked;
            var targetBoardId = target.BoardId;
            var lockBoard = dragged.IsBoardLocked;

            await Api.UpdateMatchScheduleAsync(dragged.Id, targetStart, lockTime, targetBoardId, lockBoard);
            matches = (await Api.GenerateScheduleAsync(selectedTournament.Id)).ToList();
        }
        finally
        {
            isWorking = false;
            EndScheduleMatchDrag();
            dropTargetMatchId = null;
            dropTargetScheduleMatchId = null;
        }
    }

    private async Task DropOnBoardHeaderAsync(Guid boardId)
    {
        if (selectedTournament is null || draggedScheduleMatchId is null)
            return;

        var dragged = matches.FirstOrDefault(m => m.Id == draggedScheduleMatchId.Value);
        if (dragged is null || !CanDropScheduleOnBoard(boardId))
            return;

        try
        {
            isWorking = true;

            // Match -> Board: keep current planned start time and only change board assignment.
            // Re-timing is done explicitly via "Spielplan generieren".
            var anchorStart = dragged.PlannedStartUtc;
            var lockTime = dragged.IsStartTimeLocked;
            var lockBoard = dragged.IsBoardLocked;

            await Api.UpdateMatchScheduleAsync(dragged.Id, anchorStart, lockTime, boardId, lockBoard);
            await LoadMatchesAsync(selectedTournament.Id);
        }
        finally
        {
            isWorking = false;
            EndScheduleMatchDrag();
            dropTargetScheduleBoardId = null;
            activeScheduleInsertTargetKey = null;
        }
    }

    // ─── Board Assignment (Drag & Drop) ───
    private async Task AssignBoardToMatchAsync(Guid matchId)
    {
        if (draggedBoardId is null) return;

        var target = matches.FirstOrDefault(m => m.Id == matchId);
        if (target is null || !CanAssignBoardToMatch(target))
        {
            draggedBoardId = null;
            dropTargetMatchId = null;
            return;
        }

        var boardId = draggedBoardId.Value;
        draggedBoardId = null;
        dropTargetMatchId = null;
        await Api.AssignBoardToMatchAsync(matchId, boardId);
        if (selectedTournament is not null)
        {
            // Recalculate schedule when board assignment changes
            if (!string.IsNullOrEmpty(selectedTournament.StartTime))
                matches = (await Api.GenerateScheduleAsync(selectedTournament.Id)).ToList();
            else
                await LoadMatchesAsync(selectedTournament.Id);
        }
    }

    // ─── Rounds ───
    private async Task SwitchToRoundsTabAsync()
    {
        await SwitchTabAsync("rounds");
    }

    private async Task LoadRoundsAsync()
    {
        if (selectedTournament is null) return;
        roundSettings = (await Api.GetRoundsAsync(selectedTournament.Id)).ToList();
    }

    private async Task SaveRoundAsync()
    {
        if (selectedTournament is null) return;
        if (!EnsureTournamentStructureEditable(message => roundError = message)) return;
        try
        {
            isWorking = true;
            roundError = null;
            ParseBoardAssignment(out var assignment, out var fixedId);
            foreach (var target in ResolveRoundSaveTargets())
            {
                await Api.SaveRoundAsync(selectedTournament.Id, new SaveTournamentRoundRequest(
                    selectedTournament.Id, target.Phase, target.RoundNumber,
                    newRoundBaseScore, newRoundInMode, newRoundOutMode, newRoundGameMode, newRoundLegs, newRoundSets, newRoundMaxRounds, newRoundBullMode, newRoundBullOffMode,
                    newRoundDuration, newRoundPause, newRoundPlayerPause, assignment, fixedId));
            }
            await LoadRoundsAsync();
        }
        catch (Exception ex) { roundError = ex.Message; }
        finally { isWorking = false; }
    }

    private async Task ApplyRoundToAllAsync()
    {
        if (selectedTournament is null) return;
        if (!EnsureTournamentStructureEditable(message => roundError = message)) return;
        try
        {
            isWorking = true;
            roundError = null;
            ParseBoardAssignment(out var assignment, out var fixedId);
            var phaseRounds = matches.Where(m => m.Phase == newRoundPhase).Select(m => m.Round).Distinct().OrderBy(r => r).ToList();
            if (phaseRounds.Count == 0)
                phaseRounds = Enumerable.Range(1, 8).ToList();
            foreach (var r in phaseRounds)
            {
                await Api.SaveRoundAsync(selectedTournament.Id, new SaveTournamentRoundRequest(
                    selectedTournament.Id, newRoundPhase, r,
                    newRoundBaseScore, newRoundInMode, newRoundOutMode, newRoundGameMode, newRoundLegs, newRoundSets, newRoundMaxRounds, newRoundBullMode, newRoundBullOffMode,
                    newRoundDuration, newRoundPause, newRoundPlayerPause, assignment, fixedId));
            }
            await LoadRoundsAsync();
        }
        catch (Exception ex) { roundError = ex.Message; }
        finally { isWorking = false; }
    }

    private async Task ApplyRoundToSubsequentAsync()
    {
        if (selectedTournament is null) return;
        if (!EnsureTournamentStructureEditable(message => roundError = message)) return;
        try
        {
            isWorking = true;
            roundError = null;
            ParseBoardAssignment(out var assignment, out var fixedId);
            var phaseRounds = matches.Where(m => m.Phase == newRoundPhase && m.Round >= newRoundNumber)
                .Select(m => m.Round).Distinct().OrderBy(r => r).ToList();
            if (phaseRounds.Count == 0)
                phaseRounds = Enumerable.Range(newRoundNumber, 8).ToList();
            foreach (var r in phaseRounds)
            {
                await Api.SaveRoundAsync(selectedTournament.Id, new SaveTournamentRoundRequest(
                    selectedTournament.Id, newRoundPhase, r,
                    newRoundBaseScore, newRoundInMode, newRoundOutMode, newRoundGameMode, newRoundLegs, newRoundSets, newRoundMaxRounds, newRoundBullMode, newRoundBullOffMode,
                    newRoundDuration, newRoundPause, newRoundPlayerPause, assignment, fixedId));
            }
            await LoadRoundsAsync();
        }
        catch (Exception ex) { roundError = ex.Message; }
        finally { isWorking = false; }
    }

    private void ParseBoardAssignment(out string assignment, out Guid? fixedId)
    {
        fixedId = null;
        assignment = newRoundBoardAssignment;
        if (assignment.StartsWith("Fixed:"))
        {
            fixedId = Guid.Parse(assignment[6..]);
            assignment = "Fixed";
        }
    }

    // ─── Match Detail / Result ───
    private void OpenMatchDetail(MatchDto match) => OpenMatchDetail(match, false);

    private void OpenMatchDetail(MatchDto match, bool openedFromSchedule)
    {
        detailMatch = match;
        detailMatchOpenedFromSchedule = openedFromSchedule;
        showWalkoverConfirm = false;
        walkoverWinnerId = string.Empty;
        editHomeLegs = match.HomeLegs;
        editAwayLegs = match.AwayLegs;
        editHomeSets = match.HomeSets;
        editAwaySets = match.AwaySets;
        resultError = null;
        detailMatchStatistics = [];
        // Fire-and-forget loads for statistics and follow state
        _ = LoadMatchStatisticsAsync(match.Id);
        _ = CheckFollowStateAsync(match.Id);
        _ = ReconcileTournamentMonitoringForViewAsync();
        _initDetailSections = true;
        _ = InvokeAsync(StateHasChanged);
    }

    private void CloseMatchDetail()
    {
        // Sync match back from current list so caller sees latest SignalR-updated state
        if (detailMatch is not null)
        {
            var fresh = matches.FirstOrDefault(m => m.Id == detailMatch.Id);
            if (fresh is not null) detailMatch = fresh;
        }
        detailMatch = null;
        detailMatchOpenedFromSchedule = false;
        showWalkoverConfirm = false;
        walkoverWinnerId = string.Empty;
        detailMatchStatistics = [];
        // C2: refresh matches from server after close so any changes during the modal session are reflected
        _ = BackgroundRefreshMatchesAsync();
    }

    private bool CanSetWalkover(MatchDto match)
    {
        return match.StartedUtc is null
            && match.FinishedUtc is null
            && !string.Equals(match.Status, "WalkOver", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(match.Status, "Beendet", StringComparison.OrdinalIgnoreCase);
    }

    private void OpenWalkoverDialog(MatchDto match)
    {
        if (!CanSetWalkover(match))
            return;

        showWalkoverConfirm = true;
        walkoverWinnerId = GetDefaultWalkoverWinnerId(match);
        resultError = null;
    }

    private void CancelWalkoverDialog()
    {
        showWalkoverConfirm = false;
        walkoverWinnerId = string.Empty;
    }

    private static string GetDefaultWalkoverWinnerId(MatchDto match)
    {
        if (match.HomeParticipantId == Guid.Empty && match.AwayParticipantId != Guid.Empty)
            return match.AwayParticipantId.ToString();

        if (match.AwayParticipantId == Guid.Empty && match.HomeParticipantId != Guid.Empty)
            return match.HomeParticipantId.ToString();

        return string.Empty;
    }

    private async Task ApplyWalkoverAsync()
    {
        if (detailMatch is null || selectedTournament is null)
            return;

        try
        {
            isWorking = true;
            resultError = null;

            var winnerId = Guid.TryParse(walkoverWinnerId, out var parsedWinner)
                ? parsedWinner
                : (Guid?)null;

            if (!winnerId.HasValue)
                throw new InvalidOperationException("Bitte waehlen Sie einen Gewinner fuer den Walkover aus.");

            var updated = await Api.UpdateMatchAsync(new UpdateMatchRequest(
                detailMatch.Id,
                detailMatch.BoardId,
                detailMatch.HomeLegs,
                detailMatch.AwayLegs,
                detailMatch.HomeSets,
                detailMatch.AwaySets,
                "WalkOver",
                detailMatch.IsStartTimeLocked,
                detailMatch.IsBoardLocked,
                winnerId));

            if (updated is not null)
            {
                var index = matches.FindIndex(m => m.Id == updated.Id);
                if (index >= 0)
                    matches[index] = updated;

                detailMatch = updated;
            }

            if (selectedTournament.Mode == "GroupAndKnockout")
                groupStandings = (await Api.GetGroupStandingsAsync(selectedTournament.Id)).ToList();

            showWalkoverConfirm = false;
        }
        catch (Exception ex)
        {
            resultError = ex.Message;
        }
        finally
        {
            isWorking = false;
        }
    }

    private async Task SaveResultAsync()
    {
        if (detailMatch is null || selectedTournament is null) return;
        try
        {
            isWorking = true;
            resultError = null;
            await Api.ReportResultAsync(new ReportMatchResultRequest(detailMatch.Id, editHomeLegs, editAwayLegs, editHomeSets, editAwaySets));
            await LoadMatchesAsync(selectedTournament.Id);
            if (selectedTournament.Mode == "GroupAndKnockout")
                groupStandings = (await Api.GetGroupStandingsAsync(selectedTournament.Id)).ToList();
            detailMatch = matches.FirstOrDefault(m => m.Id == detailMatch.Id);
            if (detailMatch is not null)
            {
                editHomeLegs = detailMatch.HomeLegs;
                editAwayLegs = detailMatch.AwayLegs;
                editHomeSets = detailMatch.HomeSets;
                editAwaySets = detailMatch.AwaySets;
            }
        }
        catch (Exception ex) { resultError = ex.Message; }
        finally { isWorking = false; }
    }

    // ─── Send Upcoming Match to Board ───
    private bool CanSendUpcomingMatch(MatchDto match)
    {
        if (match.FinishedUtc is not null || !match.BoardId.HasValue) return false;
        // No running match on this board
        var hasRunning = matches.Any(m => m.BoardId == match.BoardId && m.StartedUtc is not null && m.FinishedUtc is null);
        if (hasRunning) return false;
        // Match must be the next unfinished match on this board
        var nextOnBoard = matches
            .Where(m => m.BoardId == match.BoardId && m.FinishedUtc is null && m.StartedUtc is null)
            .OrderBy(m => m.PlannedStartUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(m => m.MatchNumber)
            .FirstOrDefault();
        return nextOnBoard?.Id == match.Id;
    }

    private async Task SendUpcomingMatchAsync(MatchDto match)
    {
        if (!CanSendUpcomingMatch(match) || selectedTournament is null) return;
        try
        {
            isWorking = true;
            static string SanitizeMetadataValue(string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return string.Empty;
                var trimmed = value.Trim().Replace(";", string.Empty).Replace("]", string.Empty);
                return trimmed.Length > 40 ? trimmed[..40] : trimmed;
            }

            var home = ParticipantName(match.HomeParticipantId);
            var away = ParticipantName(match.AwayParticipantId);
            var matchCode = SanitizeMetadataValue(MatchLabel(match));
            var initiator = SanitizeMetadataValue(autodartsDisplayName);
            var metadata = string.IsNullOrWhiteSpace(matchCode)
                ? string.Empty
                : string.IsNullOrWhiteSpace(initiator)
                    ? $" [ds:code={matchCode}]"
                    : $" [ds:init={initiator};code={matchCode}]";
            var label = $"{home} vs {away}{metadata}";
            await Api.SendUpcomingMatchAsync(match.BoardId!.Value, match.Id, label);
            await LoadBoardsAsync();
        }
        finally { isWorking = false; }
    }

    private async Task SyncMatchFromExternalAsync(MatchDto match)
    {
        if (string.IsNullOrEmpty(match.ExternalMatchId) || selectedTournament is null) return;
        try
        {
            isSyncing = true;
            var synced = await Api.SyncMatchFromExternalAsync(match.Id);
            if (synced is not null)
            {
                detailMatch = synced;
                editHomeLegs = synced.HomeLegs;
                editAwayLegs = synced.AwayLegs;
                editHomeSets = synced.HomeSets;
                editAwaySets = synced.AwaySets;
                matches = (await Api.GetMatchesAsync(selectedTournament.Id)).ToList();
            }
        }
        catch (Exception ex)
        {
            resultError = $"Sync fehlgeschlagen: {ex.Message}";
        }
        finally { isSyncing = false; }
    }

    private async Task EnsureListenerAsync(MatchDto match)
    {
        try
        {
            await Api.EnsureMatchListenerAsync(match.Id);
            await LoadMatchListenersAsync();
        }
        catch (Exception ex)
        {
            resultError = $"Listener konnte nicht erstellt werden: {ex.Message}";
        }
    }

    private async Task ResetMatchAsync()
    {
        if (detailMatch is null || selectedTournament is null) return;
        isWorking = true;
        resultError = null;
        try
        {
            var result = await Api.ResetMatchAsync(detailMatch.Id);
            if (result is not null)
            {
                detailMatch = result;
                editHomeLegs = 0;
                editAwayLegs = 0;
                editHomeSets = 0;
                editAwaySets = 0;
                matches = (await Api.GetMatchesAsync(selectedTournament.Id)).ToList();
            }
        }
        catch (Exception ex)
        {
            resultError = $"Zurücksetzen fehlgeschlagen: {ex.Message}";
        }
        finally { isWorking = false; }
    }

    private async Task ResetMatchByIdAsync(Guid matchId)
    {
        if (selectedTournament is null) return;
        isWorking = true;
        resultError = null;
        try
        {
            await Api.ResetMatchAsync(matchId);
            matches = (await Api.GetMatchesAsync(selectedTournament.Id)).ToList();
            await LoadMatchListenersAsync();
        }
        catch (Exception ex)
        {
            resultError = $"Zurücksetzen fehlgeschlagen: {ex.Message}";
        }
        finally { isWorking = false; }
    }

    // ─── Label Helpers ───

    /// <summary>Converts group number 1→A, 2→B, etc.</summary>
    private static string GroupLabel(int groupNumber) => ((char)('A' + groupNumber - 1)).ToString();

    /// <summary>Match label: group phase → A1, A2, B1; KO → F, SF1, QF2, 8F3, 16F4, etc.</summary>
    private string MatchLabel(MatchDto m)
    {
        if (m.Phase == "Group")
        {
            var groupLetter = GroupLabel(m.GroupNumber ?? 1);
            var matchesInGroup = matches
                .Where(x => x.Phase == "Group" && x.GroupNumber == m.GroupNumber)
                .OrderBy(x => x.Round).ThenBy(x => x.MatchNumber)
                .ToList();
            var idx = matchesInGroup.IndexOf(m) + 1;
            return $"{groupLetter}{idx}";
        }
        return KoMatchLabel(m);
    }

    private string KoMatchLabel(MatchDto m)
    {
        var koMatches = matches.Where(x => x.Phase == "Knockout").ToList();
        var maxRound = koMatches.Select(x => x.Round).DefaultIfEmpty(1).Max();
        var prefix = KoRoundPrefix(m.Round, maxRound);
        var matchesInRound = koMatches.Where(x => x.Round == m.Round).OrderBy(x => x.MatchNumber).ToList();
        var idx = matchesInRound.IndexOf(m) + 1;
        if (prefix == "F") return "F";
        return $"{prefix}{idx}";
    }

    /// <summary>KO round prefix: F, SF, QF, 8F, 16F, etc.</summary>
    private static string KoRoundPrefix(int round, int maxRound)
    {
        var fromEnd = maxRound - round;
        return fromEnd switch
        {
            0 => "F",
            1 => "SF",
            2 => "QF",
            3 => "8F",
            4 => "16F",
            _ => $"R{round}"
        };
    }

    private static string KoRoundLabel(int round, int maxRound)
    {
        var fromEnd = maxRound - round;
        return fromEnd switch
        {
            0 => "Finale",
            1 => "Halbfinale",
            2 => "Viertelfinale",
            3 => "Achtelfinale",
            4 => "Sechzehntelfinale",
            _ => $"Runde {round}"
        };
    }

    private async Task SetGroupMatchesViewModeAsync(string mode)
    {
        if (!string.Equals(mode, "horizontal", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(mode, "vertical", StringComparison.OrdinalIgnoreCase))
            return;

        groupMatchesViewMode = string.Equals(mode, "horizontal", StringComparison.OrdinalIgnoreCase)
            ? "horizontal"
            : "vertical";

        try
        {
            await JS.InvokeVoidAsync("dartSuiteUi.localStorageSet", "ds-groups-match-layout", groupMatchesViewMode);
        }
        catch
        {
            // best-effort persistence only
        }
    }

    private string RoundDisplayLabel(TournamentRoundDto rnd)
    {
        if (rnd.Phase == "Knockout")
        {
            var koMaxRound = matches.Where(m => m.Phase == "Knockout").Select(m => m.Round).DefaultIfEmpty(1).Max();
            return KoRoundLabel(rnd.RoundNumber, koMaxRound);
        }
        return $"Gruppe Runde {rnd.RoundNumber}";
    }

    private bool HasPlayedMatches() => matches.Any(m =>
        !IsWalkOverMatch(m)
        &&
        m.FinishedUtc is not null
        && m.HomeParticipantId != Guid.Empty
        && m.AwayParticipantId != Guid.Empty);

    // ─── Tab Badge Helpers ───

    private (int total, int finished, int running) GetPhaseStats(string phase)
    {
        var phaseMatches = matches
            .Where(m => m.Phase == phase && m.HomeParticipantId != Guid.Empty && m.AwayParticipantId != Guid.Empty)
            .ToList();
        var total = phaseMatches.Count;
        var finished = phaseMatches.Count(m => m.FinishedUtc is not null);
        var running = phaseMatches.Count(m => m.StartedUtc is not null && m.FinishedUtc is null);
        return (total, finished, running);
    }

    private string PhaseBadgeCss(string phase)
    {
        var (total, finished, running) = GetPhaseStats(phase);
        if (total == 0) return "text-bg-secondary";
        if (finished == total) return "text-bg-success";
        if (running > 0) return "text-bg-warning";
        return "text-bg-secondary";
    }

    private string PhaseBadgeText(string phase)
    {
        var (total, finished, running) = GetPhaseStats(phase);
        if (total == 0) return "0";
        if (running > 0) return $"{finished}/{total} ▶{running}";
        return $"{finished}/{total}";
    }

    private string ScheduleBadgeCss
    {
        get
        {
            var all = matches
                .Where(m => m.HomeParticipantId != Guid.Empty && m.AwayParticipantId != Guid.Empty)
                .ToList();
            if (all.Count == 0) return "text-bg-secondary";
            var finished = all.Count(m => m.FinishedUtc is not null);
            if (finished == all.Count) return "text-bg-success";
            var running = all.Count(m => m.StartedUtc is not null && m.FinishedUtc is null);
            if (running > 0) return "text-bg-warning";
            return "text-bg-secondary";
        }
    }

    private string ScheduleBadgeText
    {
        get
        {
            var all = matches
                .Where(m => m.HomeParticipantId != Guid.Empty && m.AwayParticipantId != Guid.Empty)
                .ToList();
            if (all.Count == 0) return "0";
            var finished = all.Count(m => m.FinishedUtc is not null);
            var running = all.Count(m => m.StartedUtc is not null && m.FinishedUtc is null);
            if (running > 0) return $"{finished}/{all.Count} ▶{running}";
            return $"{finished}/{all.Count}";
        }
    }

    private static string BoardStatusBadge(string status) => status switch
    {
        "Running" => "bg-success",
        "Online" => "bg-info text-dark",
        "Starting" => "bg-warning text-dark",
        "Error" => "bg-danger",
        _ => "bg-secondary"
    };

    // ─── Discord Webhook Test ───
    private async Task TestDiscordWebhookAsync()
    {
        if (selectedTournament is null || string.IsNullOrEmpty(editDiscordWebhookUrl)) return;
        try
        {
            isWorking = true;
            discordTestResult = null;
            // Save first to persist the URL
            await ExecuteSaveTournamentAsync();
            await Api.TestDiscordWebhookAsync(selectedTournament.Id);
            discordTestResult = "✓ Webhook-Test erfolgreich gesendet!";
            discordTestSuccess = true;
        }
        catch (Exception ex)
        {
            discordTestResult = $"✗ Fehler: {ex.Message}";
            discordTestSuccess = false;
        }
        finally { isWorking = false; }
    }

    // ─── Match Statistics (#18) ───
    private async Task LoadMatchStatisticsAsync(Guid matchId)
    {
        try
        {
            isLoadingStats = true;
            detailMatchStatistics = (await Api.GetMatchStatisticsAsync(matchId)).ToList();
        }
        catch { detailMatchStatistics = []; }
        finally { isLoadingStats = false; }
    }

    private async Task SyncMatchStatisticsAsync()
    {
        if (detailMatch is null) return;
        try
        {
            isLoadingStats = true;
            detailMatchStatistics = (await Api.SyncMatchStatisticsAsync(detailMatch.Id)).ToList();
        }
        catch (Exception ex) { resultError = $"Statistik-Sync fehlgeschlagen: {ex.Message}"; }
        finally { isLoadingStats = false; }
    }

    // ─── Match Follow/Unfollow (#14) ───
    private async Task ToggleFollowMatchAsync()
    {
        if (detailMatch is null) return;
        await ToggleFollowMatchAsync(detailMatch);
    }

    private bool IsMatchFollowed(Guid matchId)
    {
        if (detailMatch?.Id == matchId)
            return isFollowingDetailMatch;

        return followedMatchStatesById.TryGetValue(matchId, out var isFollowing) && isFollowing;
    }

    private bool IsFollowOperationBusy(Guid matchId)
        => followOperationInProgressMatchIds.Contains(matchId);

    private async Task ToggleFollowMatchAsync(MatchDto match)
    {
        if (match.Id == Guid.Empty || string.IsNullOrEmpty(autodartsDisplayName))
            return;

        if (!followOperationInProgressMatchIds.Add(match.Id))
            return;

        try
        {
            var isFollowing = IsMatchFollowed(match.Id);
            if (isFollowing)
            {
                await Api.UnfollowMatchAsync(match.Id, autodartsDisplayName);
            }
            else
            {
                await Api.FollowMatchAsync(match.Id, autodartsDisplayName);
            }

            var newState = !isFollowing;
            followedMatchStatesById[match.Id] = newState;

            if (detailMatch?.Id == match.Id)
                isFollowingDetailMatch = newState;
        }
        catch
        {
            await CheckFollowStateAsync(match.Id);
        }
        finally
        {
            followOperationInProgressMatchIds.Remove(match.Id);
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task CheckFollowStateAsync(Guid matchId)
    {
        if (matchId == Guid.Empty)
            return;

        if (string.IsNullOrEmpty(autodartsDisplayName))
        {
            followedMatchStatesById[matchId] = false;
            if (detailMatch?.Id == matchId)
                isFollowingDetailMatch = false;
            return;
        }

        try
        {
            var followers = await Api.GetMatchFollowersAsync(matchId);
            var isFollowing = followers.Any(f =>
                string.Equals(f.UserAccountName, autodartsDisplayName, StringComparison.OrdinalIgnoreCase));
            followedMatchStatesById[matchId] = isFollowing;

            if (detailMatch?.Id == matchId)
                isFollowingDetailMatch = isFollowing;
        }
        catch
        {
            followedMatchStatesById[matchId] = false;
            if (detailMatch?.Id == matchId)
                isFollowingDetailMatch = false;
        }
    }

    /// <summary>Returns all matches chronologically sorted for Spielplan, with filters applied.</summary>
    private List<MatchDto> ScheduledMatches
    {
        get
        {
            var query = matches
                .Where(m => !string.Equals(m.Status, "WalkOver", StringComparison.OrdinalIgnoreCase));

            if (scheduleHideFinished)
                query = query.Where(m => m.FinishedUtc is null);

            if (scheduleShowNoBoard)
                query = query.Where(m => !m.BoardId.HasValue && m.FinishedUtc is null);

            if (scheduleStatusFilter == "running")
                query = query.Where(m => m.StartedUtc is not null && m.FinishedUtc is null);
            else if (scheduleStatusFilter == "upcoming")
                query = query.Where(m => m.StartedUtc is null && m.FinishedUtc is null);

            return query
                .OrderBy(m => m.PlannedStartUtc ?? DateTimeOffset.MaxValue)
                .ThenBy(m => m.Phase == "Group" ? 0 : 1)
                .ThenBy(m => m.Round)
                .ThenBy(m => m.MatchNumber)
                .ToList();
        }
    }

    /// <summary>Matches without board assignment that still need to be played.</summary>
    private List<MatchDto> UnassignedUpcoming => ScheduledMatches
        .Where(m => m.FinishedUtc is null && m.BoardId is null)
        .ToList();

    /// <summary>Board queue: upcoming matches grouped by board.</summary>
    private Dictionary<Guid, List<MatchDto>> BoardQueues
    {
        get
        {
            var result = new Dictionary<Guid, List<MatchDto>>();
            foreach (var board in boards)
                result[board.Id] = ScheduledMatches
                    .Where(m => m.BoardId == board.Id && m.FinishedUtc is null)
                    .ToList();
            return result;
        }
    }

    private string ParticipantName(Guid? id)
    {
        if (!id.HasValue)
            return "?";

        var participant = participants.FirstOrDefault(p => p.Id == id.Value);
        if (participant is null)
            return "?";

        if (IsTeamplayActive && participant.TeamId.HasValue)
        {
            var teamName = teams.FirstOrDefault(t => t.Id == participant.TeamId.Value)?.Name;
            if (!string.IsNullOrWhiteSpace(teamName))
                return teamName.ToUpperInvariant();
        }

        return participant.DisplayName.ToUpperInvariant();
    }

    private string ScheduleHomeName(MatchDto match)
        => ResolveScheduleParticipantName(match, isHome: true);

    private string ScheduleAwayName(MatchDto match)
        => ResolveScheduleParticipantName(match, isHome: false);

    private string ResolveScheduleParticipantName(MatchDto match, bool isHome)
    {
        var participantId = isHome ? match.HomeParticipantId : match.AwayParticipantId;
        if (participantId != Guid.Empty)
            return ParticipantName(participantId);

        if (string.Equals(match.Phase, "Knockout", StringComparison.OrdinalIgnoreCase))
            return KoParticipantLabel(Guid.Empty, match, isHome);

        var slotOrigin = isHome ? match.HomeSlotOrigin : match.AwaySlotOrigin;
        if (!string.IsNullOrWhiteSpace(slotOrigin))
            return slotOrigin;

        return "?";
    }

    private string ParticipantNameWithSeed(Guid? id)
    {
        if (!id.HasValue) return "?";
        var participant = participants.FirstOrDefault(p => p.Id == id.Value);
        return participant is null ? "?" : ParticipantDisplayName(participant);
    }

    private bool IsSetMode(MatchDto match) =>
        roundSettings.FirstOrDefault(r => r.Phase == match.Phase && r.RoundNumber == match.Round)?.GameMode == "Sets";

    private string FormatScore(MatchDto match)
    {
        if (IsSetMode(match))
            return $"{match.HomeSets}:{match.AwaySets} ({match.HomeLegs}:{match.AwayLegs})";
        return $"{match.HomeLegs}:{match.AwayLegs}";
    }

    private MatchPlayerStatisticDto? DetailHomeStatistic
        => ResolveMatchStatistic(detailMatch, detailMatch?.HomeParticipantId ?? Guid.Empty);

    private MatchPlayerStatisticDto? DetailAwayStatistic
        => ResolveMatchStatistic(detailMatch, detailMatch?.AwayParticipantId ?? Guid.Empty);

    private MatchPlayerStatisticDto? ResolveMatchStatistic(MatchDto? match, Guid participantId)
    {
        if (match is null || participantId == Guid.Empty)
            return null;

        return detailMatchStatistics
            .Where(x => x.MatchId == match.Id && x.ParticipantId == participantId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefault();
    }

    private static string? BuildSlotOrigin(MatchDto match)
    {
        var originParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(match.HomeSlotOrigin))
            originParts.Add($"Home: {match.HomeSlotOrigin}");

        if (!string.IsNullOrWhiteSpace(match.AwaySlotOrigin))
            originParts.Add($"Away: {match.AwaySlotOrigin}");

        return originParts.Count == 0 ? null : string.Join(" | ", originParts);
    }

    private string? FormatGameplay(MatchDto match)
    {
        var round = roundSettings.FirstOrDefault(r => r.Phase == match.Phase && r.RoundNumber == match.Round);
        if (round is null) return null;

        var inAbbr = round.InMode switch { "Double" => "D", "Master" => "M", _ => "S" };
        var outAbbr = round.OutMode switch { "Double" => "D", "Master" => "M", _ => "S" };

        var modeLabel = round.GameMode == "Sets"
            ? $"First to {round.Sets ?? 3}S ({round.Legs}L)"
            : $"First to {round.Legs}L";

        return $"{round.BaseScore} {modeLabel} {inAbbr}I-{outAbbr}O {round.MaxRounds}R {round.BullMode} BO {round.BullOffMode.ToLowerInvariant()}";
    }

    /// <summary>Returns participant name or a placeholder for unresolved KO slots.</summary>
    private string KoParticipantLabel(Guid participantId, MatchDto match, bool isHome)
    {
        if (participantId != Guid.Empty)
            return ParticipantName(participantId);

        // For round 1 KO with group phase: show group rank placeholder
        if (selectedTournament?.Mode == "GroupAndKnockout" && match.Round == 1)
        {
            var koR1 = matches.Where(m => m.Phase == "Knockout" && m.Round == 1)
                .OrderBy(m => m.MatchNumber).ToList();
            var idx = koR1.IndexOf(match);
            if (idx >= 0)
            {
                var groupCount = matches.Where(m => m.Phase == "Group")
                    .Select(m => m.GroupNumber ?? 0).Distinct().Count();
                var advancers = selectedTournament.PlayoffAdvancers;
                if (groupCount > 0 && advancers > 0)
                {
                    var slot = isHome ? idx * 2 : idx * 2 + 1;
                    var rank = slot / groupCount + 1;
                    var groupNum = slot % groupCount + 1;
                    if (rank <= advancers)
                        return $"{rank}. Gruppe {GroupLabel(groupNum)}";
                }
            }
        }

        // For later KO rounds: show "Gewinner" of feeder match
        if (match.Round > 1)
        {
            var koMatches = matches.Where(m => m.Phase == "Knockout").ToList();
            var maxRound = koMatches.Select(m => m.Round).DefaultIfEmpty(1).Max();
            var prevRound = koMatches.Where(m => m.Round == match.Round - 1)
                .OrderBy(m => m.MatchNumber).ToList();
            var thisRound = koMatches.Where(m => m.Round == match.Round)
                .OrderBy(m => m.MatchNumber).ToList();
            var matchIdx = thisRound.IndexOf(match);
            if (matchIdx >= 0)
            {
                var feederIdx = isHome ? matchIdx * 2 : matchIdx * 2 + 1;
                if (feederIdx < prevRound.Count)
                {
                    var feeder = prevRound[feederIdx];
                    return $"Gewinner {KoMatchLabel(feeder)}";
                }
            }
        }

        return "?";
    }

    // ─── Board Detail ───
    private void OpenBoardDetail(BoardDto board)
    {
        detailBoard = board;
        boardSyncInfo = null;
        boardSyncError = null;
        boardSyncDebug = null;

        var currentMatch = CurrentMatchOnBoard(board.Id);
        if (currentMatch is not null)
            _ = CheckFollowStateAsync(currentMatch.Id);

        if (IsDevelopmentEnvironment)
            _ = LoadBoardSyncDebugAsync(board.Id);

        _ = ReconcileBoardMonitoringForViewAsync(board.Id);
        _ = InvokeAsync(StateHasChanged);
    }

    private async Task RequestBoardSyncAsync(BoardDto board)
    {
        try
        {
            isWorking = true;
            boardSyncError = null;
            boardSyncInfo = null;
            var accepted = await Api.RequestBoardExtensionSyncAsync(board.Id);
            boardSyncInfo = $"Sync angefordert (RequestId: {accepted.RequestId}). Die Rueckmeldung kann je nach Extension-Polling bis zu ca. 45 Sekunden dauern.";

            if (IsDevelopmentEnvironment)
            {
                var reported = await PollBoardSyncDebugAsync(board.Id, accepted.RequestId);
                if (!reported)
                {
                    boardSyncError = "Noch kein Sync-Report von der Extension empfangen. Bitte pruefe Extension-Tab/API-URL und aktualisiere den Debug-Block manuell.";
                }
            }
        }
        catch (Exception ex)
        {
            boardSyncError = ex.Message;
        }
        finally
        {
            isWorking = false;
        }
    }

    private void CloseBoardDetail()
    {
        detailBoard = null;
        boardSyncInfo = null;
        boardSyncError = null;
        boardSyncDebug = null;
        // Refresh detailMatch from in-memory list in case board interaction updated it
        if (detailMatch is not null)
        {
            var freshMatch = matches.FirstOrDefault(m => m.Id == detailMatch.Id);
            if (freshMatch is not null) detailMatch = freshMatch;
        }
        // C2: refresh matches from server after close so any board-side changes are reflected
        _ = BackgroundRefreshMatchesAsync();
        // Refresh detailBoard pointer when navigating back to board from a sub-detail
        if (_modalBackStack.Count > 0)
            _modalBackStack.Pop().Invoke();
    }

    private async Task LoadBoardSyncDebugAsync(Guid boardId)
    {
        try
        {
            isBoardSyncDebugLoading = true;
            boardSyncDebug = await Api.GetLastBoardExtensionSyncDebugAsync(boardId);
        }
        catch
        {
            // Debug panel is optional and must not break board details.
        }
        finally
        {
            isBoardSyncDebugLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task BackgroundRefreshMatchesAsync()
    {
        if (selectedTournament is null) return;
        try
        {
            matches = (await Api.GetMatchesAsync(selectedTournament.Id)).ToList();
            await InvokeAsync(StateHasChanged);
        }
        catch { /* best-effort; polling / SignalR will recover */ }
    }

    private async Task<bool> PollBoardSyncDebugAsync(Guid boardId, Guid requestId)
    {
        for (var i = 0; i < 45; i++)
        {
            await LoadBoardSyncDebugAsync(boardId);

            if (boardSyncDebug is not null
                && boardSyncDebug.RequestId == requestId
                && boardSyncDebug.ReportedAtUtc.HasValue)
                return true;

            await Task.Delay(1000);
        }

        return false;
    }

    private async Task RefreshBoardSyncDebugAsync()
    {
        if (detailBoard is null) return;
        await LoadBoardSyncDebugAsync(detailBoard.Id);
    }

    // ─── Player Detail ───
    private void OpenPlayerDetail(Guid? participantId)
    {
        if (!participantId.HasValue) return;
        detailParticipant = participants.FirstOrDefault(p => p.Id == participantId.Value);
        playerDetailTab = "info";
        _ = InvokeAsync(StateHasChanged);
    }

    private MatchDto? CurrentMatchOnBoard(Guid boardId) =>
        matches.FirstOrDefault(m => m.BoardId == boardId && m.StartedUtc is not null && m.FinishedUtc is null);

    private List<MatchDto> UpcomingMatchesOnBoard(Guid boardId) =>
        ScheduledMatches.Where(m => m.BoardId == boardId && m.FinishedUtc is null && m.StartedUtc is null)
            .ToList();

    // ─── Match Time Editing ───
    private void StartEditMatchTime(MatchDto match)
    {
        editingMatchTimeId = match.Id;
        editMatchTimeValue = match.PlannedStartUtc?.LocalDateTime.ToString("HH:mm") ?? "";
    }

    private void CancelEditMatchTime() => editingMatchTimeId = null;

    private async Task SaveMatchTimeAsync(MatchDto match)
    {
        if (selectedTournament is null) return;
        try
        {
            isWorking = true;
            DateTimeOffset? newTime = null;
            if (TimeOnly.TryParse(editMatchTimeValue, out var t))
            {
                var dt = selectedTournament.StartDate.ToDateTime(t);
                newTime = new DateTimeOffset(dt, TimeZoneInfo.Local.GetUtcOffset(dt));
            }
            await Api.UpdateMatchScheduleAsync(match.Id, newTime, true, match.BoardId, match.IsBoardLocked);
            // Recalculate schedule for subsequent matches
            if (!string.IsNullOrEmpty(selectedTournament.StartTime))
                matches = (await Api.GenerateScheduleAsync(selectedTournament.Id)).ToList();
            else
                await LoadMatchesAsync(selectedTournament.Id);
            editingMatchTimeId = null;
        }
        finally { isWorking = false; }
    }

    // ─── Lock Toggling ───
    private async Task ToggleTimeLockAsync(MatchDto match)
    {
        if (selectedTournament is null) return;
        try
        {
            isWorking = true;
            await Api.ToggleMatchTimeLockAsync(match.Id, !match.IsStartTimeLocked);
            await LoadMatchesAsync(selectedTournament.Id);
        }
        finally { isWorking = false; }
    }

    private async Task ToggleBoardLockAsync(MatchDto match)
    {
        if (selectedTournament is null) return;
        try
        {
            isWorking = true;
            await Api.ToggleMatchBoardLockAsync(match.Id, !match.IsBoardLocked);
            await LoadMatchesAsync(selectedTournament.Id);
        }
        finally { isWorking = false; }
    }

    // ─── Game Mode Lock ───
    private async Task ToggleGameModesLockAsync()
    {
        if (selectedTournament is null) return;
        editAreGameModesLocked = !editAreGameModesLocked;
        await ExecuteSaveTournamentAsync();
    }

    // ─── Missing Config Detection ───
    private bool HasMissingRoundConfig()
    {
        if (selectedTournament is null) return false;
        var koMatches = matches.Where(m => m.Phase == "Knockout").ToList();
        var koRounds = koMatches.Select(m => m.Round).Distinct();
        foreach (var r in koRounds)
        {
            if (!roundSettings.Any(rs => rs.Phase == "Knockout" && rs.RoundNumber == r))
                return true;
        }
        if (selectedTournament.Mode == "GroupAndKnockout")
        {
            var groupRounds = matches.Where(m => m.Phase == "Group").Select(m => m.Round).Distinct();
            foreach (var r in groupRounds)
            {
                if (!roundSettings.Any(rs => rs.Phase == "Group" && rs.RoundNumber == r))
                    return true;
            }
        }
        return false;
    }

    private int MissingRoundConfigCount()
    {
        if (selectedTournament is null) return 0;
        var count = 0;
        var koRounds = matches.Where(m => m.Phase == "Knockout").Select(m => m.Round).Distinct();
        foreach (var r in koRounds)
        {
            if (!roundSettings.Any(rs => rs.Phase == "Knockout" && rs.RoundNumber == r)) count++;
        }
        if (selectedTournament.Mode == "GroupAndKnockout")
        {
            var groupRounds = matches.Where(m => m.Phase == "Group").Select(m => m.Round).Distinct();
            foreach (var r in groupRounds)
            {
                if (!roundSettings.Any(rs => rs.Phase == "Group" && rs.RoundNumber == r)) count++;
            }
        }
        return count;
    }

    // ─── Round Detail Modal ───
    private void OpenRoundDetail(IReadOnlyList<TournamentRoundDto> rounds)
    {
        if (rounds.Count == 0)
            return;

        detailRoundGroup = [.. rounds.OrderBy(r => r.Phase).ThenBy(r => r.RoundNumber)];
        var round = detailRoundGroup[0];
        detailRound = round;
        newRoundPhase = round.Phase;
        newRoundNumber = round.RoundNumber;
        newRoundBaseScore = round.BaseScore;
        newRoundLegs = round.Legs;
        newRoundOutMode = round.OutMode;
        newRoundInMode = round.InMode;
        newRoundGameMode = round.GameMode;
        newRoundSets = round.Sets;
        newRoundMaxRounds = round.MaxRounds;
        newRoundBullMode = round.BullMode;
        newRoundBullOffMode = round.BullOffMode;
        newRoundDuration = round.LegDurationSeconds;
        newRoundPause = round.PauseBetweenMatchesMinutes;
        newRoundPlayerPause = round.MinPlayerPauseMinutes;
        newRoundBoardAssignment = round.BoardAssignment == "Fixed" && round.FixedBoardId is not null
            ? $"Fixed:{round.FixedBoardId}" : round.BoardAssignment;
        _ = InvokeAsync(StateHasChanged);
    }

    private void CloseRoundDetail()
    {
        detailRound = null;
        detailRoundGroup = [];
    }

    private IReadOnlyList<RoundSaveTarget> ResolveRoundSaveTargets()
    {
        if (detailRoundGroup.Count > 1)
        {
            return detailRoundGroup
                .Select(r => new RoundSaveTarget(r.Phase, r.RoundNumber))
                .OrderBy(r => r.Phase)
                .ThenBy(r => r.RoundNumber)
                .ToList();
        }

        return [new RoundSaveTarget(newRoundPhase, newRoundNumber)];
    }

    private async Task DeleteRoundAsync()
    {
        if (detailRound is null || selectedTournament is null) return;
        if (!EnsureTournamentStructureEditable(message => roundError = message)) return;
        try
        {
            isWorking = true;
            roundError = null;
            var success = await Api.DeleteRoundAsync(selectedTournament.Id, detailRound.Phase, detailRound.RoundNumber);
            if (success)
            {
                detailRound = null;
                await LoadRoundsAsync();
            }
            else
            {
                roundError = "Spielrunde konnte nicht gelöscht werden.";
            }
        }
        catch (Exception ex) { roundError = ex.Message; }
        finally { isWorking = false; }
    }

    private async Task UpdateTournamentStatusAsync(string status)
    {
        if (selectedTournament is null) return;

        // Downgrade to Erstellt deletes all rounds & matches — confirm first
        if (status == "Erstellt" && selectedTournament.Status is not "Erstellt" && (matches.Any() || roundSettings.Any()))
        {
            confirmationMessage = "⚠ Beim Zurücksetzen auf \"Erstellt\" werden alle Runden und Matches gelöscht. Wirklich fortfahren?";
            confirmationAction = () => ExecuteUpdateStatusAsync(status);
            showConfirmation = true;
            return;
        }
        // Downgrade to Geplant resets match results
        if (status == "Geplant" && selectedTournament.Status is "Gestartet" or "Beendet" or "Abgebrochen" && HasPlayedMatches())
        {
            confirmationMessage = "⚠ Beim Zurücksetzen auf \"Geplant\" werden alle Ergebnisse zurückgesetzt. Wirklich fortfahren?";
            confirmationAction = () => ExecuteUpdateStatusAsync(status);
            showConfirmation = true;
            return;
        }
        await ExecuteUpdateStatusAsync(status);
    }

    private sealed record RoundSaveTarget(string Phase, int RoundNumber);

    private async Task ExecuteUpdateStatusAsync(string status)
    {
        if (selectedTournament is null) return;
        try
        {
            isWorking = true;
            var updated = await Api.UpdateTournamentStatusAsync(selectedTournament.Id, status);
            if (updated is not null)
            {
                // Collapse all settings panels when transitioning to "Geplant" or higher
                if (!string.Equals(status, "Erstellt", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        await JS.InvokeVoidAsync("dartSuiteUi.collapseAllTournamentSettingsPanels",
                            selectedTournament.Id.ToString("N"));
                    }
                    catch { /* best-effort */ }
                }

                await LoadTournamentsAsync();
                selectedTournament = tournaments.FirstOrDefault(x => x.Id == updated.Id) ?? updated;
                PopulateEditFields(selectedTournament);
                matches = (await Api.GetMatchesAsync(selectedTournament.Id)).ToList();
                await LoadRoundsAsync();
            }
        }
        catch (Exception ex) { editError = ex.Message; }
        finally { isWorking = false; }
    }

    // ─── Teamplay: Team formation (Issue #11) ───
    private bool IsTeamFull(int teamIndex)
        => teamIndex >= 0 && teamIndex < teamDrafts.Count
            && teamDrafts[teamIndex].MemberParticipantIds.Count >= editPlayersPerTeam;

    private void StartTeamParticipantDrag(Guid participantId)
    {
        if (!CanEditTournamentStructure)
            return;

        draggedTeamParticipantId = participantId;
        selectedTeamParticipantId = participantId;
        teamDraftError = null;
    }

    private void SelectTeamParticipant(Guid participantId)
    {
        if (!CanEditTournamentStructure)
            return;

        if (selectedTeamParticipantId == participantId)
        {
            selectedTeamParticipantId = null;
            draggedTeamParticipantId = null;
            dropTargetTeamIndex = null;
            return;
        }

        selectedTeamParticipantId = participantId;
        draggedTeamParticipantId = null;
        teamDraftError = null;
    }

    private bool IsSelectedTeamParticipant(Guid participantId)
        => selectedTeamParticipantId == participantId;

    private string? SelectedTeamParticipantName
        => selectedTeamParticipantId.HasValue
            ? participants.FirstOrDefault(p => p.Id == selectedTeamParticipantId.Value)?.DisplayName
            : null;

    private async Task AssignSelectedParticipantToTeamAsync(int teamIndex)
    {
        if (selectedTeamParticipantId is null)
            return;

        await AssignParticipantToTeamAsync(teamIndex, selectedTeamParticipantId.Value);
    }

    private async Task AssignDraggedParticipantToTeamAsync(int teamIndex)
    {
        if (!CanEditTournamentStructure || draggedTeamParticipantId is null)
            return;

        await AssignParticipantToTeamAsync(teamIndex, draggedTeamParticipantId.Value);
    }

    private async Task AssignParticipantToTeamAsync(int teamIndex, Guid participantId)
    {
        if (!CanEditTournamentStructure)
            return;
        if (teamIndex < 0 || teamIndex >= teamDrafts.Count)
            return;

        dropTargetTeamIndex = null;

        var team = teamDrafts[teamIndex];
        if (team.MemberParticipantIds.Contains(participantId))
        {
            draggedTeamParticipantId = null;
            selectedTeamParticipantId = null;
            return;
        }

        if (team.MemberParticipantIds.Count >= editPlayersPerTeam)
        {
            teamDraftError = $"Team {teamIndex + 1} ist bereits voll ({editPlayersPerTeam}/{editPlayersPerTeam}).";
            return;
        }

        for (var i = 0; i < teamDrafts.Count; i++)
        {
            if (teamDrafts[i].MemberParticipantIds.Remove(participantId))
                RecomputeTeamDraftNameIfAuto(i);
        }

        team.MemberParticipantIds.Add(participantId);
        RecomputeTeamDraftNameIfAuto(teamIndex);
        draggedTeamParticipantId = null;
        selectedTeamParticipantId = null;
        hasUnsavedTeamDraftChanges = true;
        await AutoSaveTeamDraftIfCompleteAsync();
    }

    private async Task RemoveParticipantFromTeamAsync(Guid participantId)
    {
        if (!CanEditTournamentStructure)
            return;

        for (var i = 0; i < teamDrafts.Count; i++)
        {
            if (!teamDrafts[i].MemberParticipantIds.Remove(participantId))
                continue;

            RecomputeTeamDraftNameIfAuto(i);
            if (selectedTeamParticipantId == participantId)
                selectedTeamParticipantId = null;
            hasUnsavedTeamDraftChanges = true;
            await AutoSaveTeamDraftIfCompleteAsync();
            return;
        }
    }

    private async Task OnTeamDraftNameChanged(int teamIndex, ChangeEventArgs args)
    {
        if (!CanEditTournamentStructure)
            return;

        if (teamIndex < 0 || teamIndex >= teamDrafts.Count)
            return;

        var value = args.Value?.ToString()?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            teamDrafts[teamIndex].IsAutoName = true;
            RecomputeTeamDraftNameIfAuto(teamIndex);
        }
        else
        {
            teamDrafts[teamIndex].Name = value;
            teamDrafts[teamIndex].IsAutoName = false;
        }

        hasUnsavedTeamDraftChanges = true;
        await AutoSaveTeamDraftIfCompleteAsync();
    }

    private async Task ResetTeamDraftNameToAutoAsync(int teamIndex)
    {
        if (!CanEditTournamentStructure)
            return;

        if (teamIndex < 0 || teamIndex >= teamDrafts.Count)
            return;

        teamDrafts[teamIndex].IsAutoName = true;
        RecomputeTeamDraftNameIfAuto(teamIndex);
        hasUnsavedTeamDraftChanges = true;
        await AutoSaveTeamDraftIfCompleteAsync();
    }

    private void BeginTeamNameEdit(int teamIndex)
    {
        if (!CanEditTournamentStructure)
            return;

        if (teamIndex < 0 || teamIndex >= teamDrafts.Count)
            return;

        openTeamNameMenuIndex = null;
        editingTeamNameIndex = teamIndex;
        editingTeamNameValue = TeamDraftDisplayName(teamDrafts[teamIndex], teamIndex);
        pendingTeamNameFocusIndex = teamIndex;
    }

    private string TeamNameEditInputId(int teamIndex)
        => $"team-name-edit-{teamIndex}";

    private async Task HandleTeamNameEditKeyDownAsync(int teamIndex, KeyboardEventArgs args)
    {
        if (args.Key is "Enter" or "Tab")
            await CommitTeamNameEditAsync(teamIndex);
    }

    private void ToggleTeamNameMenu(int teamIndex)
    {
        if (teamIndex < 0 || teamIndex >= teamDrafts.Count)
            return;

        openTeamNameMenuIndex = openTeamNameMenuIndex == teamIndex ? null : teamIndex;
    }

    private void CloseTeamNameMenu()
        => openTeamNameMenuIndex = null;

    private async Task CommitTeamNameEditAsync(int teamIndex)
    {
        if (teamIndex < 0 || teamIndex >= teamDrafts.Count)
            return;

        if (editingTeamNameIndex != teamIndex)
            return;

        var value = editingTeamNameValue?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            teamDrafts[teamIndex].IsAutoName = true;
            RecomputeTeamDraftNameIfAuto(teamIndex);
        }
        else
        {
            teamDrafts[teamIndex].Name = value;
            teamDrafts[teamIndex].IsAutoName = false;
        }

        editingTeamNameIndex = null;
        editingTeamNameValue = string.Empty;
        openTeamNameMenuIndex = null;
        hasUnsavedTeamDraftChanges = true;
        await AutoSaveTeamDraftIfCompleteAsync();
    }

    private void CancelTeamNameEdit()
    {
        editingTeamNameIndex = null;
        editingTeamNameValue = string.Empty;
        openTeamNameMenuIndex = null;
    }

    [JSInvokable]
    public async Task HandleTeamDraftUiOutsideClickAsync()
    {
        var shouldRender = false;

        if (openTeamNameMenuIndex.HasValue)
        {
            openTeamNameMenuIndex = null;
            shouldRender = true;
        }

        if (editingTeamNameIndex.HasValue)
        {
            var index = editingTeamNameIndex.Value;
            if (index >= 0 && index < teamDrafts.Count)
                await CommitTeamNameEditAsync(index);
            else
                CancelTeamNameEdit();

            shouldRender = true;
        }

        if (showPageSettingsDropdown)
        {
            showPageSettingsDropdown = false;
            shouldRender = true;
        }

        if (showStatusDropdown)
        {
            showStatusDropdown = false;
            shouldRender = true;
        }

        if (shouldRender)
            await InvokeAsync(StateHasChanged);
    }

    private void StartTeamSeedDrag(int teamIndex)
    {
        if (!CanEditTournamentStructure || !CanSeedTeams)
            return;

        if (teamIndex < 0 || teamIndex >= teamDrafts.Count)
            return;

        openTeamNameMenuIndex = null;
        selectedTeamSeedIndex = null;
        teamSeedInsertTargetIndex = null;
        isTeamSeedDragging = true;
        draggedTeamSeedIndex = teamIndex;
    }

    private void EndTeamSeedDrag()
    {
        CancelTeamSeedLongPress();
        draggedTeamSeedIndex = null;
        selectedTeamSeedIndex = null;
        teamSeedInsertTargetIndex = null;
        isTeamSeedDragging = false;
    }

    private void MarkTeamSeedInsertTarget(int teamIndex)
    {
        if (!isTeamSeedDragging || !draggedTeamSeedIndex.HasValue)
            return;

        if (teamIndex < 0 || teamIndex >= teamDrafts.Count)
            return;

        teamSeedInsertTargetIndex = teamIndex;
    }

    private void ClearTeamSeedInsertTarget(int teamIndex)
    {
        if (teamSeedInsertTargetIndex == teamIndex)
            teamSeedInsertTargetIndex = null;
    }

    private async Task DropTeamSeedAtAsync(int targetTeamIndex)
    {
        if (selectedTournament is null || !CanEditTournamentStructure || !CanSeedTeams)
            return;

        if (!draggedTeamSeedIndex.HasValue)
            return;

        var sourceTeamIndex = draggedTeamSeedIndex.Value;
        EndTeamSeedDrag();

        if (sourceTeamIndex == targetTeamIndex)
            return;

        if (sourceTeamIndex < 0 || sourceTeamIndex >= teamDrafts.Count || targetTeamIndex < 0 || targetTeamIndex >= teamDrafts.Count)
            return;

        try
        {
            isWorking = true;
            await JS.InvokeVoidAsync("dartSuiteDraw.captureListPositions", TeamSeedGridId, ".team-seed-card");

            (teamDrafts[sourceTeamIndex], teamDrafts[targetTeamIndex]) = (teamDrafts[targetTeamIndex], teamDrafts[sourceTeamIndex]);

            await PersistTeamSeedOrderAsync();
            await InvokeAsync(StateHasChanged);
            await JS.InvokeVoidAsync("dartSuiteDraw.playCapturedListAnimation", TeamSeedGridId, ".team-seed-card", 320);
        }
        catch (Exception ex)
        {
            teamDraftError = ex.Message;
        }
        finally
        {
            isWorking = false;
        }
    }

    private async Task InsertTeamSeedBeforeAsync(int targetTeamIndex)
    {
        if (selectedTournament is null || !CanEditTournamentStructure || !CanSeedTeams)
            return;

        if (!draggedTeamSeedIndex.HasValue)
            return;

        var sourceTeamIndex = draggedTeamSeedIndex.Value;
        EndTeamSeedDrag();

        if (sourceTeamIndex < 0 || sourceTeamIndex >= teamDrafts.Count || targetTeamIndex < 0 || targetTeamIndex >= teamDrafts.Count)
            return;

        if (sourceTeamIndex == targetTeamIndex)
            return;

        try
        {
            isWorking = true;
            await JS.InvokeVoidAsync("dartSuiteDraw.captureListPositions", TeamSeedGridId, ".team-seed-card");

            var moved = teamDrafts[sourceTeamIndex];
            teamDrafts.RemoveAt(sourceTeamIndex);
            if (sourceTeamIndex < targetTeamIndex)
                targetTeamIndex--;

            teamDrafts.Insert(targetTeamIndex, moved);

            await PersistTeamSeedOrderAsync();
            await InvokeAsync(StateHasChanged);
            await JS.InvokeVoidAsync("dartSuiteDraw.playCapturedListAnimation", TeamSeedGridId, ".team-seed-card", 320);
        }
        catch (Exception ex)
        {
            teamDraftError = ex.Message;
        }
        finally
        {
            isWorking = false;
        }
    }

    private async Task PersistTeamSeedOrderAsync()
    {
        if (selectedTournament is null)
            return;

        for (var i = 0; i < teamDrafts.Count; i++)
        {
            var teamMember = TeamMemberForDraft(teamDrafts[i]);
            if (teamMember is null)
                continue;

            // Only teams within the configured seeded count receive a seed number; the rest get 0.
            var targetSeed = (editSeedingEnabled && editSeedTopCount > 0 && i < editSeedTopCount) ? i + 1 : 0;
            if (teamMember.Seed == targetSeed)
                continue;

            await Api.UpdateParticipantAsync(selectedTournament.Id, new UpdateParticipantRequest(
                selectedTournament.Id,
                teamMember.Id,
                teamMember.DisplayName,
                teamMember.AccountName,
                teamMember.IsAutodartsAccount,
                teamMember.IsManager,
                targetSeed,
                teamMember.SeedPot,
                teamMember.GroupNumber,
                teamMember.Type));
        }

        await LoadParticipantsAsync(selectedTournament.Id);
        await LoadTeamsAsync(selectedTournament.Id);
        await SortTeamDraftsBySeedAnimatedAsync();
    }

    private async Task SortTeamDraftsBySeedAnimatedAsync()
    {
        if (!editSeedingEnabled || teamDrafts.Count < 2)
            return;

        await JS.InvokeVoidAsync("dartSuiteDraw.captureListPositions", TeamSeedGridId, ".team-seed-card");

        teamDrafts = teamDrafts
            .Select((draft, index) => new
            {
                Draft = draft,
                Index = index,
                Seed = TeamSeedForDraft(draft, index + 1)
            })
            .OrderBy(x => x.Seed)
            .ThenBy(x => x.Index)
            .Select(x => x.Draft)
            .ToList();

        await InvokeAsync(StateHasChanged);
        await JS.InvokeVoidAsync("dartSuiteDraw.playCapturedListAnimation", TeamSeedGridId, ".team-seed-card", 320);
    }

    private async Task AutoSaveTeamDraftIfCompleteAsync()
    {
        if (selectedTournament is null || !IsTeamplayActive)
            return;

        if (!CanEditTournamentStructure)
            return;

        await SaveTeamDraftAsync();
    }

    private void RecomputeTeamDraftNameIfAuto(int teamIndex)
    {
        if (teamIndex < 0 || teamIndex >= teamDrafts.Count)
            return;
        if (!teamDrafts[teamIndex].IsAutoName)
            return;

        teamDrafts[teamIndex].Name = BuildAutoTeamName(teamDrafts[teamIndex].MemberParticipantIds, teamIndex);
    }

    private static string TeamDraftMemberSignature(TeamDraftItem draft)
        => string.Join("|", draft.MemberParticipantIds.OrderBy(id => id));

    private async Task GenerateRandomTeamsAsync()
    {
        if (selectedTournament is null || !IsTeamplayActive)
            return;
        if (!EnsureTournamentStructureEditable(message => teamDraftError = message))
            return;

        if (IsRegistrationOpen)
        {
            teamDraftError = "Die Registrierung ist noch geöffnet. Bitte zuerst die Registrierung schließen, bevor Teams gebildet werden.";
            return;
        }

        if (!TeamSizeDividesParticipants)
        {
            teamDraftError = "Die Teamgröße passt nicht zur Teilnehmeranzahl. Bitte Teilnehmer oder Spieler/Team anpassen.";
            return;
        }

        await BeginViewportLockAsync();
        try
        {
            isWorking = true;
            isDrawAnimating = true;
            teamDraftError = null;

            EnsureTeamDraftSlots();

            var lockedTeamIndices = Enumerable.Range(0, teamDrafts.Count)
                .Where(i => teamDrafts[i].MemberParticipantIds.Count == editPlayersPerTeam)
                .ToHashSet();

            var editableTeamIndices = Enumerable.Range(0, teamDrafts.Count)
                .Where(i => !lockedTeamIndices.Contains(i))
                .ToList();

            var lockedParticipantIds = lockedTeamIndices
                .SelectMany(i => teamDrafts[i].MemberParticipantIds)
                .ToHashSet();

            foreach (var i in editableTeamIndices)
            {
                teamDrafts[i].TeamId = null;
                teamDrafts[i].MemberParticipantIds.Clear();
                teamDrafts[i].IsAutoName = true;
                teamDrafts[i].Name = $"Team {i + 1}";
            }

            var shuffled = EffectivePlayerParticipants
                .Where(p => !lockedParticipantIds.Contains(p.Id))
                .OrderBy(_ => Random.Shared.Next())
                .ToList();

            var expectedRemainingSlots = editableTeamIndices.Count * editPlayersPerTeam;
            if (shuffled.Count != expectedRemainingSlots)
            {
                teamDraftError = $"Zufallsbildung nicht möglich: {shuffled.Count} freie Teilnehmer für {expectedRemainingSlots} freie Team-Slots.";
                return;
            }

            if (editableTeamIndices.Count == 0)
            {
                teamDraftError = "Alle Teams sind bereits vollständig. Für komplette Neuverteilung bitte zuerst Entwurf leeren.";
                return;
            }

            var nextEditableIndex = 0;
            var assignedDuringRun = new HashSet<Guid>();

            foreach (var participant in shuffled)
            {
                while (teamDrafts[editableTeamIndices[nextEditableIndex]].MemberParticipantIds.Count >= editPlayersPerTeam)
                    nextEditableIndex = (nextEditableIndex + 1) % editableTeamIndices.Count;

                var nextTeamIndex = editableTeamIndices[nextEditableIndex];

                if (drawAnimationMode == "Exciting")
                {
                    var suspensePool = shuffled.Where(p => !assignedDuringRun.Contains(p.Id)).ToList();
                    var suspenseTargets = editableTeamIndices
                        .Where(i => teamDrafts[i].MemberParticipantIds.Count < editPlayersPerTeam)
                        .ToList();

                    var hopCount = ComputeExcitingHopCount(suspensePool.Count);
                    var hopDelays = BuildDeceleratingHopDelays(hopCount, 70, 250);
                    var targetHopCount = ComputeDropzoneHopCount(suspenseTargets.Count);
                    var targetHopInterval = Math.Max(1, hopDelays.Count / Math.Max(1, targetHopCount));
                    for (var i = 0; i < hopDelays.Count; i++)
                    {
                        var delay = hopDelays[i];
                        suspensePool = shuffled.Where(p => !assignedDuringRun.Contains(p.Id)).ToList();
                        if (suspensePool.Count <= 1) break;

                        drawCandidateParticipantId = suspensePool[Random.Shared.Next(suspensePool.Count)].Id;
                        if (suspenseTargets.Count > 0 && (i == 0 || i == hopDelays.Count - 1 || (i % targetHopInterval) == 0))
                            drawHighlightedTeamIndex = suspenseTargets[Random.Shared.Next(suspenseTargets.Count)];

                        await InvokeAsync(StateHasChanged);
                        await Task.Delay(delay);
                    }

                    drawCandidateParticipantId = participant.Id;
                    drawHighlightedTeamIndex = nextTeamIndex;
                    await InvokeAsync(StateHasChanged);
                    await Task.Delay(320);
                }
                else
                {
                    drawCandidateParticipantId = participant.Id;
                    drawHighlightedTeamIndex = nextTeamIndex;
                    await InvokeAsync(StateHasChanged);

                    if (drawAnimationMode == "Moderate")
                        await Task.Delay(550);
                }

                teamDrafts[nextTeamIndex].MemberParticipantIds.Add(participant.Id);
                RecomputeTeamDraftNameIfAuto(nextTeamIndex);

                drawWinnerParticipantId = participant.Id;
                drawArrivingParticipantId = participant.Id;
                drawCandidateParticipantId = null;
                await InvokeAsync(StateHasChanged);

                if (drawAnimationMode == "Moderate")
                    await Task.Delay(420);
                else if (drawAnimationMode == "Exciting")
                    await Task.Delay(520);

                drawWinnerParticipantId = null;
                drawArrivingParticipantId = null;
                assignedDuringRun.Add(participant.Id);
                nextEditableIndex = (nextEditableIndex + 1) % editableTeamIndices.Count;
            }

            hasUnsavedTeamDraftChanges = true;
        }
        finally
        {
            isDrawAnimating = false;
            drawCandidateParticipantId = null;
            drawWinnerParticipantId = null;
            drawArrivingParticipantId = null;
            drawHighlightedTeamIndex = null;
            await InvokeAsync(StateHasChanged);
            isWorking = false;
            await EndViewportLockAsync();
        }

        await SaveTeamDraftAsync();
        if (editSeedingEnabled)
            await PersistTeamSeedOrderAsync();
    }

    private async Task SaveTeamDraftAsync()
    {
        if (selectedTournament is null || !IsTeamplayActive)
            return;
        if (!EnsureTournamentStructureEditable(message => teamDraftError = message))
            return;

        if (IsRegistrationOpen)
            return; // Block saving while registration is open; UI warning explains this to the user.

        if (!TeamSizeDividesParticipants)
        {
            teamDraftError = "Die Teamgröße passt nicht zur Teilnehmeranzahl.";
            return;
        }

        var seedSnapshotByTeamId = teamDrafts
            .Where(t => t.TeamId.HasValue)
            .Select(t => new { TeamId = t.TeamId!.Value, Seed = TeamSeedForDraft(t, 0) })
            .Where(x => x.Seed > 0)
            .GroupBy(x => x.TeamId)
            .ToDictionary(g => g.Key, g => g.First().Seed);

        var seedSnapshotByMemberSignature = teamDrafts
            .Select(t => new { Signature = TeamDraftMemberSignature(t), Seed = TeamSeedForDraft(t, 0) })
            .Where(x => x.Seed > 0 && !string.IsNullOrWhiteSpace(x.Signature))
            .GroupBy(x => x.Signature)
            .ToDictionary(g => g.Key, g => g.First().Seed, StringComparer.Ordinal);

        var orderSnapshotByTeamId = teamDrafts
            .Select((t, index) => new { t.TeamId, Index = index })
            .Where(x => x.TeamId.HasValue)
            .GroupBy(x => x.TeamId!.Value)
            .ToDictionary(g => g.Key, g => g.First().Index);

        var orderSnapshotByMemberSignature = teamDrafts
            .Select((t, index) => new { Signature = TeamDraftMemberSignature(t), Index = index })
            .Where(x => !string.IsNullOrWhiteSpace(x.Signature))
            .GroupBy(x => x.Signature)
            .ToDictionary(g => g.Key, g => g.First().Index, StringComparer.Ordinal);

        var requestTeams = teamDrafts
            .Select((t, index) => new SaveTeamRequest(
                t.TeamId,
                string.IsNullOrWhiteSpace(t.Name) ? BuildAutoTeamName(t.MemberParticipantIds, index) : t.Name.Trim(),
                t.MemberParticipantIds.ToList()))
            .ToList();

        try
        {
            isWorking = true;
            teamDraftError = null;
            teams = (await Api.SaveTeamsAsync(selectedTournament.Id, new SaveTeamsRequest(selectedTournament.Id, requestTeams))).ToList();
            BuildTeamDraftsFromServerState();
            await LoadParticipantsAsync(selectedTournament.Id);

            if (!editSeedingEnabled)
            {
                teamDrafts = teamDrafts
                    .Select((draft, index) => new
                    {
                        Draft = draft,
                        Index = index,
                        Order = draft.TeamId.HasValue && orderSnapshotByTeamId.TryGetValue(draft.TeamId.Value, out var teamOrder)
                            ? teamOrder
                            : (orderSnapshotByMemberSignature.TryGetValue(TeamDraftMemberSignature(draft), out var signatureOrder)
                                ? signatureOrder
                                : int.MaxValue)
                    })
                    .OrderBy(x => x.Order)
                    .ThenBy(x => x.Index)
                    .Select(x => x.Draft)
                    .ToList();

                await InvokeAsync(StateHasChanged);
                return;
            }

            if (editSeedingEnabled && (seedSnapshotByTeamId.Count > 0 || seedSnapshotByMemberSignature.Count > 0))
            {
                var seedNeedsReapply = false;
                foreach (var draft in teamDrafts)
                {
                    var signature = TeamDraftMemberSignature(draft);
                    var expectedSeed = 0;

                    var hasExpectedSeed = draft.TeamId.HasValue && seedSnapshotByTeamId.TryGetValue(draft.TeamId.Value, out expectedSeed);
                    if (!hasExpectedSeed && !string.IsNullOrWhiteSpace(signature))
                        hasExpectedSeed = seedSnapshotByMemberSignature.TryGetValue(signature, out expectedSeed);

                    if (!hasExpectedSeed)
                        continue;

                    var currentSeed = TeamSeedForDraft(draft, 0);
                    if (currentSeed != expectedSeed)
                    {
                        seedNeedsReapply = true;
                        break;
                    }
                }

                if (seedNeedsReapply)
                {
                    teamDrafts = teamDrafts
                        .Select((draft, index) => new
                        {
                            Draft = draft,
                            Index = index,
                            Seed = draft.TeamId.HasValue && seedSnapshotByTeamId.TryGetValue(draft.TeamId.Value, out var s)
                                ? s
                                : (seedSnapshotByMemberSignature.TryGetValue(TeamDraftMemberSignature(draft), out var sigSeed)
                                    ? sigSeed
                                    : int.MaxValue)
                        })
                        .OrderBy(x => x.Seed)
                        .ThenBy(x => x.Index)
                        .Select(x => x.Draft)
                        .ToList();

                    await PersistTeamSeedOrderAsync();
                }
            }
        }
        catch (Exception ex)
        {
            teamDraftError = ex.Message;
        }
        finally
        {
            isWorking = false;
        }
    }

    private void ResetTeamDraftLocal()
    {
        if (!CanEditTournamentStructure)
            return;

        EnsureTeamDraftSlots();
        for (var i = 0; i < teamDrafts.Count; i++)
        {
            teamDrafts[i].TeamId = null;
            teamDrafts[i].MemberParticipantIds.Clear();
            teamDrafts[i].IsAutoName = true;
            teamDrafts[i].Name = $"Team {i + 1}";
        }

        selectedTeamParticipantId = null;
        draggedTeamParticipantId = null;
        dropTargetTeamIndex = null;
        hasUnsavedTeamDraftChanges = true;
        teamDraftError = null;
    }

    private async Task ReloadTeamDraftFromServerAsync()
    {
        if (selectedTournament is null)
            return;
        if (!EnsureTournamentStructureEditable(message => teamDraftError = message))
            return;

        try
        {
            isWorking = true;
            await LoadTeamsAsync(selectedTournament.Id);
        }
        finally
        {
            isWorking = false;
        }
    }

    private string DrawTeamAnimationCss(int teamIndex)
        => drawHighlightedTeamIndex == teamIndex ? "draw-target-group" : string.Empty;

    private List<ParticipantDto> ResolveDrawAssignmentTargets(ParticipantDto participant)
    {
        if (!IsTeamplayActive)
            return [participant];

        var normalizedDisplay = NormalizeDisplayName(participant.DisplayName);
        var teamMembers = participants
            .Where(IsTeamMember)
            .Where(p =>
                (participant.TeamId.HasValue && p.TeamId == participant.TeamId) ||
                NormalizeDisplayName(p.DisplayName) == normalizedDisplay)
            .GroupBy(p => p.Id)
            .Select(g => g.First())
            .ToList();

        return teamMembers.Count > 0 ? teamMembers : [participant];
    }

    // ─── Draw: Assign Participant to Group ───
    private async Task AssignParticipantToGroupAsync(int groupNumber)
    {
        if (selectedTournament is null || draggedParticipantId is null) return;
        if (!EnsureTournamentStructureEditable(message => editError = message))
        {
            draggedParticipantId = null;
            dropTargetGroupNumber = null;
            return;
        }

        var participantId = draggedParticipantId.Value;
        draggedParticipantId = null;
        dropTargetGroupNumber = null;

        var participant = participants.FirstOrDefault(p => p.Id == participantId);
        if (participant is null) return;

        var assignmentTargets = ResolveDrawAssignmentTargets(participant);
        var targetIds = assignmentTargets.Select(p => p.Id).ToHashSet();

        try
        {
            isWorking = true;

            await JS.InvokeVoidAsync("dartSuiteUi.saveScrollY");
            // Optimistic local update so assigned entries disappear from the unassigned pool immediately.
            participants = participants
                .Select(p => targetIds.Contains(p.Id)
                    ? p with { GroupNumber = groupNumber }
                    : p)
                .ToList();
            await InvokeAsync(StateHasChanged);

            foreach (var target in assignmentTargets)
            {
                await Api.UpdateParticipantAsync(selectedTournament.Id, new UpdateParticipantRequest(
                    selectedTournament.Id, target.Id, target.DisplayName, target.AccountName,
                    target.IsAutodartsAccount, target.IsManager, target.Seed, target.SeedPot,
                    groupNumber));
            }
            await LoadParticipantsAsync(selectedTournament.Id);
            await JS.InvokeVoidAsync("dartSuiteUi.restoreScrollY");
        }
        catch (Exception ex) { editError = ex.Message; }
        finally { isWorking = false; }
    }

    private async Task AssignParticipantToGroupDirectAsync(Guid participantId, int groupNumber)
    {
        if (selectedTournament is null) return;
        if (!EnsureTournamentStructureEditable(message => editError = message)) return;

        var participant = participants.FirstOrDefault(p => p.Id == participantId);
        if (participant is null) return;

        var assignmentTargets = ResolveDrawAssignmentTargets(participant);
        var targetIds = assignmentTargets.Select(p => p.Id).ToHashSet();

        try
        {
            isWorking = true;

            await JS.InvokeVoidAsync("dartSuiteUi.saveScrollY");
            participants = participants
                .Select(p => targetIds.Contains(p.Id)
                    ? p with { GroupNumber = groupNumber }
                    : p)
                .ToList();
            await InvokeAsync(StateHasChanged);

            foreach (var target in assignmentTargets)
            {
                await Api.UpdateParticipantAsync(selectedTournament.Id, new UpdateParticipantRequest(
                    selectedTournament.Id, target.Id, target.DisplayName, target.AccountName,
                    target.IsAutodartsAccount, target.IsManager, target.Seed, target.SeedPot,
                    groupNumber));
            }
            await LoadParticipantsAsync(selectedTournament.Id);
            await JS.InvokeVoidAsync("dartSuiteUi.restoreScrollY");
        }
        catch (Exception ex) { editError = ex.Message; }
        finally { isWorking = false; }
    }

    /// <summary>Remove a participant from their group (back to unassigned).</summary>
    private async Task UnassignParticipantFromGroupAsync(Guid participantId)
    {
        if (selectedTournament is null) return;
        if (!EnsureTournamentStructureEditable(message => editError = message)) return;

        var participant = participants.FirstOrDefault(p => p.Id == participantId);
        if (participant is null) return;

        var assignmentTargets = ResolveDrawAssignmentTargets(participant);
        var targetIds = assignmentTargets.Select(p => p.Id).ToHashSet();

        try
        {
            isWorking = true;

            await JS.InvokeVoidAsync("dartSuiteUi.saveScrollY");
            participants = participants
                .Select(p => targetIds.Contains(p.Id)
                    ? p with { GroupNumber = null }
                    : p)
                .ToList();
            await InvokeAsync(StateHasChanged);

            foreach (var target in assignmentTargets)
            {
                await Api.UpdateParticipantAsync(selectedTournament.Id, new UpdateParticipantRequest(
                    selectedTournament.Id, target.Id, target.DisplayName, target.AccountName,
                    target.IsAutodartsAccount, target.IsManager, target.Seed, target.SeedPot,
                    null));
            }
            await LoadParticipantsAsync(selectedTournament.Id);
            await JS.InvokeVoidAsync("dartSuiteUi.restoreScrollY");
        }
        catch (Exception ex) { editError = ex.Message; }
        finally { isWorking = false; }
    }

    /// <summary>Reset all group assignments (participants go back to unassigned).</summary>
    private async Task ResetDrawAsync()
    {
        if (selectedTournament is null) return;
        if (!EnsureTournamentStructureEditable(message => editError = message)) return;

        try
        {
            isWorking = true;
            // Reset all persisted group assignments to avoid stale team/group edge-cases.
            var targetIds = participants
                .Where(p => p.GroupNumber.HasValue && p.GroupNumber > 0)
                .Select(p => p.Id)
                .ToHashSet();

            // Update the UI immediately so the reset is visible on click, not after the roundtrip completes.
            participants = participants
                .Select(p => targetIds.Contains(p.Id)
                    ? p with { GroupNumber = null }
                    : p)
                .ToList();
            await InvokeAsync(StateHasChanged);

            foreach (var p in participants.Where(p => targetIds.Contains(p.Id)).ToList())
            {
                await Api.UpdateParticipantAsync(selectedTournament.Id, new UpdateParticipantRequest(
                    selectedTournament.Id, p.Id, p.DisplayName, p.AccountName,
                    p.IsAutodartsAccount, p.IsManager, p.Seed, p.SeedPot, null));
            }
            await LoadParticipantsAsync(selectedTournament.Id);
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex) { editError = ex.Message; }
        finally { isWorking = false; }
    }

    /// <summary>Assign seed pots based on seeding list, then auto-distribute to groups.</summary>
    private async Task AssignSeedPotsAndDistributeAsync()
    {
        if (selectedTournament is null) return;
        if (!EnsureTournamentStructureEditable(message => editError = message)) return;

        try
        {
            isWorking = true;
            await Api.AssignSeedPotsAsync(selectedTournament.Id);
            await LoadParticipantsAsync(selectedTournament.Id);
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex) { editError = ex.Message; }
        finally { isWorking = false; }
    }

    private sealed record DrawStep(Guid ParticipantId, int TargetGroup, int SourcePot);

    private string DrawParticipantAnimationCss(Guid participantId)
    {
        if (drawCandidateParticipantId == participantId)
            return "draw-item-candidate";

        var classes = new List<string>(2);
        if (drawWinnerParticipantId == participantId)
            classes.Add("draw-item-winner");
        if (drawArrivingParticipantId == participantId)
            classes.Add("draw-item-arriving");

        return classes.Count > 0 ? string.Join(" ", classes) : string.Empty;
    }

    private string DrawGroupAnimationCss(int groupNumber)
        => drawHighlightedGroupNumber == groupNumber ? "draw-target-group" : string.Empty;

    private string DrawPotAnimationCss(int potNumber)
        => drawSourcePotNumber == potNumber ? "draw-target-group" : string.Empty;

    private static string PotBadgeCss(int potNumber) => (potNumber % 6) switch
    {
        1 => "text-bg-primary",
        2 => "text-bg-success",
        3 => "text-bg-warning",
        4 => "text-bg-danger",
        5 => "text-bg-info",
        _ => "text-bg-secondary"
    };

    private async Task ResetGroupAssignmentsForDrawAsync()
    {
        if (selectedTournament is null) return;
        if (!EnsureTournamentStructureEditable(message => editError = message)) return;

        var assigned = EffectiveDrawParticipants.Where(p => p.GroupNumber.HasValue && p.GroupNumber > 0).ToList();
        var targetIds = assigned
            .SelectMany(ResolveDrawAssignmentTargets)
            .Select(p => p.Id)
            .ToHashSet();

        foreach (var p in participants.Where(p => targetIds.Contains(p.Id)).ToList())
        {
            var updated = await Api.UpdateParticipantAsync(selectedTournament.Id, new UpdateParticipantRequest(
                selectedTournament.Id, p.Id, p.DisplayName, p.AccountName,
                p.IsAutodartsAccount, p.IsManager, p.Seed, p.SeedPot, null));
            ReplaceParticipant(updated);
        }
    }

    private void ReplaceParticipant(ParticipantDto updated)
    {
        var idx = participants.FindIndex(p => p.Id == updated.Id);
        if (idx >= 0) participants[idx] = updated;
    }

    private List<DrawStep> BuildRandomDrawPlan()
    {
        var pool = UnassignedParticipants;
        var steps = new List<DrawStep>();
        if (pool.Count == 0 || editGroupCount < 1) return steps;

        var ranked = pool
            .Where(p => editSeedingEnabled && editSeedTopCount > 0 && p.Seed > 0 && p.Seed <= editSeedTopCount)
            .OrderBy(p => p.Seed)
            .ToList();

        var remaining = pool
            .Except(ranked)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        var groupSizes = Enumerable.Range(1, editGroupCount).ToDictionary(g => g, _ => 0);

        for (var i = 0; i < ranked.Count; i++)
        {
            var targetGroup = (i % editGroupCount) + 1;
            groupSizes[targetGroup]++;
            steps.Add(new DrawStep(ranked[i].Id, targetGroup, ranked[i].SeedPot));
        }

        foreach (var p in remaining)
        {
            var targetGroup = groupSizes
                .OrderBy(x => x.Value)
                .ThenBy(_ => Random.Shared.Next())
                .First().Key;
            groupSizes[targetGroup]++;
            steps.Add(new DrawStep(p.Id, targetGroup, p.SeedPot));
        }

        return steps;
    }

    private List<DrawStep> BuildSeededPotsDrawPlan()
    {
        var steps = new List<DrawStep>();
        if (editGroupCount < 1) return steps;

        var groupSizes = Enumerable.Range(1, editGroupCount).ToDictionary(g => g, _ => 0);
        var assignedIds = new HashSet<Guid>();

        var pots = UnassignedParticipants
            .Where(p => p.SeedPot > 0)
            .GroupBy(p => p.SeedPot)
            .OrderBy(g => g.Key)
            .ToList();

        foreach (var pot in pots)
        {
            var remaining = pot.OrderBy(_ => Random.Shared.Next()).ToList();

            while (remaining.Count > 0)
            {
                var groupOrder = Enumerable.Range(1, editGroupCount)
                    .OrderBy(_ => Random.Shared.Next())
                    .ToList();

                foreach (var group in groupOrder)
                {
                    if (remaining.Count == 0)
                        break;

                    var drawIndex = Random.Shared.Next(remaining.Count);
                    var picked = remaining[drawIndex];
                    remaining.RemoveAt(drawIndex);

                    steps.Add(new DrawStep(picked.Id, group, pot.Key));
                    assignedIds.Add(picked.Id);
                    groupSizes[group]++;
                }
            }
        }

        // Fallback: if some participants have no pot assignment, still distribute them fairly.
        var withoutPot = UnassignedParticipants
            .Where(p => p.SeedPot <= 0 && !assignedIds.Contains(p.Id))
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        foreach (var participant in withoutPot)
        {
            var targetGroup = groupSizes
                .OrderBy(x => x.Value)
                .ThenBy(_ => Random.Shared.Next())
                .First().Key;

            steps.Add(new DrawStep(participant.Id, targetGroup, 0));
            groupSizes[targetGroup]++;
            assignedIds.Add(participant.Id);
        }

        return steps;
    }

    private List<DrawStep> BuildDrawPlan()
    {
        if (editGroupDrawMode == "SeededPots")
        {
            var seededPlan = BuildSeededPotsDrawPlan();
            if (seededPlan.Count > 0)
                return seededPlan;

            // No pots available or no seeded result possible: fallback to random to avoid dead-end.
            return BuildRandomDrawPlan();
        }

        return BuildRandomDrawPlan();
    }

    private int KnockoutBracketSize
    {
        get
        {
            var size = 1;
            while (size < EffectiveDrawParticipants.Count) size *= 2;
            return Math.Max(2, size);
        }
    }

    private int KnockoutMatchCount => KnockoutBracketSize / 2;

    private static int[] BuildSeededBracketOrder(int size)
    {
        if (size == 1) return [0];
        if (size == 2) return [0, 1];

        var result = new int[size];
        result[0] = 0;
        result[1] = 1;
        var positions = 2;

        while (positions < size)
        {
            var temp = new int[positions * 2];
            for (var i = 0; i < positions; i++)
            {
                temp[i * 2] = result[i];
                temp[i * 2 + 1] = positions * 2 - 1 - result[i];
            }
            Array.Copy(temp, result, positions * 2);
            positions *= 2;
        }

        return result;
    }

    private void EnsureKnockoutDrawCards()
    {
        if (selectedTournament?.Mode != "Knockout") return;
        var required = KnockoutMatchCount;
        if (knockoutDrawCards.Count == required) return;

        knockoutDrawCards = Enumerable.Range(1, required)
            .Select(i => new KnockoutDrawCard { MatchNumber = i })
            .ToList();
    }

    private void CleanupKnockoutDrawCards()
    {
        if (selectedTournament?.Mode != "Knockout" || knockoutDrawCards.Count == 0) return;
        var validIds = EffectiveDrawParticipants.Select(p => p.Id).ToHashSet();
        foreach (var card in knockoutDrawCards)
        {
            if (card.HomeParticipantId.HasValue && !validIds.Contains(card.HomeParticipantId.Value))
                card.HomeParticipantId = null;
            if (card.AwayParticipantId.HasValue && !validIds.Contains(card.AwayParticipantId.Value))
                card.AwayParticipantId = null;
        }
    }

    private bool IsKnockoutDrawComplete => KnockoutAssignedParticipants.Count == EffectiveDrawParticipants.Count;

    private List<ParticipantDto> KnockoutAssignedParticipants
    {
        get
        {
            var ids = knockoutDrawCards
                .SelectMany(c => new[] { c.HomeParticipantId, c.AwayParticipantId })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();
            return EffectiveDrawParticipants.Where(p => ids.Contains(p.Id)).ToList();
        }
    }

    private List<ParticipantDto> KnockoutUnassignedParticipants
    {
        get
        {
            var assignedIds = KnockoutAssignedParticipants.Select(p => p.Id).ToHashSet();
            return EffectiveDrawParticipants
                .Where(p => !assignedIds.Contains(p.Id))
                .OrderBy(p => p.Seed > 0 ? p.Seed : int.MaxValue)
                .ThenBy(p => p.DisplayName)
                .ToList();
        }
    }

    private ParticipantDto? FindParticipant(Guid? id)
        => id.HasValue ? EffectiveDrawParticipants.FirstOrDefault(p => p.Id == id.Value) : null;

    private void StartKnockoutDrag(Guid participantId)
    {
        if (!CanEditTournamentStructure)
            return;

        draggedKnockoutParticipantId = participantId;
    }

    private void RemoveParticipantFromAllKnockoutCards(Guid participantId)
    {
        foreach (var card in knockoutDrawCards)
        {
            if (card.HomeParticipantId == participantId) card.HomeParticipantId = null;
            if (card.AwayParticipantId == participantId) card.AwayParticipantId = null;
        }
    }

    private void AssignKnockoutCardSlot(int matchNumber, bool isHomeSlot, Guid participantId)
    {
        if (!CanEditTournamentStructure)
            return;

        EnsureKnockoutDrawCards();
        var card = knockoutDrawCards.FirstOrDefault(c => c.MatchNumber == matchNumber);
        if (card is null) return;

        RemoveParticipantFromAllKnockoutCards(participantId);

        if (isHomeSlot)
            card.HomeParticipantId = participantId;
        else
            card.AwayParticipantId = participantId;
    }

    private void AssignDraggedToKnockoutCard(int matchNumber, bool isHomeSlot)
    {
        if (!CanEditTournamentStructure)
        {
            draggedKnockoutParticipantId = null;
            return;
        }

        if (!draggedKnockoutParticipantId.HasValue) return;
        AssignKnockoutCardSlot(matchNumber, isHomeSlot, draggedKnockoutParticipantId.Value);
        draggedKnockoutParticipantId = null;
    }

    private void ClearKnockoutCardSlot(int matchNumber, bool isHomeSlot)
    {
        if (!CanEditTournamentStructure)
            return;

        var card = knockoutDrawCards.FirstOrDefault(c => c.MatchNumber == matchNumber);
        if (card is null) return;
        if (isHomeSlot)
            card.HomeParticipantId = null;
        else
            card.AwayParticipantId = null;
    }

    private void ResetKnockoutDrawCards()
    {
        knockoutDrawCards.ForEach(c => { c.HomeParticipantId = null; c.AwayParticipantId = null; });
        HideKoDrawToken();
    }

    private string DrawKnockoutSlotCss(int matchNumber, bool isHomeSlot)
    {
        if (drawHighlightedKoMatchNumber == matchNumber && drawHighlightedKoHomeSlot == isHomeSlot)
            return "draw-target-group";
        return string.Empty;
    }

    private sealed class RelativePoint
    {
        public double Left { get; set; }
        public double Top { get; set; }
    }

    private static string KnockoutSlotElementId(int matchNumber, bool isHomeSlot)
        => $"ko-slot-{matchNumber}-{(isHomeSlot ? "home" : "away")}";

    private async Task MoveKoDrawTokenToSlotAsync(int matchNumber, bool isHomeSlot)
    {
        var targetId = KnockoutSlotElementId(matchNumber, isHomeSlot);
        var point = await JS.InvokeAsync<RelativePoint?>("dartSuiteDraw.getRelativeCenter", KoDrawContainerId, targetId);
        if (point is null) return;

        koDrawTokenStyle = $"left:{point.Left}px; top:{point.Top}px;";
        showKoDrawToken = true;
        await InvokeAsync(StateHasChanged);
    }

    private void HideKoDrawToken()
    {
        showKoDrawToken = false;
        koDrawTokenStyle = string.Empty;
    }

    private sealed record KnockoutDrawStep(Guid ParticipantId, int MatchNumber, bool IsHomeSlot);

    private List<KnockoutDrawStep> BuildKnockoutAutoDrawSteps()
    {
        EnsureKnockoutDrawCards();
        var result = new List<KnockoutDrawStep>();
        var bracketSize = KnockoutBracketSize;

        List<ParticipantDto> ordered;
        if (editSeedingEnabled)
        {
            var ranked = EffectiveDrawParticipants
                .Where(p => p.Seed > 0 && p.Seed <= editSeedTopCount)
                .OrderBy(p => p.Seed)
                .ToList();
            var unranked = EffectiveDrawParticipants
                .Except(ranked)
                .OrderBy(_ => Random.Shared.Next())
                .ToList();
            ordered = ranked.Concat(unranked).ToList();
        }
        else
        {
            ordered = EffectiveDrawParticipants.OrderBy(_ => Random.Shared.Next()).ToList();
        }

        var seedOrder = BuildSeededBracketOrder(bracketSize);
        for (var pos = 0; pos < bracketSize; pos++)
        {
            var seedIndex = seedOrder[pos];
            if (seedIndex >= ordered.Count) continue;

            var participant = ordered[seedIndex];
            var matchNumber = (pos / 2) + 1;
            var isHome = pos % 2 == 0;
            result.Add(new KnockoutDrawStep(participant.Id, matchNumber, isHome));
        }

        return result;
    }

    private List<(int MatchNumber, bool IsHomeSlot)> FreeKnockoutSlots()
    {
        return knockoutDrawCards
            .SelectMany(c => new[]
            {
                (c.MatchNumber, IsHomeSlot: true, Occupied: c.HomeParticipantId.HasValue),
                (c.MatchNumber, IsHomeSlot: false, Occupied: c.AwayParticipantId.HasValue)
            })
            .Where(x => !x.Occupied)
            .Select(x => (x.MatchNumber, x.IsHomeSlot))
            .ToList();
    }

    private async Task AnimateKnockoutDrawStepsAsync(List<KnockoutDrawStep> steps)
    {
        isDrawAnimating = true;
        var assignedDuringRun = new HashSet<Guid>();
        try
        {
            foreach (var step in steps)
            {
                if (drawAnimationMode == "Moderate")
                {
                    drawCandidateParticipantId = step.ParticipantId;
                    drawHighlightedKoMatchNumber = step.MatchNumber;
                    drawHighlightedKoHomeSlot = step.IsHomeSlot;
                    await MoveKoDrawTokenToSlotAsync(step.MatchNumber, step.IsHomeSlot);
                    await InvokeAsync(StateHasChanged);
                    await Task.Delay(550);

                    AssignKnockoutCardSlot(step.MatchNumber, step.IsHomeSlot, step.ParticipantId);
                    drawWinnerParticipantId = step.ParticipantId;
                    drawArrivingParticipantId = step.ParticipantId;
                    drawCandidateParticipantId = null;
                    await InvokeAsync(StateHasChanged);
                    await Task.Delay(420);
                    drawWinnerParticipantId = null;
                    drawArrivingParticipantId = null;
                }
                else if (drawAnimationMode == "Exciting")
                {
                    var slots = FreeKnockoutSlots();
                    slots = slots.OrderBy(_ => Random.Shared.Next()).ToList();

                    var suspensePool = steps
                        .Select(s => s.ParticipantId)
                        .Where(id => !assignedDuringRun.Contains(id))
                        .Distinct()
                        .ToList();

                    var hopCount = ComputeExcitingHopCount(suspensePool.Count);
                    var hopDelays = BuildDeceleratingHopDelays(hopCount, 70, 220);
                    var targetHopCount = ComputeDropzoneHopCount(slots.Count);
                    var targetHopInterval = Math.Max(1, hopDelays.Count / Math.Max(1, targetHopCount));

                    for (var i = 0; i < hopDelays.Count; i++)
                    {
                        suspensePool = steps
                            .Select(s => s.ParticipantId)
                            .Where(id => !assignedDuringRun.Contains(id))
                            .Distinct()
                            .ToList();
                        if (suspensePool.Count <= 1)
                            break;

                        drawCandidateParticipantId = suspensePool[Random.Shared.Next(suspensePool.Count)];

                        if (slots.Count > 0 && (i == 0 || i == hopDelays.Count - 1 || (i % targetHopInterval) == 0))
                        {
                            var hop = slots[Random.Shared.Next(slots.Count)];
                            drawHighlightedKoMatchNumber = hop.MatchNumber;
                            drawHighlightedKoHomeSlot = hop.IsHomeSlot;
                            await MoveKoDrawTokenToSlotAsync(hop.MatchNumber, hop.IsHomeSlot);
                        }

                        await InvokeAsync(StateHasChanged);
                        await Task.Delay(hopDelays[i]);
                    }

                    drawHighlightedKoMatchNumber = step.MatchNumber;
                    drawHighlightedKoHomeSlot = step.IsHomeSlot;
                    drawCandidateParticipantId = step.ParticipantId;
                    await InvokeAsync(StateHasChanged);
                    await Task.Delay(280);

                    await MoveKoDrawTokenToSlotAsync(step.MatchNumber, step.IsHomeSlot);
                    AssignKnockoutCardSlot(step.MatchNumber, step.IsHomeSlot, step.ParticipantId);
                    drawWinnerParticipantId = step.ParticipantId;
                    drawArrivingParticipantId = step.ParticipantId;
                    drawCandidateParticipantId = null;
                    await InvokeAsync(StateHasChanged);
                    await Task.Delay(520);
                    drawWinnerParticipantId = null;
                    drawArrivingParticipantId = null;
                }
                else
                {
                    AssignKnockoutCardSlot(step.MatchNumber, step.IsHomeSlot, step.ParticipantId);
                }

                drawCandidateParticipantId = null;
                drawHighlightedKoMatchNumber = null;
                drawHighlightedKoHomeSlot = null;
                HideKoDrawToken();
                assignedDuringRun.Add(step.ParticipantId);
                await InvokeAsync(StateHasChanged);
            }
        }
        finally
        {
            isDrawAnimating = false;
            drawCandidateParticipantId = null;
            drawWinnerParticipantId = null;
            drawArrivingParticipantId = null;
            drawHighlightedKoMatchNumber = null;
            drawHighlightedKoHomeSlot = null;
            HideKoDrawToken();
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task AutoDrawKnockoutAsync()
    {
        EnsureKnockoutDrawCards();
        ResetKnockoutDrawCards();
        var steps = BuildKnockoutAutoDrawSteps();

        if (drawAnimationMode == "Off")
        {
            foreach (var s in steps)
                AssignKnockoutCardSlot(s.MatchNumber, s.IsHomeSlot, s.ParticipantId);
            await InvokeAsync(StateHasChanged);
            return;
        }

        await AnimateKnockoutDrawStepsAsync(steps);
    }

    private async Task ApplyKnockoutDrawCardsToSeedsAsync()
    {
        if (selectedTournament is null) return;
        if (!EnsureTournamentStructureEditable(message => editError = message)) return;

        EnsureKnockoutDrawCards();

        var bracketSize = KnockoutBracketSize;
        var seedOrder = BuildSeededBracketOrder(bracketSize);
        var desiredSeedByParticipant = new Dictionary<Guid, int>();

        foreach (var card in knockoutDrawCards)
        {
            var homePos = (card.MatchNumber - 1) * 2;
            var awayPos = homePos + 1;

            if (card.HomeParticipantId.HasValue)
                desiredSeedByParticipant[card.HomeParticipantId.Value] = seedOrder[homePos] + 1;
            if (card.AwayParticipantId.HasValue)
                desiredSeedByParticipant[card.AwayParticipantId.Value] = seedOrder[awayPos] + 1;
        }

        var updates = participants
            .Where(p => desiredSeedByParticipant.ContainsKey(p.Id) && p.Seed != desiredSeedByParticipant[p.Id])
            .ToList();

        if (!updates.Any()) return;

        foreach (var participant in updates)
        {
            var newSeed = desiredSeedByParticipant[participant.Id];
            var updated = await Api.UpdateParticipantAsync(selectedTournament.Id, new UpdateParticipantRequest(
                selectedTournament.Id, participant.Id, participant.DisplayName, participant.AccountName,
                participant.IsAutodartsAccount, participant.IsManager, newSeed, participant.SeedPot, participant.GroupNumber));
            ReplaceParticipant(updated);
        }
    }

    private List<ParticipantDto> GetAnimationSourceCandidates(int sourcePot)
    {
        var unassigned = UnassignedParticipants;
        if (sourcePot > 0)
            return unassigned.Where(p => p.SeedPot == sourcePot).ToList();
        return unassigned;
    }

    private static List<int> BuildDeceleratingHopDelays(int hopCount, int startMs, int endMs)
    {
        if (hopCount <= 0)
            return [];

        var delays = new List<int>(hopCount);
        for (var i = 0; i < hopCount; i++)
        {
            var t = hopCount == 1 ? 1d : (double)i / (hopCount - 1);
            var eased = t * t;
            var delay = (int)Math.Round(startMs + ((endMs - startMs) * eased), MidpointRounding.AwayFromZero);
            delays.Add(Math.Max(40, delay));
        }

        return delays;
    }

    private static int ComputeExcitingHopCount(int candidateCount)
    {
        if (candidateCount <= 1)
            return 1;

        return Math.Clamp(candidateCount * 2, 8, 18);
    }

    private static int ComputeDropzoneHopCount(int slotCount)
    {
        if (slotCount <= 1)
            return 1;

        return Math.Clamp(slotCount / 3 + 1, 2, 4);
    }

    private async Task BeginViewportLockAsync()
    {
        keepDrawViewportStable = true;
        try
        {
            await JS.InvokeVoidAsync("dartSuiteUi.saveScrollY");
        }
        catch
        {
            // ignore JS interop errors during prerender
        }
    }

    private async Task EndViewportLockAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("dartSuiteUi.restoreScrollY");
        }
        catch
        {
            // ignore JS interop errors during prerender
        }

        keepDrawViewportStable = false;
    }

    private async Task ApplyDrawStepAsync(DrawStep step)
    {
        if (selectedTournament is null) return;
        var participant = participants.FirstOrDefault(p => p.Id == step.ParticipantId);
        if (participant is null) return;

        var assignmentTargets = ResolveDrawAssignmentTargets(participant);
        foreach (var target in assignmentTargets)
        {
            var updated = await Api.UpdateParticipantAsync(selectedTournament.Id, new UpdateParticipantRequest(
                selectedTournament.Id, target.Id, target.DisplayName, target.AccountName,
                target.IsAutodartsAccount, target.IsManager, target.Seed, target.SeedPot, step.TargetGroup));
            ReplaceParticipant(updated);
        }
    }

    private async Task AnimateDrawStepsAsync(List<DrawStep> steps)
    {
        isDrawAnimating = true;
        try
        {
            foreach (var step in steps)
            {
                drawHighlightedGroupNumber = step.TargetGroup;
                drawSourcePotNumber = step.SourcePot > 0 ? step.SourcePot : null;

                if (drawAnimationMode == "Moderate")
                {
                    drawCandidateParticipantId = step.ParticipantId;
                    await InvokeAsync(StateHasChanged);
                    await Task.Delay(550);

                    await ApplyDrawStepAsync(step);

                    drawWinnerParticipantId = step.ParticipantId;
                    drawArrivingParticipantId = step.ParticipantId;
                    drawCandidateParticipantId = null;
                    await InvokeAsync(StateHasChanged);
                    await Task.Delay(420);
                    drawWinnerParticipantId = null;
                    drawArrivingParticipantId = null;
                }
                else if (drawAnimationMode == "Exciting")
                {
                    var candidates = GetAnimationSourceCandidates(step.SourcePot);
                    var hopCount = ComputeExcitingHopCount(candidates.Count);
                    var hopDelays = BuildDeceleratingHopDelays(hopCount, 70, 250);
                    var targetHopCount = ComputeDropzoneHopCount(editGroupCount);
                    var targetHopInterval = Math.Max(1, hopDelays.Count / Math.Max(1, targetHopCount));
                    for (var i = 0; i < hopDelays.Count; i++)
                    {
                        var delay = hopDelays[i];
                        candidates = GetAnimationSourceCandidates(step.SourcePot);
                        if (candidates.Count <= 1) break;

                        drawCandidateParticipantId = candidates[Random.Shared.Next(candidates.Count)].Id;
                        if (i == 0 || i == hopDelays.Count - 1 || (i % targetHopInterval) == 0)
                            drawHighlightedGroupNumber = Random.Shared.Next(1, editGroupCount + 1);
                        await InvokeAsync(StateHasChanged);
                        await Task.Delay(delay);
                    }

                    drawCandidateParticipantId = step.ParticipantId;
                    drawHighlightedGroupNumber = step.TargetGroup;
                    await InvokeAsync(StateHasChanged);
                    await Task.Delay(320);

                    await ApplyDrawStepAsync(step);
                    drawWinnerParticipantId = step.ParticipantId;
                    drawArrivingParticipantId = step.ParticipantId;
                    drawCandidateParticipantId = null;
                    await InvokeAsync(StateHasChanged);
                    await Task.Delay(520);
                    drawWinnerParticipantId = null;
                    drawArrivingParticipantId = null;
                }
                else
                {
                    await ApplyDrawStepAsync(step);
                }

                drawHighlightedGroupNumber = null;
                drawSourcePotNumber = null;
                await InvokeAsync(StateHasChanged);
            }
        }
        finally
        {
            isDrawAnimating = false;
            drawCandidateParticipantId = null;
            drawWinnerParticipantId = null;
            drawArrivingParticipantId = null;
            drawHighlightedGroupNumber = null;
            drawSourcePotNumber = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task HarmonizeTeamGroupAssignmentsAsync()
    {
        if (selectedTournament is null || !IsTeamplayActive || editGroupCount < 1)
            return;

        var desiredGroupByParticipantId = new Dictionary<Guid, int>();

        for (var group = 1; group <= editGroupCount; group++)
        {
            foreach (var representative in GroupParticipants(group))
            {
                foreach (var target in ResolveDrawAssignmentTargets(representative))
                {
                    desiredGroupByParticipantId[target.Id] = group;
                }
            }
        }

        if (desiredGroupByParticipantId.Count == 0)
            return;

        foreach (var participant in participants.Where(p => desiredGroupByParticipantId.ContainsKey(p.Id)).ToList())
        {
            var targetGroup = desiredGroupByParticipantId[participant.Id];
            if (participant.GroupNumber == targetGroup)
                continue;

            var updated = await Api.UpdateParticipantAsync(selectedTournament.Id, new UpdateParticipantRequest(
                selectedTournament.Id, participant.Id, participant.DisplayName, participant.AccountName,
                participant.IsAutodartsAccount, participant.IsManager, participant.Seed, participant.SeedPot, targetGroup));
            ReplaceParticipant(updated);
        }
    }

    /// <summary>Auto-draw participants into groups using selected mode and optional animation.</summary>
    private async Task AutoDrawAsync()
    {
        if (selectedTournament is null) return;
        // editGroupCount only matters for GroupAndKnockout; K.O.-only draw has no group count.
        if (selectedTournament.Mode != "Knockout" && editGroupCount < 1) return;
        if (!EnsureTournamentStructureEditable(message => editError = message)) return;

        if (IsRegistrationOpen)
        {
            registrationDrawContinuation = AutoDrawAsync;
            showRegistrationDrawConfirmation = true;
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (selectedTournament.Mode == "Knockout")
        {
            await BeginViewportLockAsync();
            try
            {
                isWorking = true;
                await AutoDrawKnockoutAsync();
            }
            catch (Exception ex) { editError = ex.Message; }
            finally
            {
                isWorking = false;
                await InvokeAsync(StateHasChanged);
                await EndViewportLockAsync();
            }
            return;
        }

        await BeginViewportLockAsync();
        try
        {
            isWorking = true;

            // Always start from a clean assignment state for deterministic draw animation/result.
            await ResetGroupAssignmentsForDrawAsync();

            if (editGroupDrawMode == "SeededPots" && editSeedingEnabled)
            {
                var withPots = await Api.AssignSeedPotsAsync(selectedTournament.Id);
                participants = withPots.ToList();
            }

            var drawPlan = BuildDrawPlan();

            if (drawPlan.Count == 0 && UnassignedParticipants.Count > 0)
            {
                editError = "Auslosung konnte nicht erstellt werden. Bitte Lostöpfe prüfen oder den Modus auf 'Zufällig' stellen.";
                return;
            }

            if (drawAnimationMode == "Off")
            {
                foreach (var step in drawPlan)
                    await ApplyDrawStepAsync(step);
            }
            else
            {
                await AnimateDrawStepsAsync(drawPlan);
            }

            await HarmonizeTeamGroupAssignmentsAsync();

            await LoadParticipantsAsync(selectedTournament.Id);
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex) { editError = ex.Message; }
        finally
        {
            isWorking = false;
            await InvokeAsync(StateHasChanged);
            await EndViewportLockAsync();
        }
    }

    /// <summary>Delete tournament plan (matches only, keep group assignments).</summary>
    private async Task DeleteTournamentPlanAsync()
    {
        if (selectedTournament is null) return;
        if (!CanEditTournamentStructure)
        {
            ShowInactiveActionInfo(CannotEditStructureReason, "Turnierplan löschen nicht möglich");
            return;
        }

        confirmationMessage = "Alle Matches werden gelöscht. Die Gruppeneinteilung bleibt bestehen. Wirklich fortfahren?";
        showConfirmationPlanImpact = false;
        confirmationAction = async () =>
        {
            try
            {
                isWorking = true;
                // Reset to Erstellt deletes matches, or we need a dedicated endpoint
                // For now: use status transition to delete matches then restore status
                var currentStatus = selectedTournament.Status;
                await Api.UpdateTournamentStatusAsync(selectedTournament.Id, "Erstellt");
                if (currentStatus != "Erstellt")
                    await Api.UpdateTournamentStatusAsync(selectedTournament.Id, currentStatus);
                await LoadTournamentsAsync();
                selectedTournament = tournaments.FirstOrDefault(x => x.Id == selectedTournament.Id) ?? selectedTournament;
                matches = (await Api.GetMatchesAsync(selectedTournament.Id)).ToList();
                groupStandings = [];
                await LoadRoundsAsync();
                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex) { editError = ex.Message; }
            finally { isWorking = false; }
        };
        showConfirmation = true;
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Create the tournament plan (finalize draw).</summary>
    private async Task CreateTournamentPlanAsync()
    {
        if (selectedTournament is null) return;
        if (!EnsureTournamentStructureEditable(message => editError = message)) return;

        if (!CanExecuteDrawCreatePlan)
        {
            ShowInactiveActionInfo(DrawCreatePlanDisabledReason, "Turnierplan erstellen nicht möglich");
            return;
        }

        if (IsRegistrationOpen)
        {
            registrationDrawContinuation = CreateTournamentPlanAsync;
            showRegistrationDrawConfirmation = true;
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (IsTeamplayActive && !CanProceedWithTeamDraw)
        {
            editError = hasUnsavedTeamDraftChanges
                ? "Bitte Teamzuordnungen zuerst speichern."
                : "Teamplay ist aktiv: Alle Teams müssen vollständig gebildet sein, bevor der Turnierplan erstellt wird.";
            return;
        }
        try
        {
            isWorking = true;
            if (selectedTournament.Mode == "GroupAndKnockout")
            {
                await Api.GenerateGroupMatchesAsync(selectedTournament.Id);
                matches = (await Api.GetMatchesAsync(selectedTournament.Id)).ToList();
                groupStandings = (await Api.GetGroupStandingsAsync(selectedTournament.Id)).ToList();
                await LoadRoundsAsync();
                activeTab = HasMissingRoundConfig() ? "rounds" : "groups";
            }
            else
            {
                if (IsTeamplayActive && !CanProceedWithTeamDraw)
                {
                    editError = hasUnsavedTeamDraftChanges
                        ? "Bitte Teamzuordnungen zuerst speichern."
                        : "Teamplay ist aktiv: Alle Teams müssen vollständig gebildet sein, bevor ausgelost werden kann.";
                    return;
                }
                await ApplyKnockoutDrawCardsToSeedsAsync();
                matches = (await Api.GenerateMatchesAsync(selectedTournament.Id)).ToList();
                await LoadRoundsAsync();
                activeTab = HasMissingRoundConfig() ? "rounds" : "knockout";
            }
        }
        catch (Exception ex) { editError = ex.Message; }
        finally { isWorking = false; }
    }

    private async Task NormalizeSeedRanksAsync()
    {
        if (selectedTournament is null || !editSeedingEnabled) return;

        if (IsTeamplayActive)
        {
            await PersistTeamSeedOrderAsync();
            return;
        }

        var targetCount = Math.Min(editSeedTopCount, EffectiveDrawParticipants.Count);

        var ordered = EffectiveDrawParticipants
            .OrderBy(p => p.Seed > 0 ? p.Seed : int.MaxValue)
            .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var updates = ordered
            .Select((p, idx) => new { Participant = p, NewSeed = targetCount > 0 && idx < targetCount ? idx + 1 : 0 })
            .Where(x => x.Participant.Seed != x.NewSeed)
            .ToList();

        if (updates.Count == 0) return;

        try
        {
            isWorking = true;
            foreach (var update in updates)
            {
                await Api.UpdateParticipantAsync(selectedTournament.Id, new UpdateParticipantRequest(
                    selectedTournament.Id, update.Participant.Id, update.Participant.DisplayName, update.Participant.AccountName,
                    update.Participant.IsAutodartsAccount, update.Participant.IsManager, update.NewSeed,
                    update.Participant.SeedPot, update.Participant.GroupNumber));
            }
            await LoadParticipantsAsync(selectedTournament.Id);
        }
        catch (Exception ex) { editError = ex.Message; }
        finally { isWorking = false; }
    }

    private async Task ResetAllSeedRanksAsync()
    {
        if (selectedTournament is null)
            return;

        var updates = EffectiveDrawParticipants
            .Where(p => p.Seed != 0)
            .ToList();

        if (updates.Count == 0)
            return;

        try
        {
            isWorking = true;
            foreach (var participant in updates)
            {
                await Api.UpdateParticipantAsync(selectedTournament.Id, new UpdateParticipantRequest(
                    selectedTournament.Id,
                    participant.Id,
                    participant.DisplayName,
                    participant.AccountName,
                    participant.IsAutodartsAccount,
                    participant.IsManager,
                    0,
                    participant.SeedPot,
                    participant.GroupNumber));
            }

            await LoadParticipantsAsync(selectedTournament.Id);
            if (IsTeamplayActive)
            {
                await LoadTeamsAsync(selectedTournament.Id);
                await SortTeamDraftsBySeedAnimatedAsync();
            }
        }
        catch (Exception ex)
        {
            editError = ex.Message;
        }
        finally
        {
            isWorking = false;
        }
    }

    // ─── Seeding: Update participant seed via drag & drop ───
    private async Task UpdateParticipantSeedAsync(Guid participantId, int newSeed)
    {
        if (selectedTournament is null) return;
        if (!EnsureTournamentStructureEditable(message => editError = message)) return;

        var participant = participants.FirstOrDefault(p => p.Id == participantId);
        if (participant is null) return;
        if (IsTeamplayActive && !IsTeamMember(participant))
        {
            editError = "Im Teamplay können nur Team-Teilnehmer (TT) gesetzt werden.";
            return;
        }

        var maxSeedCount = Math.Max(0, EffectiveDrawParticipants.Count);
        var clampedSeed = Math.Clamp(newSeed, 0, maxSeedCount);
        try
        {
            isWorking = true;
            await Api.UpdateParticipantAsync(selectedTournament.Id, new UpdateParticipantRequest(
                selectedTournament.Id, participantId, participant.DisplayName, participant.AccountName,
                participant.IsAutodartsAccount, participant.IsManager, clampedSeed, participant.SeedPot, participant.GroupNumber));
            await LoadParticipantsAsync(selectedTournament.Id);
        }
        catch (Exception ex) { editError = ex.Message; }
        finally { isWorking = false; }
    }

    /// <summary>Get the drop highlight CSS for a group container during drag.</summary>
    private string GroupDropHighlightCss(int groupNumber)
    {
        if (draggedParticipantId is null) return string.Empty;

        var groupSizes = Enumerable.Range(1, editGroupCount)
            .Select(g => GroupParticipants(g).Count)
            .ToList();
        var currentSize = groupSizes.Count >= groupNumber ? groupSizes[groupNumber - 1] : 0;
        var maxSize = groupSizes.Count > 0 ? groupSizes.Max() : 0;

        // After adding this participant, the size would be:
        var newSize = currentSize + 1;
        if (newSize > maxSize && groupSizes.Count(s => s == maxSize) <= 1 && currentSize == maxSize)
            return "border-danger bg-danger bg-opacity-10"; // sole largest → red
        if (currentSize < maxSize)
            return "border-success bg-success bg-opacity-10"; // smaller than largest → green
        return string.Empty; // OK
    }

    private static string StatusBadgeCss(string status) => status switch
    {
        "Erstellt" => "text-bg-secondary",
        "Geplant" => "text-bg-primary",
        "Gestartet" => "text-bg-success",
        "Beendet" => "text-bg-dark",
        "Abgebrochen" => "text-bg-danger",
        _ => "text-bg-warning"
    };

    // ─── Blitztabelle (Flash Table) ───
    private List<GroupStandingDto> GetFlashStandings(int groupNumber)
    {
        // Start with existing standings
        var existingStandings = groupStandings.Where(s => s.GroupNumber == groupNumber).ToList();

        // Find running (started but not finished) group matches
        var runningMatches = matches.Where(m =>
            m.Phase == "Group" && m.GroupNumber == groupNumber &&
            m.StartedUtc is not null && m.FinishedUtc is null).ToList();

        if (!runningMatches.Any()) return existingStandings;

        // Clone standings and add provisional wins for leading players in active matches
        var flash = existingStandings.Select(s => new GroupStandingDto(
            s.ParticipantId, s.ParticipantName, s.GroupNumber,
            s.Played, s.Won, s.Lost, s.Points, s.LegsWon, s.LegsLost, s.LegDifference)).ToList();

        foreach (var m in runningMatches)
        {
            if (m.HomeLegs == m.AwayLegs) continue; // tied — no provisional win

            var leaderId = m.HomeLegs > m.AwayLegs ? m.HomeParticipantId : m.AwayParticipantId;
            var leaderStanding = flash.FirstOrDefault(s => s.ParticipantId == leaderId);
            if (leaderStanding is null) continue;

            var idx = flash.IndexOf(leaderStanding);
            var winPts = selectedTournament?.WinPoints ?? 2;
            flash[idx] = leaderStanding with
            {
                Points = leaderStanding.Points + winPts,
                Played = leaderStanding.Played + 1,
                Won = leaderStanding.Won + 1
            };
        }

        return flash.OrderByDescending(s => s.Points).ThenByDescending(s => s.LegDifference).ToList();
    }

    // ─── Round started/completed checks ───
    private bool IsRoundStarted(string phase, int roundNumber) =>
        matches.Any(m => m.Phase == phase && m.Round == roundNumber &&
            m.HomeParticipantId != Guid.Empty && m.AwayParticipantId != Guid.Empty &&
            (m.StartedUtc is not null || m.FinishedUtc is not null));

    private bool IsRoundCompleted(string phase, int roundNumber)
    {
        var realMatches = matches.Where(m => m.Phase == phase && m.Round == roundNumber
            && m.HomeParticipantId != Guid.Empty && m.AwayParticipantId != Guid.Empty).ToList();
        return realMatches.Count > 0 && realMatches.All(m => m.FinishedUtc is not null);
    }

    private bool IsGroupCompleted(int groupNumber) =>
        matches.Where(m => m.Phase == "Group" && m.GroupNumber == groupNumber && m.HomeParticipantId != Guid.Empty && m.AwayParticipantId != Guid.Empty).All(m => m.FinishedUtc is not null)
        && matches.Any(m => m.Phase == "Group" && m.GroupNumber == groupNumber);

    /// <summary>Gets the next match info for a participant after a given match.</summary>
    private string? GetNextMatchInfo(MatchDto currentMatch, Guid participantId)
    {
        if (participantId == Guid.Empty) return null;

        // In KO: find the match in the next round where the winner goes
        if (currentMatch.Phase == "Knockout")
        {
            var koMatches = matches.Where(m => m.Phase == "Knockout").OrderBy(m => m.Round).ThenBy(m => m.MatchNumber).ToList();
            var nextRound = koMatches.Where(m => m.Round == currentMatch.Round + 1).OrderBy(m => m.MatchNumber).ToList();
            var currentRound = koMatches.Where(m => m.Round == currentMatch.Round).OrderBy(m => m.MatchNumber).ToList();
            var matchIdx = currentRound.IndexOf(currentMatch);
            if (matchIdx >= 0 && matchIdx / 2 < nextRound.Count)
            {
                var nextMatch = nextRound[matchIdx / 2];
                var opponentId = matchIdx % 2 == 0 ? nextMatch.AwayParticipantId : nextMatch.HomeParticipantId;
                var time = nextMatch.PlannedStartUtc?.LocalDateTime.ToString("HH:mm");
                if (opponentId != Guid.Empty)
                    return $"{time ?? "?"} gegen {ParticipantName(opponentId)}";

                // Opponent not yet known — show feeder match
                var feederIdx = matchIdx % 2 == 0 ? matchIdx + 1 : matchIdx - 1;
                if (feederIdx >= 0 && feederIdx < currentRound.Count)
                {
                    var feeder = currentRound[feederIdx];
                    return $"{time ?? "?"} gegen Gewinner aus {ParticipantName(feeder.HomeParticipantId)} / {ParticipantName(feeder.AwayParticipantId)}";
                }
            }
            return null;
        }

        // In Group: find next unfinished match for this participant
        var nextGroupMatch = matches
            .Where(m => m.Phase == "Group" && m.Id != currentMatch.Id && m.FinishedUtc is null
                && (m.HomeParticipantId == participantId || m.AwayParticipantId == participantId))
            .OrderBy(m => m.PlannedStartUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(m => m.Round)
            .FirstOrDefault();

        if (nextGroupMatch is not null)
        {
            var opponentId = nextGroupMatch.HomeParticipantId == participantId
                ? nextGroupMatch.AwayParticipantId
                : nextGroupMatch.HomeParticipantId;
            var time = nextGroupMatch.PlannedStartUtc?.LocalDateTime.ToString("HH:mm");
            return $"{time ?? "?"} gegen {ParticipantName(opponentId)}";
        }

        return null;
    }
}
