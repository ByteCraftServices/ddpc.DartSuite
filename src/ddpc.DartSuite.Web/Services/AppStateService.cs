using ddpc.DartSuite.Application.Contracts.Autodarts;
using ddpc.DartSuite.Application.Contracts.Tournaments;

namespace ddpc.DartSuite.Web.Services;

public sealed class AppStateService
{
    public TournamentDto? SelectedTournament { get; private set; }
    public AutodartsProfileDto? AutodartsProfile { get; private set; }
    public bool IsAutodartsConnected { get; private set; }

    public event Action? OnChange;

    public void SetSelectedTournament(TournamentDto? tournament)
    {
        SelectedTournament = tournament;
        OnChange?.Invoke();
    }

    /// <summary>Update without firing OnChange (used by MainLayout to avoid re-render loops).</summary>
    public void SetSelectedTournamentSilent(TournamentDto? tournament)
    {
        SelectedTournament = tournament;
    }

    public void SetAutodartsProfile(AutodartsProfileDto? profile, bool isConnected)
    {
        AutodartsProfile = profile;
        IsAutodartsConnected = isConnected;
        OnChange?.Invoke();
    }

    /// <summary>Update without firing OnChange (used by MainLayout to avoid re-render loops).</summary>
    public void SetAutodartsProfileSilent(AutodartsProfileDto? profile, bool isConnected)
    {
        AutodartsProfile = profile;
        IsAutodartsConnected = isConnected;
    }
}
