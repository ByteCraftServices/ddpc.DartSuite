namespace ddpc.DartSuite.Application.Contracts.Autodarts;

public sealed record AutodartsBoardInfoDto(
    string ExternalBoardId,
    string Name,
    string? LocalIpAddress,
    string? BoardManagerUrl,
    string? Ownership);
