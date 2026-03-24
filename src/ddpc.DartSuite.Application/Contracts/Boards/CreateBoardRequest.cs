namespace ddpc.DartSuite.Application.Contracts.Boards;

public sealed record CreateBoardRequest(
    string ExternalBoardId,
    string Name,
    string? LocalIpAddress,
    string? BoardManagerUrl);