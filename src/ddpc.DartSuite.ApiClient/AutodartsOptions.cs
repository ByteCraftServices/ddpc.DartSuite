namespace ddpc.DartSuite.ApiClient;

public sealed class AutodartsOptions
{
    public string AuthBaseUrl { get; set; } = "https://login.autodarts.io/";
    public string ApiBaseUrl { get; set; } = "https://api.autodarts.io/";
    public string Realm { get; set; } = "autodarts";
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? WebBaseUrl { get; set; }
    public string? Scope { get; set; } = "openid profile email";
    public string? Audience { get; set; }
    public string[] AudienceCandidates { get; set; } = Array.Empty<string>();
}
