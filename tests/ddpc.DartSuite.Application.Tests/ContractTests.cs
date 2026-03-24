using ddpc.DartSuite.Application.Contracts.Tournaments;
using FluentAssertions;

namespace ddpc.DartSuite.Application.Tests;

public sealed class ContractTests
{
    [Fact]
    public void CreateTournamentRequest_ShouldSupportNullableEndDate()
    {
        var request = new CreateTournamentRequest(
            "Demo",
            "manager",
            DateOnly.FromDateTime(DateTime.Today),
            null,
            false,
            "Knockout",
            "OnSite");

        request.EndDate.Should().BeNull();
    }
}