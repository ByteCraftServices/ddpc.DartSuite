namespace ddpc.DartSuite.Application.Contracts.Matches;

public sealed record ReportMatchResultRequest(
    Guid MatchId,
    int HomeLegs,
    int AwayLegs,
    int HomeSets = 0,
    int AwaySets = 0);