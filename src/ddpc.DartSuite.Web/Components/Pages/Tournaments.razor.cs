using ddpc.DartSuite.Application.Contracts.Boards;
using ddpc.DartSuite.Application.Contracts.Matches;
using ddpc.DartSuite.Application.Contracts.Tournaments;
using ddpc.DartSuite.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace ddpc.DartSuite.Web.Components.Pages;

public partial class Tournaments : IDisposable
{
    [Inject] private DartSuiteApiService Api { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private AppStateService AppState { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "matchId")]
    public string? QueryMatchId { get; set; }

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

    // ─── Player Detail Modal ───
    private ParticipantDto? detailParticipant;
    private string playerDetailTab = "info";

    // ─── Dialog Navigation Stack ───
    private readonly Stack<Action> _modalBackStack = new();

    // ─── Game Mode Lock ───
    private bool editAreGameModesLocked;

    // ─── KO View Mode ───
    private string koViewMode = "tree"; // tree | round | live

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

    /// <summary>True if the current user is a Spielleiter for the selected tournament.</summary>
    private bool IsCurrentUserManager =>
        selectedTournament is not null && autodartsDisplayName is not null &&
        (string.Equals(selectedTournament.OrganizerAccount, autodartsDisplayName, StringComparison.OrdinalIgnoreCase)
         || participants.Any(p => p.IsManager && string.Equals(p.AccountName, autodartsDisplayName, StringComparison.OrdinalIgnoreCase))
         || participants.Any(p => p.IsManager && string.Equals(p.DisplayName, autodartsDisplayName, StringComparison.OrdinalIgnoreCase)));

    // ─── Lifecycle ───
    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(LoadTournamentsAsync(), LoadBoardsAsync(), TryLoadAutodartsSessionAsync());

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
                    break;
                }
            }
        }

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

    public void Dispose()
    {
        _autoRefreshTimer?.Dispose();
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
        selectedTournament = tournament;
        activeTab = "general";
        editError = null;
        editSuccess = null;
        participantError = null;
        detailMatch = null;
        PopulateEditFields(tournament);
        await Task.WhenAll(LoadParticipantsAsync(tournament.Id), LoadMatchesAsync(tournament.Id), LoadBoardsAsync(), LoadRoundsAsync());
        if (tournament.Mode == "GroupAndKnockout" && matches.Any(m => m.Phase == "Group"))
            groupStandings = (await Api.GetGroupStandingsAsync(tournament.Id)).ToList();
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
    }

    // ─── Save Tournament ───
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
                editRegistrationEnd.HasValue ? new DateTimeOffset(editRegistrationEnd.Value) : null));
            await LoadTournamentsAsync();
            selectedTournament = tournaments.FirstOrDefault(x => x.Id == updated.Id) ?? updated;
            PopulateEditFields(selectedTournament);
            editSuccess = "Turnier gespeichert.";
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
        => participants = (await Api.GetParticipantsAsync(tournamentId)).ToList();

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
                editPIsAutodarts, editPIsManager, editingParticipant.Seed));
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
            activeTab = "knockout";
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
                activeTab = "knockout";
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
    private void OpenMatchDetail(MatchDto match)
    {
        detailMatch = match;
        editHomeLegs = match.HomeLegs;
        editAwayLegs = match.AwayLegs;
        editHomeSets = match.HomeSets;
        editAwaySets = match.AwaySets;
        resultError = null;
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
    private void OpenBoardDetail(BoardDto board) => detailBoard = board;
    private void CloseBoardDetail()
    {
        detailBoard = null;
        if (_modalBackStack.Count > 0)
            _modalBackStack.Pop().Invoke();
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
        try
        {
            isWorking = true;
            var updated = await Api.UpdateTournamentStatusAsync(selectedTournament.Id, status);
            if (updated is not null)
            {
                selectedTournament = updated;
                matches = (await Api.GetMatchesAsync(selectedTournament.Id)).ToList();
            }
        }
        catch (Exception ex) { resultError = ex.Message; }
        finally { isWorking = false; }
    }

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
