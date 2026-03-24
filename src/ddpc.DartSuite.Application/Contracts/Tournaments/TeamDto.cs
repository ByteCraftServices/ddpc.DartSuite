namespace ddpc.DartSuite.Application.Contracts.Tournaments;

public sealed record TeamDto(
    Guid Id,
    Guid TournamentId,
    string Name,
    int? GroupNumber,
    IReadOnlyList<ParticipantDto> Members);
