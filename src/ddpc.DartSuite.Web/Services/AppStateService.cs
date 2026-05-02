using ddpc.DartSuite.Application.Contracts.Autodarts;
using ddpc.DartSuite.Application.Contracts.Tournaments;

namespace ddpc.DartSuite.Web.Services;

/// <summary>Indicates how the current Autodarts session was established.</summary>
public enum AutodartsLoginSource
{
    /// <summary>No active session.</summary>
    None,
    /// <summary>Session established by the user entering credentials manually.</summary>
    Manual,
    /// <summary>Session restored automatically from stored local-browser data.</summary>
    AutoRestore,
}

public sealed class AppStateService
{
    public TournamentDto? SelectedTournament { get; private set; }
    public AutodartsProfileDto? AutodartsProfile { get; private set; }
    public bool IsAutodartsConnected { get; private set; }
    public bool IsAdmin { get; private set; }

    /// <summary>How the current Autodarts session was established.</summary>
    public AutodartsLoginSource LoginSource { get; private set; }

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

    public void SetAutodartsProfile(AutodartsProfileDto? profile, bool isConnected, AutodartsLoginSource source = AutodartsLoginSource.Manual)
    {
        AutodartsProfile = profile;
        IsAutodartsConnected = isConnected;
        LoginSource = isConnected ? source : AutodartsLoginSource.None;
        OnChange?.Invoke();
    }

    /// <summary>Update without firing OnChange (used by MainLayout to avoid re-render loops).</summary>
    public void SetAutodartsProfileSilent(AutodartsProfileDto? profile, bool isConnected, AutodartsLoginSource source = AutodartsLoginSource.Manual)
    {
        AutodartsProfile = profile;
        IsAutodartsConnected = isConnected;
        LoginSource = isConnected ? source : AutodartsLoginSource.None;
    }

    public void SetIsAdmin(bool isAdmin)
    {
        IsAdmin = isAdmin;
        OnChange?.Invoke();
    }

    /// <summary>Update without firing OnChange (used by MainLayout to avoid re-render loops).</summary>
    public void SetIsAdminSilent(bool isAdmin)
    {
        IsAdmin = isAdmin;
    }
}
