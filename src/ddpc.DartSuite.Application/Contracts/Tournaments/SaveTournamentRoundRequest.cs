namespace ddpc.DartSuite.Application.Contracts.Tournaments;

public sealed record SaveTournamentRoundRequest(
    Guid TournamentId,
    string Phase,
    int RoundNumber,
    int BaseScore = 501,
    string InMode = "Straight",
    string OutMode = "Double",
    string GameMode = "Legs",
    int Legs = 3,
    int? Sets = null,
    int MaxRounds = 50,
    string BullMode = "25/50",
    string BullOffMode = "Normal",
    int MatchDurationMinutes = 0,
    int PauseBetweenMatchesMinutes = 0,
    int MinPlayerPauseMinutes = 0,
    string BoardAssignment = "Dynamic",
    Guid? FixedBoardId = null);
