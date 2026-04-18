using System.Net;
using System.Net.Http;
using System.Text;
using System.Globalization;
using System.Reflection;
using Bunit;
using ddpc.DartSuite.Application.Contracts.Matches;
using ddpc.DartSuite.Application.Contracts.Tournaments;
using ddpc.DartSuite.Web.Components;
using ddpc.DartSuite.Web.Components.Pages;
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

    [Fact]
    public void MatchStatusAndLiveBadges_ExposeDescriptiveTooltips()
    {
        using var ctx = CreateContext();

        var cut = ctx.RenderComponent<MatchCard>(parameters => parameters
            .Add(p => p.Match, CreateRunningMatch())
            .Add(p => p.Layout, "Horizontal")
            .Add(p => p.ShowActionButtons, false)
            .Add(p => p.ShowHeaderMatchStatus, true)
            .Add(p => p.ShowHeaderLiveBadge, true));

        var liveBadge = cut.FindAll("span.badge").First(x => x.TextContent.Trim() == "Live");
        liveBadge.GetAttribute("title").Should().Be("Live-Match: Werte werden in Echtzeit aktualisiert");

        var statusBadge = cut.FindAll("span.badge").First(x => x.TextContent.Trim() == "Aktiv");
        statusBadge.GetAttribute("title").Should().Contain("Aktiv: Match laeuft gerade");
    }

    [Fact]
    public void Header_DoesNotUseOverflowHiddenClass()
    {
        using var ctx = CreateContext();

        var cut = ctx.RenderComponent<MatchCard>(parameters => parameters
            .Add(p => p.Match, CreateRunningMatch())
            .Add(p => p.Layout, "Horizontal")
            .Add(p => p.ShowActionButtons, false));

        var header = cut.Find("div.card-header");
        header.ClassList.Should().NotContain("overflow-hidden");
    }

    [Fact]
    public void ListLayout_FooterActionLocation_RendersSingleActionMenu()
    {
        using var ctx = CreateContext();

        var cut = ctx.RenderComponent<MatchCard>(parameters => parameters
            .Add(p => p.Match, CreateRunningMatch())
            .Add(p => p.Layout, "List")
            .Add(p => p.ShowActionButtons, true)
            .Add(p => p.ActionButtonsLocation, "Footer")
            .Add(p => p.ShowSyncAction, true));

        cut.FindAll("button.match-action-menu-button").Should().HaveCount(1);
    }

    [Fact]
    public void ListLayout_RendersStableSlotStructure()
    {
        using var ctx = CreateContext();

        var cut = ctx.RenderComponent<MatchCard>(parameters => parameters
            .Add(p => p.Match, CreatePlannedMatch())
            .Add(p => p.Layout, "List")
            .Add(p => p.ShowActionButtons, true)
            .Add(p => p.ShowSyncAction, true)
            .Add(p => p.ActionButtonsLocation, "Header"));

        cut.FindAll(".match-list-leading-meta").Should().HaveCount(1);
        cut.FindAll(".match-list-player-slot-home").Should().HaveCount(1);
        cut.FindAll(".match-list-score-slot").Should().HaveCount(1);
        cut.FindAll(".match-list-player-slot-away").Should().HaveCount(1);
        cut.FindAll(".match-list-trailing-meta").Should().HaveCount(1);
        cut.FindAll(".match-list-actions-slot").Should().HaveCount(1);
    }

    [Fact]
    public void Highlighting_UsesHighlightPhasePreset_WhenThresholdIsReached()
    {
        using var ctx = CreateContext();

        var cut = ctx.RenderComponent<MatchCard>(parameters => parameters
            .Add(p => p.Match, CreateRunningMatch())
            .Add(p => p.Layout, "Horizontal")
            .Add(p => p.ShowActionButtons, false)
            .Add(p => p.EnableHighlighting, true)
            .Add(p => p.HighlightCheckoutThreshold, 501)
            .Add(p => p.HighlightPhaseBorderPreset, "info")
            .Add(p => p.HighlightPhaseBackgroundPreset, "info-soft"));

        cut.Find("div.match-card").ClassList.Should().Contain("match-highlight-border-info");
        cut.Find("div.match-card").ClassList.Should().Contain("match-highlight-bg-info-soft");
    }

    [Fact]
    public void PlayerName_AverageFormatting_IsCultureInvariant()
    {
        using var ctx = CreateContext();

        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            var german = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentCulture = german;
            CultureInfo.CurrentUICulture = german;

            var cut = ctx.RenderComponent<PlayerName>(parameters => parameters
                .Add(p => p.Name, "Max")
                .Add(p => p.ShowAverage, true)
                .Add(p => p.Average, 72.5));

            cut.Markup.Should().Contain("(72.5)");
            cut.Markup.Should().NotContain("(72,5)");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void TournamentRailHeightFormatting_IsCultureInvariant()
    {
        var method = typeof(Tournaments).GetMethod("GetTournamentRailItemHeight", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var tournament = new TournamentDto(
            Guid.NewGuid(),
            "PL Test",
            "doc",
            "Aktiv",
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow),
            null,
            "GroupsAndKnockout",
            "Local",
            false,
            false,
            false,
            null,
            16,
            4,
            8,
            2,
            1,
            "RoundRobin",
            "Snake",
            "Auto",
            "None",
            false,
            1,
            3,
            1);

        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            var german = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentCulture = german;
            CultureInfo.CurrentUICulture = german;

            var result = (string?)method!.Invoke(null, [tournament]);
            result.Should().NotBeNull();
            result!.Should().Contain(".");
            result.Should().NotContain(",");
            result.Should().EndWith("rem");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
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
