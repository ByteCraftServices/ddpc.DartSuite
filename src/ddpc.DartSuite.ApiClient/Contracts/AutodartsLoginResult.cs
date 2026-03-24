namespace ddpc.DartSuite.ApiClient.Contracts;

public sealed record AutodartsLoginResult(
    AutodartsProfile Profile,
    IReadOnlyList<AutodartsBoard> Boards,
    AutodartsTokenResponse Token);
