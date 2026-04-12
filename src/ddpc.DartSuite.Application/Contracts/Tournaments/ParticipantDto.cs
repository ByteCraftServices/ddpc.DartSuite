namespace ddpc.DartSuite.Application.Contracts.Tournaments;

public sealed record ParticipantDto(
    Guid Id,
    string DisplayName,
    string AccountName,
    bool IsAutodartsAccount,
    bool IsManager,
    int Seed,
    int SeedPot = 0,
    int? GroupNumber = null,
    Guid? TeamId = null,
    string NotificationPreference = "OwnMatches",
    string Type = "Spieler");