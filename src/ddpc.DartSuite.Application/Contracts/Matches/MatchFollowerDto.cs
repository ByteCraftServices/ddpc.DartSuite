namespace ddpc.DartSuite.Application.Contracts.Matches;

public sealed record MatchFollowerDto(
    Guid Id,
    Guid MatchId,
    string UserAccountName,
    DateTimeOffset CreatedUtc);
