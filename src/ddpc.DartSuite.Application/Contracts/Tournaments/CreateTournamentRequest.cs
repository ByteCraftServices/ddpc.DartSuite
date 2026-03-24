namespace ddpc.DartSuite.Application.Contracts.Tournaments;

public sealed record CreateTournamentRequest(
    string Name,
    string OrganizerAccount,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool TeamplayEnabled,
    string Mode,
    string Variant,
    string? StartTime = null);