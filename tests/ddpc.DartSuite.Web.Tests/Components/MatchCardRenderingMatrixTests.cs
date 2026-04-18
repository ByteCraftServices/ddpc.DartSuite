using System.Net;
using System.Net.Http;
using System.Text;
using Bunit;
using ddpc.DartSuite.Application.Contracts.Matches;
using ddpc.DartSuite.Web.Components;
using ddpc.DartSuite.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ddpc.DartSuite.Web.Tests.Components;

public sealed class MatchCardRenderingMatrixTests
{
    [Fact]
    public void ListLayout_LiveScoreMode_ShowsSetAndLegForBothSides()
    {
        using var ctx = CreateContext();

        var cut = ctx.RenderComponent<MatchCard>(parameters => parameters
            .Add(p => p.Match, CreateRunningMatch(homeLegs: 2, awayLegs: 1, homeSets: 0, awaySets: 0))
            .Add(p => p.Layout, "List")
            .Add(p => p.SetModeActive, true)
            .Add(p => p.ShowActionButtons, false)
            .Add(p => p.ScoreMode, "LiveScores")
            .Add(p => p.LiveScoreEnabled, true));

        cut.FindAll("span.badge").Count(x => x.TextContent.Trim() == "S 0").Should().Be(2);
        cut.FindAll("span.badge").Count(x => x.TextContent.Trim() == "L 2").Should().Be(1);
        cut.FindAll("span.badge").Count(x => x.TextContent.Trim() == "L 1").Should().Be(1);
    }

    [Fact]
    public void ListLayout_UsesBodyStartTimeWhenHeaderStartTimeDisabled()
    {
        using var ctx = CreateContext();

        var cut = ctx.RenderComponent<MatchCard>(parameters => parameters
            .Add(p => p.Match, CreatePlannedMatch())
            .Add(p => p.Layout, "List")
            .Add(p => p.ShowActionButtons, false)
            .Add(p => p.ShowHeaderStartTime, false)
            .Add(p => p.ShowBodyStartTime, true));

        cut.Markup.Should().Contain("Zeit:");
    }

    [Fact]
    public void BodyParity_FieldsRenderWhenHeaderCounterpartsDisabled()
    {
        using var ctx = CreateContext();

        var cut = ctx.RenderComponent<MatchCard>(parameters => parameters
            .Add(p => p.Match, CreateRunningMatch())
            .Add(p => p.Layout, "Horizontal")
            .Add(p => p.MatchName, "Entscheidungsspiel")
            .Add(p => p.ShowHeaderMatchName, false)
            .Add(p => p.ShowHeaderMatchStatus, false)
            .Add(p => p.ShowHeaderLiveBadge, false)
            .Add(p => p.ShowBodyMatchName, true)
            .Add(p => p.ShowBodyMatchStatus, true)
            .Add(p => p.ShowBodyLiveBadge, true)
            .Add(p => p.ShowActionButtons, false));

        cut.Markup.Should().Contain("Entscheidungsspiel");
        cut.FindAll("span.badge").Any(x => x.TextContent.Trim() == "Live").Should().BeTrue();
        cut.FindAll("span.badge").Any(x => x.TextContent.Trim() == "Aktiv").Should().BeTrue();
    }

    [Fact]
    public void ActionMenu_UsesLargeButtonClassAndAriaLabel()
    {
        using var ctx = CreateContext();

        var cut = ctx.RenderComponent<MatchCard>(parameters => parameters
            .Add(p => p.Match, CreateRunningMatch())
            .Add(p => p.Layout, "List")
            .Add(p => p.ShowActionButtons, true)
            .Add(p => p.ShowSyncAction, true));

        var menuButton = cut.Find("button.match-action-menu-button");
        menuButton.GetAttribute("aria-label").Should().NotBeNullOrWhiteSpace();
        menuButton.GetAttribute("aria-label")!.Should().Contain("Match-Aktionen");
    }

    private static TestContext CreateContext()
    {
        var ctx = new TestContext();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Api:BaseUrl"] = "http://localhost"
            })
            .Build();

        ctx.Services.AddSingleton<IConfiguration>(config);
        ctx.Services.AddSingleton<NavigationManager, StubNavigationManager>();
        ctx.Services.AddSingleton(new TournamentHubService(config));

        var nav = new StubNavigationManager();
        var client = new HttpClient(new StubHttpHandler()) { BaseAddress = new Uri("http://localhost") };
        ctx.Services.AddSingleton(new DartSuiteApiService(client, nav));

        return ctx;
    }

    private static MatchDto CreatePlannedMatch()
    {
        return new MatchDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Group",
            1,
            1,
            5,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            0,
            0,
            0,
            0,
            null,
            DateTimeOffset.UtcNow.AddMinutes(10),
            false,
            false,
            null,
            null,
            "Geplant",
            null,
            DateTimeOffset.UtcNow.AddMinutes(40),
            DateTimeOffset.UtcNow.AddMinutes(40),
            0,
            "OnTime",
            null,
            null,
            null,
            "Max Mustermann",
            "Anna Schmidt",
            "Board 1");
    }

    private static MatchDto CreateRunningMatch(int homeLegs = 2, int awayLegs = 1, int homeSets = 0, int awaySets = 0)
    {
        return new MatchDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Group",
            1,
            1,
            5,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            homeLegs,
            awayLegs,
            homeSets,
            awaySets,
            null,
            DateTimeOffset.UtcNow.AddMinutes(-15),
            false,
            false,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            null,
            "Aktiv",
            null,
            DateTimeOffset.UtcNow.AddMinutes(35),
            DateTimeOffset.UtcNow.AddMinutes(35),
            0,
            "OnTime",
            null,
            null,
            null,
            "Max Mustermann",
            "Anna Schmidt",
            "Board 1");
    }

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }

    private sealed class StubNavigationManager : NavigationManager
    {
        public StubNavigationManager()
        {
            Initialize("http://localhost/", "http://localhost/");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }
}
