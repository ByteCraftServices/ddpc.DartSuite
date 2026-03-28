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
    public MatchStatus Status { get; set; } = MatchStatus.Erstellt;
    public DateTimeOffset? StartedUtc { get; set; }
    public DateTimeOffset? FinishedUtc { get; set; }
    public string? ExternalMatchId { get; set; }

    /// <summary>Sentinel ID for bye slots.</summary>
    public static readonly Guid ByeParticipantId = Guid.Empty;

    public bool IsBye => Status == MatchStatus.WalkOver;

    /// <summary>Recomputes Status from current field values (does not override WalkOver unless it's a bye).</summary>
    public void RecomputeStatus()
    {
        // A bye is when exactly one participant slot is empty.
        bool isBye = (HomeParticipantId == Guid.Empty) != (AwayParticipantId == Guid.Empty);
        if (isBye)
        {
            Status = MatchStatus.WalkOver;
            return;
        }

        if (Status == MatchStatus.WalkOver) return;
        if (FinishedUtc is not null || WinnerParticipantId is not null)
            Status = MatchStatus.Beendet;
        else if (ExternalMatchId is not null || StartedUtc is not null)
            Status = MatchStatus.Aktiv;
        else if (PlannedStartUtc is not null)
            Status = MatchStatus.Geplant;
        else
            Status = MatchStatus.Erstellt;
    }

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