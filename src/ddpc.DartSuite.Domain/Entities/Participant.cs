using ddpc.DartSuite.Domain.Enums;

namespace ddpc.DartSuite.Domain.Entities;

public sealed class Participant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TournamentId { get; set; }
    public required string DisplayName { get; set; }
    public required string AccountName { get; set; }
    public bool IsAutodartsAccount { get; set; } = true;
    public bool IsManager { get; set; }
    public int Seed { get; set; }
    public int SeedPot { get; set; }
    public int? GroupNumber { get; set; }
    public Guid? TeamId { get; set; }
    public ParticipantType Type { get; set; } = ParticipantType.Spieler;
    public Enums.NotificationPreference NotificationPreference { get; set; } = Enums.NotificationPreference.OwnMatches;
}