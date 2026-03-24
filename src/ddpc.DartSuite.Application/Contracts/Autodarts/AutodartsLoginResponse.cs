namespace ddpc.DartSuite.Application.Contracts.Autodarts;

public sealed record AutodartsLoginResponse(
    AutodartsProfileDto Profile,
    IReadOnlyList<AutodartsBoardInfoDto> Boards);
