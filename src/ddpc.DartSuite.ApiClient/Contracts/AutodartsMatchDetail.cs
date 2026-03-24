using System.Text.Json;

namespace ddpc.DartSuite.ApiClient.Contracts;

public sealed record AutodartsMatchDetail(
    string Id,
    string? Variant,
    string? GameMode,
    bool Finished,
    JsonElement? Players,
    JsonElement? Turns,
    JsonElement? Legs,
    JsonElement? Sets,
    JsonElement? Stats,
    JsonElement RawJson);
