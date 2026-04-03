using ddpc.DartSuite.Application.Contracts.Boards;
using ddpc.DartSuite.Application.Contracts.Matches;
using ddpc.DartSuite.Application.Contracts.Tournaments;
using ddpc.DartSuite.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Hosting;
using Microsoft.JSInterop;
using System.Diagnostics;

namespace ddpc.DartSuite.Web.Components.Pages;

public partial class Tournaments : IAsyncDisposable
{
    [Inject] private DartSuiteApiService Api { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private AppStateService AppState { get; set; } = default!;
    [Inject] private TournamentHubService HubService { get; set; } = default!;
    [Inject] private IWebHostEnvironment HostEnvironment { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "matchId")]
    public string? QueryMatchId { get; set; }

    [SupplyParameterFromQuery(Name = "boardId")]
    public string? QueryBoardId { get; set; }

    [SupplyParameterFromQuery(Name = "tab")]
    public string? QueryTab { get; set; }

    private Timer? _autoRefreshTimer;

    // ─── Data State ───
    private List<TournamentDto> tournaments = [];
    private List<BoardDto> boards = [];
    private List<ParticipantDto> participants = [];
    private List<MatchDto> matches = [];
    private List<TournamentRoundDto> roundSettings = [];
    private List<GroupStandingDto> groupStandings = [];
    private TournamentDto? selectedTournament;
    private string activeTab = "general";
    private bool isWorking;
    private bool showStatusDropdown;

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

    // ─── Match Detail / Result Edit Modal ───
    private MatchDto? detailMatch;
    private int editHomeLegs;
    private int editAwayLegs;
    private int editHomeSets;
    private int editAwaySets;
    private string? resultError;
    private bool isSyncing;
    private bool detailMatchOpenedFromSchedule;

    // ─── Match Listeners ───
    private List<MatchListenerInfoDto> matchListeners = [];

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
    private string confirmationMessage = string.Empty;
    private Func<Task>? confirmationAction;

    // ─── Spielplan: Collapsed Groups ───
    private bool showGroupMatches;

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

    // ─── Blitztabelle ───
    private bool showFlashTable;

    // ─── Match Schedule Editing ───
    private Guid? editingMatchTimeId;
    private string editMatchTimeValue = string.Empty;

    // ─── Round Detail Modal ───
    private TournamentRoundDto? detailRound;

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

    // ─── Autodarts Session ───
    private bool isAutodartsConnected;
    private string? autodartsDisplayName;
    private bool IsDevelopmentEnvironment => HostEnvironment.IsDevelopment();

    /// <summary>True if the current user is a Spielleiter for the selected tournament.</summary>
    private bool IsCurrentUserManager =>
        selectedTournament is not null && autodartsDisplayName is not null &&
        (string.Equals(selectedTournament.OrganizerAccount, autodartsDisplayName, StringComparison.OrdinalIgnoreCase)
         || participants.Any(p => p.IsManager && string.Equals(p.AccountName, autodartsDisplayName, StringComparison.OrdinalIgnoreCase))
         || participants.Any(p => p.IsManager && string.Equals(p.DisplayName, autodartsDisplayName, StringComparison.OrdinalIgnoreCase)));

    private bool CanEditTournamentSettings => selectedTournament is not null && !selectedTournament.IsLocked && IsCurrentUserManager;

    /// <summary>Managers can edit basic settings while tournament is unlocked.</summary>
    private bool CanEditBasicSettings => CanEditTournamentSettings;

    /// <summary>Can edit participant-related settings (teamplay, seeding, registration): Status ≤ Geplant AND no plan.</summary>
    private bool CanEditParticipantSettings =>
        CanEditBasicSettings && !matches.Any();

    /// <summary>Can edit draw/group config until the group phase has started.</summary>
    private bool CanEditGroupConfig =>
        CanEditTournamentSettings && !matches.Any(m => m.Phase == "Group" && m.StartedUtc is not null);

    /// <summary>Can edit draw mode until the group phase has started.</summary>
    private bool CanEditDrawMode =>
        CanEditTournamentSettings && !matches.Any(m => m.Phase == "Group" && m.StartedUtc is not null);

    /// <summary>Can edit scoring: while group phase not started.</summary>
    private bool CanEditScoring =>
        CanEditTournamentSettings && !matches.Any(m => m.Phase == "Group" && m.StartedUtc is not null);

