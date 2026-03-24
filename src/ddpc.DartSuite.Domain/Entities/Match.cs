using ddpc.DartSuite.Domain.Enums;

namespace ddpc.DartSuite.Domain.Entities;

public sealed class Match
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TournamentId { get; set; }
    public MatchPhase Phase { get; set; } = MatchPhase.Knockout;
    public int? GroupNumber { get; set; }
    public int Round { get; set; }
    public int MatchNumber { get; set; }
    public Guid? BoardId { get; set; }
    public Guid HomeParticipantId { get; set; }
    public Guid AwayParticipantId { get; set; }
    public int HomeLegs { get; set; }
    public int AwayLegs { get; set; }
    public int HomeSets { get; set; }
    public int AwaySets { get; set; }
    public Guid? WinnerParticipantId { get; set; }
    public DateTimeOffset? PlannedStartUtc { get; set; }
    public bool IsStartTimeLocked { get; set; }
    public bool IsBoardLocked { get; set; }
    public DateTimeOffset? StartedUtc { get; set; }
    public DateTimeOffset? FinishedUtc { get; set; }
    public string? ExternalMatchId { get; set; }

    /// <summary>Sentinel ID for bye slots.</summary>
    public static readonly Guid ByeParticipantId = Guid.Empty;

    public bool IsBye => HomeParticipantId == ByeParticipantId || AwayParticipantId == ByeParticipantId;

    public void ReportResult(int homeLegs, int awayLegs, int homeSets = 0, int awaySets = 0)
    {
        HomeLegs = homeLegs;
        AwayLegs = awayLegs;
        HomeSets = homeSets;
        AwaySets = awaySets;
        WinnerParticipantId = homeLegs >= awayLegs ? HomeParticipantId : AwayParticipantId;
        FinishedUtc = DateTimeOffset.UtcNow;
    }
}