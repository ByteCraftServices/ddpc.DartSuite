namespace ddpc.DartSuite.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public string Provider { get; set; } = "InMemory";
    public string ConnectionString { get; set; } = "Data Source=dartsuite.db";
}