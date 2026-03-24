namespace ddpc.DartSuite.Application.Contracts.Boards;

public sealed record UpdateBoardRequest(
    Guid Id,
    string Name,
    string? LocalIpAddress,
    string? BoardManagerUrl);
