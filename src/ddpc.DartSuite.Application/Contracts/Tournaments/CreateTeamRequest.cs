namespace ddpc.DartSuite.Application.Contracts.Tournaments;

public sealed record CreateTeamRequest(
    Guid TournamentId,
    string Name,
    IReadOnlyList<Guid> MemberParticipantIds);
