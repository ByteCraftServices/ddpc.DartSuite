namespace ddpc.DartSuite.Application.Contracts.Tournaments;

public sealed record TournamentRoundDto(
    Guid Id,
    Guid TournamentId,
    string Phase,
    int RoundNumber,
    int BaseScore,
    string InMode,
    string OutMode,
    string GameMode,
    int Legs,
    int? Sets,
    int MaxRounds,
    string BullMode,
    string BullOffMode,
    int MatchDurationMinutes,
    int PauseBetweenMatchesMinutes,
    int MinPlayerPauseMinutes,
    string BoardAssignment,
    Guid? FixedBoardId);
