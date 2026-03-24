using ddpc.DartSuite.Domain.Enums;

namespace ddpc.DartSuite.Domain.Entities;

public sealed class Tournament
{
    private readonly List<Participant> _participants = new();

    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string OrganizerAccount { get; set; } = string.Empty;
    public TournamentStatus Status { get; set; } = TournamentStatus.Erstellt;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TournamentMode Mode { get; set; } = TournamentMode.Knockout;
    public TournamentVariant Variant { get; set; } = TournamentVariant.Online;
    public bool TeamplayEnabled { get; set; }
    public bool IsLocked { get; set; }
    public bool AreGameModesLocked { get; set; }
    public string? JoinCode { get; set; }

    // Group phase settings
    public int GroupCount { get; set; }
    public int PlayoffAdvancers { get; set; } = 2;
    public int KnockoutsPerRound { get; set; } = 1;
    public int MatchesPerOpponent { get; set; } = 1;
    public GroupMode GroupMode { get; set; } = GroupMode.RoundRobin;
    public GroupDrawMode GroupDrawMode { get; set; } = GroupDrawMode.Random;
    public PlanningVariant PlanningVariant { get; set; } = PlanningVariant.RoundByRound;
    public GroupOrderMode GroupOrderMode { get; set; } = GroupOrderMode.ReverseEachRound;

    // KO settings
    public bool ThirdPlaceMatch { get; set; }

    // Team settings
    public int PlayersPerTeam { get; set; } = 1;

    // Scoring settings
    public int WinPoints { get; set; } = 2;
    public int LegFactor { get; set; } = 1;

    public IReadOnlyCollection<Participant> Participants => _participants.AsReadOnly();

    public void AddParticipant(Participant participant)
    {
        var duplicate = _participants.Any(x =>
            string.Equals(x.AccountName, participant.AccountName, StringComparison.OrdinalIgnoreCase));

        if (duplicate)
        {
            throw new InvalidOperationException("Participant already exists in tournament.");
        }

        participant.TournamentId = Id;
        _participants.Add(participant);
    }
}