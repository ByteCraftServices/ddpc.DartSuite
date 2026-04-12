namespace ddpc.DartSuite.Application.Contracts.Tournaments;

public sealed record UpdateParticipantRequest(
    Guid TournamentId,
    Guid ParticipantId,
    string DisplayName,
    string AccountName,
    bool IsAutodartsAccount,
    bool IsManager,
    int Seed,
    int SeedPot = 0,
    int? GroupNumber = null,
    string? Type = null);
