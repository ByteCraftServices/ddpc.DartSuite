namespace ddpc.DartSuite.Domain.Entities;

public sealed class Admin
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string AccountName { get; set; }
    public DateOnly ValidFromDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly ValidToDate { get; set; } = DateOnly.MaxValue;
}
