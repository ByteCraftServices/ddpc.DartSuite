using ddpc.DartSuite.Domain.Enums;

namespace ddpc.DartSuite.Domain.Entities;

public sealed class TournamentRound
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TournamentId { get; set; }
    public MatchPhase Phase { get; set; } = MatchPhase.Knockout;
    public int RoundNumber { get; set; }

    // Gameplay settings
    public int BaseScore { get; set; } = 501;
    public string InMode { get; set; } = "Straight";
    public string OutMode { get; set; } = "Double";
    public GameMode GameMode { get; set; } = GameMode.Legs;
    public int Legs { get; set; } = 3;
    public int? Sets { get; set; }
    public int MaxRounds { get; set; } = 50;
    public string BullMode { get; set; } = "25/50";
    public string BullOffMode { get; set; } = "Normal";

    // Scheduling
    public int MatchDurationMinutes { get; set; }
    public int PauseBetweenMatchesMinutes { get; set; }
    public int MinPlayerPauseMinutes { get; set; }

    // Board assignment
    public BoardAssignmentMode BoardAssignment { get; set; } = BoardAssignmentMode.Dynamic;
    public Guid? FixedBoardId { get; set; }
}
