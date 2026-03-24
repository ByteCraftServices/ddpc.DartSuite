namespace ddpc.DartSuite.Application.Contracts.Tournaments;

public sealed record ParticipantDto(
    Guid Id,
    string DisplayName,
    string AccountName,
    bool IsAutodartsAccount,
    bool IsManager,
    int Seed,
    int? GroupNumber = null,
    Guid? TeamId = null);