using ddpc.DartSuite.Domain.Entities;
using FluentAssertions;

namespace ddpc.DartSuite.Domain.Tests;

public sealed class TournamentTests
{
    [Fact]
    public void AddParticipant_ShouldRejectDuplicates()
    {
        var tournament = new Tournament
        {
            Name = "Demo",
            OrganizerAccount = "org",
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today)
        };

        tournament.AddParticipant(new Participant
        {
            DisplayName = "A",
            AccountName = "user-a"
        });

        var act = () => tournament.AddParticipant(new Participant
        {
            DisplayName = "B",
            AccountName = "user-a"
        });

        act.Should().Throw<InvalidOperationException>();
    }
}