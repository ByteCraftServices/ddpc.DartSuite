using System.Text.Json;

namespace ddpc.DartSuite.ApiClient.Contracts;

public sealed record AutodartsLobby(
    string Id,
    string? Status,
    JsonElement? Players,
    JsonElement? Settings,
    JsonElement RawJson);
