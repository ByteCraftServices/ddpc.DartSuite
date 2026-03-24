namespace ddpc.DartSuite.ApiClient.Contracts;

public sealed record AutodartsEvent(string EventType, string BoardExternalId, string PayloadJson, DateTimeOffset TimestampUtc);