namespace ddpc.DartSuite.Application.Contracts.Boards;

public sealed record CreateVirtualBoardRequest(
    string Name,
    string? OwnerAccountName);
