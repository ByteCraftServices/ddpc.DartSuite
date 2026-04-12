namespace ddpc.DartSuite.Web.Components.Pages;

public sealed class KnockoutDrawCard
{
    public int MatchNumber { get; init; }
    public Guid? HomeParticipantId { get; set; }
    public Guid? AwayParticipantId { get; set; }
}