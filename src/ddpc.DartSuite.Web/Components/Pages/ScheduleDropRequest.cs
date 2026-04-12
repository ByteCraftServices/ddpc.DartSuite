namespace ddpc.DartSuite.Web.Components.Pages;

public sealed record ScheduleDropRequest(
    Guid? PreviousMatchId,
    Guid? NextMatchId,
    Guid? BoardId);