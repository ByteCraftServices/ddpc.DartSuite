namespace ddpc.DartSuite.Application.Contracts.Matches;

public sealed record MatchDto(
    Guid Id,
    Guid TournamentId,
    string Phase,
    int? GroupNumber,
    int Round,
    int MatchNumber,
    Guid? BoardId,
    Guid HomeParticipantId,
    Guid AwayParticipantId,
    int HomeLegs,
    int AwayLegs,
    int HomeSets,
    int AwaySets,
    Guid? WinnerParticipantId,
    DateTimeOffset? PlannedStartUtc,
    bool IsStartTimeLocked,
    bool IsBoardLocked,
    DateTimeOffset? StartedUtc,
    DateTimeOffset? FinishedUtc,
    string Status = "Erstellt",
    string? ExternalMatchId = null);