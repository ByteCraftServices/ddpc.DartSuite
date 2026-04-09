namespace ddpc.DartSuite.Application.Contracts.Tournaments;

public sealed record AddParticipantRequest(
    Guid TournamentId,
    string DisplayName,
    string AccountName,
    bool IsAutodartsAccount,
    bool IsManager,
    int Seed,
    string? Type = null);