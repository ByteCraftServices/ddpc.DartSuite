namespace ddpc.DartSuite.ApiClient.Contracts;

public sealed record AutodartsBoard(
    string ExternalBoardId,
    string Name,
    string? LocalIpAddress,
    string? BoardManagerUrl,
    string? Ownership = null);