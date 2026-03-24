namespace ddpc.DartSuite.Application.Contracts.Autodarts;

public sealed record AutodartsBoardsImportRequest(IReadOnlyList<AutodartsBoardInfoDto> Boards);
