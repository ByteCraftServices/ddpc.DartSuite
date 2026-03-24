namespace ddpc.DartSuite.Application.Contracts.Autodarts;

public sealed record AutodartsSessionStatusDto(
    bool IsConnected,
    AutodartsProfileDto? Profile,
    DateTimeOffset? ExpiresAt);
