namespace ddpc.DartSuite.Application.Contracts.Boards;

public sealed record UpdateBoardRequest(
    Guid Id,
    string Name,
    string? ExternalBoardId,
    string? LocalIpAddress,
    string? BoardManagerUrl,
    bool? IsVirtual = null,
    string? OwnerAccountName = null);