    /// <summary>Returns true if any participant has been assigned to a group.</summary>
    private bool HasDrawAssignments() => participants.Any(p => p.GroupNumber.HasValue && p.GroupNumber > 0);

    /// <summary>Returns unassigned participants (no group number set).</summary>
    private List<ParticipantDto> UnassignedParticipants =>
        participants.Where(p => !p.GroupNumber.HasValue || p.GroupNumber == 0).ToList();

    /// <summary>Returns participants assigned to a specific group number.</summary>
    private List<ParticipantDto> GroupParticipants(int groupNumber) =>
        participants.Where(p => p.GroupNumber == groupNumber).OrderBy(p => p.Seed).ToList();

    /// <summary>Ideal group size for even distribution.</summary>
    private int IdealGroupSize => editGroupCount > 0 ? (int)Math.Ceiling((double)participants.Count / editGroupCount) : participants.Count;

    // ─── Drag & Drop: Draw (Participant → Group) ───
    private Guid? draggedParticipantId;
    private int? dropTargetGroupNumber;

    // ─── Seeding Drag ───
    private int? draggedSeedIndex;

    // ─── Draw Animation ───
    private string drawAnimationMode = "Off"; // Off | Exciting | Moderate
    private bool isDrawAnimating;
    private Guid? drawCandidateParticipantId;
    private Guid? drawWinnerParticipantId;
    private int? drawHighlightedGroupNumber;
    private int? drawSourcePotNumber;
    private int? drawHighlightedKoMatchNumber;
    private bool? drawHighlightedKoHomeSlot;
    private bool showKoDrawToken;
    private string koDrawTokenStyle = string.Empty;
    private const string KoDrawContainerId = "ko-draw-grid";

