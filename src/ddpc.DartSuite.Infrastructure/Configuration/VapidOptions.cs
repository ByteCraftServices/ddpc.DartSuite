namespace ddpc.DartSuite.Infrastructure.Configuration;

public sealed class VapidOptions
{
    public const string SectionName = "Vapid";

    public string Subject { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
}
