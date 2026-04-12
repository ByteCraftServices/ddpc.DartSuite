namespace ddpc.DartSuite.Application.Contracts.Matches;

public sealed record UpdateMatchRequest(
    Guid MatchId,
    Guid? BoardId,
    int HomeLegs,
    int AwayLegs,
    int HomeSets,
    int AwaySets,
    string Status,
    bool IsStartTimeLocked,
    bool IsBoardLocked,
    Guid? WinnerParticipantId,
    DateTimeOffset? PlannedStartUtc = null,
    DateTimeOffset? PlannedEndUtc = null,
    string? HomeSlotOrigin = null,
    string? AwaySlotOrigin = null,
    string? NextMatchInfo = null);
