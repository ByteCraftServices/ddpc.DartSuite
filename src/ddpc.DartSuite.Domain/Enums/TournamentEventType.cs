namespace ddpc.DartSuite.Domain.Enums;

public enum TournamentEventType
{
    MatchCreated = 0,
    MatchStarted = 1,
    LegFinished = 2,
    SetFinished = 3,
    MatchFinished = 4,
    MatchWalkover = 5,
    BoardStatusChanged = 6,
    ParticipantStatusChanged = 7,
    ScheduleChanged = 8,
    TournamentStatusChanged = 9,
    GroupStandingsUpdated = 10
}
