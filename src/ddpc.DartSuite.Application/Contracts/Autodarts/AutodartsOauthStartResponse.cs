namespace ddpc.DartSuite.Application.Contracts.Autodarts;

public sealed record AutodartsOauthStartResponse(
    string AuthorizationUrl,
    string SessionId);
