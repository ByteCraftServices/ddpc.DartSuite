namespace ddpc.DartSuite.Application.Contracts.Tournaments;

public sealed record UserViewPreferenceDto(
    Guid Id,
    string UserAccountName,
    string ViewContext,
    string SettingsJson);
