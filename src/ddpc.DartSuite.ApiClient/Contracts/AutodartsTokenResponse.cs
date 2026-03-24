namespace ddpc.DartSuite.ApiClient.Contracts;

public sealed record AutodartsTokenResponse(
    string AccessToken,
    string? RefreshToken,
    int ExpiresIn);