    // ─── Knockout Draw (UI-only staging before plan generation) ───
    private sealed class KnockoutDrawCard
    {
        public int MatchNumber { get; init; }
        public Guid? HomeParticipantId { get; set; }
        public Guid? AwayParticipantId { get; set; }
    }

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
        => editSeedingEnabled && (editSeedTopCount <= 0 || editSeedTopCount > participants.Count);

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
    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(LoadTournamentsAsync(), LoadBoardsAsync(), TryLoadAutodartsSessionAsync());

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
            activeTab = QueryTab;
            if (selectedTournament is null && AppState.SelectedTournament is not null)
            {
                var t = tournaments.FirstOrDefault(t => t.Id == AppState.SelectedTournament.Id);
                if (t is not null)
                    await SelectTournamentAsync(t);
            }
        }

        _autoRefreshTimer = new Timer(OnAutoRefresh, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

        // Connect to SignalR hub
        await ConnectToHubAsync();
    }

    private async Task TryOpenBoardDetailFromQueryAsync()
    {
        if (string.IsNullOrWhiteSpace(QueryBoardId) || !Guid.TryParse(QueryBoardId, out var boardId))
            return;

        var board = boards.FirstOrDefault(b => b.Id == boardId);
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
            HubService.OnMatchUpdated += OnHubMatchUpdated;
            HubService.OnBoardsUpdated += OnHubBoardsUpdated;
            HubService.OnParticipantsUpdated += OnHubParticipantsUpdated;
            HubService.OnTournamentUpdated += OnHubTournamentUpdated;
            HubService.OnScheduleUpdated += OnHubScheduleUpdated;
            HubService.OnReconnected += OnHubReconnected;
            await HubService.StartAsync();

            // If tournament already selected, join the group
            if (selectedTournament is not null)
                await HubService.JoinTournamentAsync(selectedTournament.Id.ToString());
        }
        catch { /* Hub connection is optional — timer fallback handles it */ }
    }

    private async Task OnHubMatchUpdated(string tournamentId)
    {
        if (selectedTournament is null || selectedTournament.Id.ToString() != tournamentId) return;
        await InvokeAsync(async () =>
        {
            matches = (await Api.GetMatchesAsync(selectedTournament.Id)).ToList();
            if (activeTab == "groups")
                groupStandings = (await Api.GetGroupStandingsAsync(selectedTournament.Id)).ToList();
            StateHasChanged();
        });
    }

    private async Task OnHubBoardsUpdated(string _)
    {
        await InvokeAsync(async () =>
        {
            await LoadBoardsAsync();
            StateHasChanged();
        });
    }

    private async Task OnHubParticipantsUpdated(string tournamentId)
    {
        if (selectedTournament is null || selectedTournament.Id.ToString() != tournamentId) return;
        await InvokeAsync(async () =>
        {
            await LoadParticipantsAsync(selectedTournament.Id);
            StateHasChanged();
        });
    }

    private async Task OnHubTournamentUpdated(string tournamentId)
    {
        if (selectedTournament is null || selectedTournament.Id.ToString() != tournamentId) return;
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
        await InvokeAsync(async () =>
        {
            matches = (await Api.GetMatchesAsync(selectedTournament.Id)).ToList();
            StateHasChanged();
        });
    }

    private async Task OnHubReconnected()
    {
        // Re-join tournament group after reconnection
        if (selectedTournament is not null)
            await HubService.JoinTournamentAsync(selectedTournament.Id.ToString());
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
        try
        {
            if (selectedTournament is null) return;
            await InvokeAsync(async () =>
            {
                var prevBoardCount = boards.Count;
                var prevParticipantCount = participants.Count;
                await Task.WhenAll(LoadBoardsAsync(), LoadParticipantsAsync(selectedTournament.Id), LoadMatchListenersAsync());

                // Auto-sync live match data if detail modal is open with a running external match
                if (detailMatch is not null && !string.IsNullOrEmpty(detailMatch.ExternalMatchId) && detailMatch.FinishedUtc is null && !isSyncing)
                {
                    // Only sync manually if no listener is handling it
                    var hasListener = matchListeners.Any(l => l.MatchId == detailMatch.Id && l.IsRunning);
                    if (!hasListener)
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
                            }
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
                        }
                        catch { /* silent */ }
                    }
                }

                if (boards.Count != prevBoardCount || participants.Count != prevParticipantCount)
                    StateHasChanged();
                else if (detailMatch is not null && !string.IsNullOrEmpty(detailMatch.ExternalMatchId))
                    StateHasChanged();
            });
        }
        catch { /* suppress — component may be disposed */ }
    }

    public async ValueTask DisposeAsync()
    {
        _autoRefreshTimer?.Dispose();

        HubService.OnMatchUpdated -= OnHubMatchUpdated;
        HubService.OnBoardsUpdated -= OnHubBoardsUpdated;
        HubService.OnParticipantsUpdated -= OnHubParticipantsUpdated;
        HubService.OnTournamentUpdated -= OnHubTournamentUpdated;
        HubService.OnScheduleUpdated -= OnHubScheduleUpdated;
        HubService.OnReconnected -= OnHubReconnected;

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

    private async Task LoadTournamentsAsync()
        => tournaments = (await Api.GetTournamentsAsync()).ToList();

    private async Task LoadBoardsAsync()
        => boards = (await Api.GetBoardsAsync()).ToList();

    private async Task LoadMatchListenersAsync()
    {
        try { matchListeners = (await Api.GetMatchListenersAsync()).ToList(); }
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
        // Leave previous tournament hub group
        if (selectedTournament is not null && selectedTournament.Id != tournament.Id)
        {
            try { await HubService.LeaveTournamentAsync(selectedTournament.Id.ToString()); }
            catch { /* suppress */ }
        }

        selectedTournament = tournament;
        activeTab = "general";
        editError = null;
        editSuccess = null;
        participantError = null;
        detailMatch = null;
        PopulateEditFields(tournament);
        await Task.WhenAll(LoadParticipantsAsync(tournament.Id), LoadMatchesAsync(tournament.Id), LoadBoardsAsync(), LoadRoundsAsync());
        if (tournament.Mode == "Knockout")
            EnsureKnockoutDrawCards();
        if (tournament.Mode == "GroupAndKnockout" && matches.Any(m => m.Phase == "Group"))
            groupStandings = (await Api.GetGroupStandingsAsync(tournament.Id)).ToList();

        // Join the new tournament hub group
        try { await HubService.JoinTournamentAsync(tournament.Id.ToString()); }
        catch { /* suppress */ }
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

        if (reasons.Count > 0 && matches.Any())
        {
            confirmationMessage = $"Die Änderung von {string.Join(", ", reasons)} wirkt sich auf den bestehenden Turnierplan aus.";
            confirmationAction = ExecuteSaveTournamentAsync;
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

    private async Task OnSeedingEnabledChangedAsync()
    {
        await AutoSaveSettingAsync();
        if (editSeedingEnabled)
            await NormalizeSeedRanksAsync();
    }

    private async Task OnSeedTopCountChangedAsync()
    {
        await AutoSaveSettingAsync();
        await NormalizeSeedRanksAsync();
    }

    private async Task ExecuteSaveTournamentAsync()
    {
        if (selectedTournament is null) return;
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
        }
        catch (Exception ex) { editError = ex.Message; }
        finally { isWorking = false; }
    }

    // ─── Confirmation Dialog ───
    private async Task AcceptConfirmation()
    {
        showConfirmation = false;
        if (confirmationAction is not null) await confirmationAction();
        confirmationAction = null;
    }

    private void RejectConfirmation()
    {
        showConfirmation = false;
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
        if (selectedTournament?.Mode == "Knockout")
            EnsureKnockoutDrawCards();
        CleanupKnockoutDrawCards();
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
        if (matches.Any())
        {
            confirmationMessage = $"Durch das Entfernen von \"{p.DisplayName}\" wird der bestehende Turnierplan ungültig.";
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

    // ─── Board Assignment (Drag & Drop) ───
    private async Task AssignBoardToMatchAsync(Guid matchId)
    {
        if (draggedBoardId is null) return;
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
        activeTab = "rounds";
        await LoadRoundsAsync();
    }

    private async Task LoadRoundsAsync()
    {
        if (selectedTournament is null) return;
        roundSettings = (await Api.GetRoundsAsync(selectedTournament.Id)).ToList();
    }

    private async Task SaveRoundAsync()
    {
        if (selectedTournament is null) return;
        try
        {
            isWorking = true;
            roundError = null;
            ParseBoardAssignment(out var assignment, out var fixedId);
            await Api.SaveRoundAsync(selectedTournament.Id, new SaveTournamentRoundRequest(
                selectedTournament.Id, newRoundPhase, newRoundNumber,
                newRoundBaseScore, newRoundInMode, newRoundOutMode, newRoundGameMode, newRoundLegs, newRoundSets, newRoundMaxRounds, newRoundBullMode, newRoundBullOffMode,
                newRoundDuration, newRoundPause, newRoundPlayerPause, assignment, fixedId));
            await LoadRoundsAsync();
        }
        catch (Exception ex) { roundError = ex.Message; }
        finally { isWorking = false; }
    }

    private async Task ApplyRoundToAllAsync()
    {
        if (selectedTournament is null) return;
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
        editHomeLegs = match.HomeLegs;
        editAwayLegs = match.AwayLegs;
        editHomeSets = match.HomeSets;
        editAwaySets = match.AwaySets;
        resultError = null;
        detailMatchStatistics = [];
        // Fire-and-forget loads for statistics and follow state
        _ = LoadMatchStatisticsAsync(match.Id);
        _ = CheckFollowStateAsync(match.Id);
    }

    private void CloseMatchDetail()
    {
        detailMatch = null;
        detailMatchOpenedFromSchedule = false;
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
            var label = $"{ParticipantName(match.HomeParticipantId)} vs {ParticipantName(match.AwayParticipantId)}";
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
        if (detailMatch is null || string.IsNullOrEmpty(autodartsDisplayName)) return;
        try
        {
            if (isFollowingDetailMatch)
            {
                await Api.UnfollowMatchAsync(detailMatch.Id, autodartsDisplayName);
                isFollowingDetailMatch = false;
            }
            else
            {
                await Api.FollowMatchAsync(detailMatch.Id, autodartsDisplayName);
                isFollowingDetailMatch = true;
            }
        }
        catch { /* silent */ }
    }

    private async Task CheckFollowStateAsync(Guid matchId)
    {
        if (string.IsNullOrEmpty(autodartsDisplayName)) { isFollowingDetailMatch = false; return; }
        try
        {
            var followers = await Api.GetMatchFollowersAsync(matchId);
            isFollowingDetailMatch = followers.Any(f =>
                string.Equals(f.UserAccountName, autodartsDisplayName, StringComparison.OrdinalIgnoreCase));
        }
        catch { isFollowingDetailMatch = false; }
    }

    /// <summary>Returns all matches chronologically sorted for Spielplan, with filters applied.</summary>
    private List<MatchDto> ScheduledMatches
    {
        get
        {
            var query = matches
                .Where(m => m.HomeParticipantId != Guid.Empty && m.AwayParticipantId != Guid.Empty);

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

    private string ParticipantName(Guid? id) =>
        id.HasValue ? participants.FirstOrDefault(p => p.Id == id.Value)?.DisplayName?.ToUpperInvariant() ?? "?" : "?";

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

        if (IsDevelopmentEnvironment)
            _ = LoadBoardSyncDebugAsync(board.Id);
    }

    private async Task RequestBoardSyncAsync(BoardDto board)
    {
        try
        {
            isWorking = true;
            boardSyncError = null;
            boardSyncInfo = null;
            var accepted = await Api.RequestBoardExtensionSyncAsync(board.Id);
            boardSyncInfo = $"Sync angefordert (RequestId: {accepted.RequestId}). Die Extension meldet den aktuellen Match-Kontext in den naechsten Sekunden.";

            if (IsDevelopmentEnvironment)
                await PollBoardSyncDebugAsync(board.Id, accepted.RequestId);
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

    private async Task PollBoardSyncDebugAsync(Guid boardId, Guid requestId)
    {
        for (var i = 0; i < 8; i++)
        {
            await LoadBoardSyncDebugAsync(boardId);

            if (boardSyncDebug is not null
                && boardSyncDebug.RequestId == requestId
                && boardSyncDebug.ReportedAtUtc.HasValue)
                return;

            await Task.Delay(1000);
        }
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
    private void OpenRoundDetail(TournamentRoundDto round)
    {
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
        newRoundDuration = round.MatchDurationMinutes;
        newRoundPause = round.PauseBetweenMatchesMinutes;
        newRoundPlayerPause = round.MinPlayerPauseMinutes;
        newRoundBoardAssignment = round.BoardAssignment == "Fixed" && round.FixedBoardId is not null
            ? $"Fixed:{round.FixedBoardId}" : round.BoardAssignment;
    }

    private void CloseRoundDetail() => detailRound = null;

    private async Task DeleteRoundAsync()
    {
        if (detailRound is null || selectedTournament is null) return;
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

    private async Task ExecuteUpdateStatusAsync(string status)
    {
        if (selectedTournament is null) return;
        try
        {
            isWorking = true;
            var updated = await Api.UpdateTournamentStatusAsync(selectedTournament.Id, status);
            if (updated is not null)
            {
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

    // ─── Draw: Assign Participant to Group ───
    private async Task AssignParticipantToGroupAsync(int groupNumber)
    {
        if (selectedTournament is null || draggedParticipantId is null) return;
        var participantId = draggedParticipantId.Value;
        draggedParticipantId = null;
        dropTargetGroupNumber = null;

        var participant = participants.FirstOrDefault(p => p.Id == participantId);
        if (participant is null) return;

        try
        {
            isWorking = true;
            await Api.UpdateParticipantAsync(selectedTournament.Id, new UpdateParticipantRequest(
                selectedTournament.Id, participantId, participant.DisplayName, participant.AccountName,
                participant.IsAutodartsAccount, participant.IsManager, participant.Seed, participant.SeedPot,
                groupNumber));
            await LoadParticipantsAsync(selectedTournament.Id);
        }
        catch (Exception ex) { editError = ex.Message; }
        finally { isWorking = false; }
    }

    private async Task AssignParticipantToGroupDirectAsync(Guid participantId, int groupNumber)
    {
        if (selectedTournament is null) return;
        var participant = participants.FirstOrDefault(p => p.Id == participantId);
        if (participant is null) return;

        try
        {
            isWorking = true;
            await Api.UpdateParticipantAsync(selectedTournament.Id, new UpdateParticipantRequest(
                selectedTournament.Id, participantId, participant.DisplayName, participant.AccountName,
                participant.IsAutodartsAccount, participant.IsManager, participant.Seed, participant.SeedPot,
                groupNumber));
            await LoadParticipantsAsync(selectedTournament.Id);
        }
        catch (Exception ex) { editError = ex.Message; }
        finally { isWorking = false; }
    }

    /// <summary>Remove a participant from their group (back to unassigned).</summary>
    private async Task UnassignParticipantFromGroupAsync(Guid participantId)
    {
        if (selectedTournament is null) return;
        var participant = participants.FirstOrDefault(p => p.Id == participantId);
        if (participant is null) return;

        try
        {
            isWorking = true;
            await Api.UpdateParticipantAsync(selectedTournament.Id, new UpdateParticipantRequest(
                selectedTournament.Id, participantId, participant.DisplayName, participant.AccountName,
                participant.IsAutodartsAccount, participant.IsManager, participant.Seed, participant.SeedPot,
                null));
            await LoadParticipantsAsync(selectedTournament.Id);
        }
        catch (Exception ex) { editError = ex.Message; }
        finally { isWorking = false; }
    }

    /// <summary>Reset all group assignments (participants go back to unassigned).</summary>
    private async Task ResetDrawAsync()
    {
        if (selectedTournament is null) return;
        confirmationMessage = "Alle Gruppenzuteilungen werden zurückgesetzt. Wirklich fortfahren?";
        confirmationAction = async () =>
        {
            try
            {
                isWorking = true;
                foreach (var p in participants.Where(p => p.GroupNumber.HasValue && p.GroupNumber > 0))
                {
                    await Api.UpdateParticipantAsync(selectedTournament.Id, new UpdateParticipantRequest(
                        selectedTournament.Id, p.Id, p.DisplayName, p.AccountName,
                        p.IsAutodartsAccount, p.IsManager, p.Seed, 0, null));
                }
                await LoadParticipantsAsync(selectedTournament.Id);
            }
            catch (Exception ex) { editError = ex.Message; }
            finally { isWorking = false; }
        };
        showConfirmation = true;
    }

    /// <summary>Assign seed pots based on seeding list, then auto-distribute to groups.</summary>
    private async Task AssignSeedPotsAndDistributeAsync()
    {
        if (selectedTournament is null) return;
        try
        {
            isWorking = true;
            var updatedParticipants = await Api.AssignSeedPotsAsync(selectedTournament.Id);
            participants = updatedParticipants.ToList();
        }
        catch (Exception ex) { editError = ex.Message; }
        finally { isWorking = false; }
    }

    private sealed record DrawStep(Guid ParticipantId, int TargetGroup, int SourcePot);

    private string DrawParticipantAnimationCss(Guid participantId)
    {
        if (drawWinnerParticipantId == participantId) return "draw-item-winner";
        if (drawCandidateParticipantId == participantId) return "draw-item-candidate";
        return string.Empty;
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
        var assigned = participants.Where(p => p.GroupNumber.HasValue && p.GroupNumber > 0).ToList();
        foreach (var p in assigned)
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

        var pots = UnassignedParticipants
            .Where(p => p.SeedPot > 0)
            .GroupBy(p => p.SeedPot)
            .OrderBy(g => g.Key)
            .ToList();

        foreach (var pot in pots)
        {
            var remaining = pot.OrderBy(_ => Random.Shared.Next()).ToList();
            for (var group = 1; group <= editGroupCount && remaining.Count > 0; group++)
            {
                var drawIndex = Random.Shared.Next(remaining.Count);
                var picked = remaining[drawIndex];
                remaining.RemoveAt(drawIndex);
                steps.Add(new DrawStep(picked.Id, group, pot.Key));
            }
        }

        return steps;
    }

    private List<DrawStep> BuildDrawPlan()
        => editGroupDrawMode == "SeededPots"
            ? BuildSeededPotsDrawPlan()
            : BuildRandomDrawPlan();

    private int KnockoutBracketSize
    {
        get
        {
            var size = 1;
            while (size < participants.Count) size *= 2;
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
        var validIds = participants.Select(p => p.Id).ToHashSet();
        foreach (var card in knockoutDrawCards)
        {
            if (card.HomeParticipantId.HasValue && !validIds.Contains(card.HomeParticipantId.Value))
                card.HomeParticipantId = null;
            if (card.AwayParticipantId.HasValue && !validIds.Contains(card.AwayParticipantId.Value))
                card.AwayParticipantId = null;
        }
    }

    private bool IsKnockoutDrawComplete => KnockoutAssignedParticipants.Count == participants.Count;

    private List<ParticipantDto> KnockoutAssignedParticipants
    {
        get
        {
            var ids = knockoutDrawCards
                .SelectMany(c => new[] { c.HomeParticipantId, c.AwayParticipantId })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();
            return participants.Where(p => ids.Contains(p.Id)).ToList();
        }
    }

    private List<ParticipantDto> KnockoutUnassignedParticipants
    {
        get
        {
            var assignedIds = KnockoutAssignedParticipants.Select(p => p.Id).ToHashSet();
            return participants
                .Where(p => !assignedIds.Contains(p.Id))
                .OrderBy(p => p.Seed > 0 ? p.Seed : int.MaxValue)
                .ThenBy(p => p.DisplayName)
                .ToList();
        }
    }

    private ParticipantDto? FindParticipant(Guid? id)
        => id.HasValue ? participants.FirstOrDefault(p => p.Id == id.Value) : null;

    private void StartKnockoutDrag(Guid participantId)
    {
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
        if (!draggedKnockoutParticipantId.HasValue) return;
        AssignKnockoutCardSlot(matchNumber, isHomeSlot, draggedKnockoutParticipantId.Value);
        draggedKnockoutParticipantId = null;
    }

    private void ClearKnockoutCardSlot(int matchNumber, bool isHomeSlot)
    {
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
            var ranked = participants
                .Where(p => p.Seed > 0 && p.Seed <= editSeedTopCount)
                .OrderBy(p => p.Seed)
                .ToList();
            var unranked = participants
                .Except(ranked)
                .OrderBy(_ => Random.Shared.Next())
                .ToList();
            ordered = ranked.Concat(unranked).ToList();
        }
        else
        {
            ordered = participants.OrderBy(_ => Random.Shared.Next()).ToList();
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
                    await Task.Delay(1300);

                    AssignKnockoutCardSlot(step.MatchNumber, step.IsHomeSlot, step.ParticipantId);
                    drawWinnerParticipantId = step.ParticipantId;
                    await InvokeAsync(StateHasChanged);
                    await Task.Delay(1300);
                    drawWinnerParticipantId = null;
                }
                else if (drawAnimationMode == "Exciting")
                {
                    drawCandidateParticipantId = step.ParticipantId;

                    var hops = FreeKnockoutSlots();
                    hops = hops.OrderBy(_ => Random.Shared.Next()).ToList();
                    foreach (var hop in hops.Take(Math.Min(8, hops.Count)))
                    {
                        drawHighlightedKoMatchNumber = hop.MatchNumber;
                        drawHighlightedKoHomeSlot = hop.IsHomeSlot;
                        await MoveKoDrawTokenToSlotAsync(hop.MatchNumber, hop.IsHomeSlot);
                        await InvokeAsync(StateHasChanged);
                        await Task.Delay(156);
                    }

                    drawHighlightedKoMatchNumber = step.MatchNumber;
                    drawHighlightedKoHomeSlot = step.IsHomeSlot;
                    drawWinnerParticipantId = step.ParticipantId;
                    await MoveKoDrawTokenToSlotAsync(step.MatchNumber, step.IsHomeSlot);
                    await InvokeAsync(StateHasChanged);
                    await Task.Delay(1170);

                    AssignKnockoutCardSlot(step.MatchNumber, step.IsHomeSlot, step.ParticipantId);
                    await InvokeAsync(StateHasChanged);
                    await Task.Delay(420);
                    drawWinnerParticipantId = null;
                }
                else
                {
                    AssignKnockoutCardSlot(step.MatchNumber, step.IsHomeSlot, step.ParticipantId);
                }

                drawCandidateParticipantId = null;
                drawHighlightedKoMatchNumber = null;
                drawHighlightedKoHomeSlot = null;
                HideKoDrawToken();
                await InvokeAsync(StateHasChanged);
            }
        }
        finally
        {
            isDrawAnimating = false;
            drawCandidateParticipantId = null;
            drawWinnerParticipantId = null;
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

    private static int ComputeExcitingDurationMs(int candidateCount)
    {
        // Scale suspense with remaining candidates, max 5 seconds.
        var scaled = Math.Max(1560, candidateCount * 286);
        return Math.Min(5000, scaled);
    }

    private async Task ApplyDrawStepAsync(DrawStep step)
    {
        if (selectedTournament is null) return;
        var participant = participants.FirstOrDefault(p => p.Id == step.ParticipantId);
        if (participant is null) return;

        var updated = await Api.UpdateParticipantAsync(selectedTournament.Id, new UpdateParticipantRequest(
            selectedTournament.Id, participant.Id, participant.DisplayName, participant.AccountName,
            participant.IsAutodartsAccount, participant.IsManager, participant.Seed, participant.SeedPot, step.TargetGroup));
        ReplaceParticipant(updated);
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
                    await Task.Delay(1300);

                    drawCandidateParticipantId = null;
                    await ApplyDrawStepAsync(step);

                    drawWinnerParticipantId = step.ParticipantId;
                    await InvokeAsync(StateHasChanged);
                    await Task.Delay(1300);
                    drawWinnerParticipantId = null;
                }
                else if (drawAnimationMode == "Exciting")
                {
                    var candidates = GetAnimationSourceCandidates(step.SourcePot);
                    var durationMs = ComputeExcitingDurationMs(candidates.Count);
                    var timer = Stopwatch.StartNew();
                    while (timer.ElapsedMilliseconds < durationMs)
                    {
                        candidates = GetAnimationSourceCandidates(step.SourcePot);
                        if (candidates.Count <= 1) break;
                        drawCandidateParticipantId = candidates[Random.Shared.Next(candidates.Count)].Id;
                        await InvokeAsync(StateHasChanged);
                        await Task.Delay(156);
                    }

                    drawCandidateParticipantId = null;
                    drawWinnerParticipantId = step.ParticipantId;
                    await InvokeAsync(StateHasChanged);
                    await Task.Delay(1170);

                    await ApplyDrawStepAsync(step);
                    await InvokeAsync(StateHasChanged);
                    await Task.Delay(420);
                    drawWinnerParticipantId = null;
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
            drawHighlightedGroupNumber = null;
            drawSourcePotNumber = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>Auto-draw participants into groups using selected mode and optional animation.</summary>
    private async Task AutoDrawAsync()
    {
        if (selectedTournament is null || editGroupCount < 1) return;

        if (selectedTournament.Mode == "Knockout")
        {
            try
            {
                isWorking = true;
                await AutoDrawKnockoutAsync();
            }
            catch (Exception ex) { editError = ex.Message; }
            finally { isWorking = false; }
            return;
        }

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

            if (drawAnimationMode == "Off")
            {
                foreach (var step in drawPlan)
                    await ApplyDrawStepAsync(step);
            }
            else
            {
                await AnimateDrawStepsAsync(drawPlan);
            }

            await LoadParticipantsAsync(selectedTournament.Id);
        }
        catch (Exception ex) { editError = ex.Message; }
        finally { isWorking = false; }
    }

    /// <summary>Delete tournament plan (matches only, keep group assignments).</summary>
    private async Task DeleteTournamentPlanAsync()
    {
        if (selectedTournament is null) return;
        confirmationMessage = "Alle Matches werden gelöscht. Die Gruppeneinteilung bleibt bestehen. Wirklich fortfahren?";
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
            }
            catch (Exception ex) { editError = ex.Message; }
            finally { isWorking = false; }
        };
        showConfirmation = true;
    }

    /// <summary>Create the tournament plan (finalize draw).</summary>
    private async Task CreateTournamentPlanAsync()
    {
        if (selectedTournament is null) return;
        try
        {
            isWorking = true;
            if (selectedTournament.Mode == "GroupAndKnockout")
            {
                await Api.GenerateGroupMatchesAsync(selectedTournament.Id);
                matches = (await Api.GetMatchesAsync(selectedTournament.Id)).ToList();
                groupStandings = (await Api.GetGroupStandingsAsync(selectedTournament.Id)).ToList();
                activeTab = "groups";
            }
            else
            {
                await ApplyKnockoutDrawCardsToSeedsAsync();
                matches = (await Api.GenerateMatchesAsync(selectedTournament.Id)).ToList();
                activeTab = "knockout";
            }
        }
        catch (Exception ex) { editError = ex.Message; }
        finally { isWorking = false; }
    }

    private async Task NormalizeSeedRanksAsync()
    {
        if (selectedTournament is null || !editSeedingEnabled) return;

        var targetCount = Math.Min(editSeedTopCount, participants.Count);

        var ordered = participants
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

    // ─── Seeding: Update participant seed via drag & drop ───
    private async Task UpdateParticipantSeedAsync(Guid participantId, int newSeed)
    {
        if (selectedTournament is null) return;
        var participant = participants.FirstOrDefault(p => p.Id == participantId);
        if (participant is null) return;
        try
        {
            isWorking = true;
            await Api.UpdateParticipantAsync(selectedTournament.Id, new UpdateParticipantRequest(
                selectedTournament.Id, participantId, participant.DisplayName, participant.AccountName,
                participant.IsAutodartsAccount, participant.IsManager, newSeed, participant.SeedPot, participant.GroupNumber));
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
