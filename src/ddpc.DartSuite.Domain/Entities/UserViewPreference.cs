namespace ddpc.DartSuite.Domain.Entities;

/// <summary>Stores user-specific match view display preferences per view context.</summary>
public sealed class UserViewPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserAccountName { get; set; } = string.Empty;

    /// <summary>View context key, e.g. "TournamentPlan", "GroupPhase", "KnockoutPhase", "Schedule".</summary>
    public string ViewContext { get; set; } = string.Empty;

    /// <summary>JSON-serialized display settings (visible columns, detail fields, etc.).</summary>
    public string SettingsJson { get; set; } = "{}";

    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
