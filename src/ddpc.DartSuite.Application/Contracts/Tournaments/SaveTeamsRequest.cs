namespace ddpc.DartSuite.Application.Contracts.Tournaments;

public sealed record SaveTeamsRequest(
    Guid TournamentId,
    IReadOnlyList<SaveTeamRequest> Teams);
