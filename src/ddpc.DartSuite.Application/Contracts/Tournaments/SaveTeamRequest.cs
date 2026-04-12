namespace ddpc.DartSuite.Application.Contracts.Tournaments;

public sealed record SaveTeamRequest(
    Guid? TeamId,
    string Name,
    IReadOnlyList<Guid> MemberParticipantIds);
