namespace ddpc.DartSuite.Application.Contracts.Notifications;

public sealed record TournamentEventDto(
    Guid Id,
    Guid TournamentId,
    string EventType,
    string Title,
    string? Message,
    Guid? MatchId,
    Guid? ParticipantId,
    DateTimeOffset CreatedUtc);
