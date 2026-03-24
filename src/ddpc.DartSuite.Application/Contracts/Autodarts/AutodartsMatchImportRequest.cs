using System.Text.Json;

namespace ddpc.DartSuite.Application.Contracts.Autodarts;

public sealed record AutodartsMatchImportRequest(
    string SourceUrl,
    string? MatchId,
    string? LobbyId,
    JsonElement? Match,
    JsonElement? Stats,
    JsonElement? Lobby);
