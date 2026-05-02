using System.Net;
using System.Net.Http;
using System.Text;
using Bunit;
using ddpc.DartSuite.Web.Components.Layout;
using ddpc.DartSuite.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ddpc.DartSuite.Web.Tests.Components;

public sealed class NavMenuExpansionTests
{
    // ── Overview group ──────────────────────────────────────────────

    [Fact]
    public void OverviewGroup_ChevronClick_TogglesExpansion()
    {
        using var ctx = CreateContext();
        var cut = RenderNavMenu(ctx);

        // Initially collapsed
        OverviewSubItemCount(cut).Should().Be(0);

        // Click chevron → expand
        ClickOverviewChevron(cut);
        OverviewSubItemCount(cut).Should().BeGreaterThan(0);

        // Click chevron again → collapse
        ClickOverviewChevron(cut);
        OverviewSubItemCount(cut).Should().Be(0);
    }

    [Fact]
    public void OverviewGroup_ParentLinkClick_AlwaysExpands()
    {
        using var ctx = CreateContext();
        var cut = RenderNavMenu(ctx);

        // Click parent link twice – should always be expanded after each click
        ClickOverviewParentLink(cut);
        OverviewSubItemCount(cut).Should().BeGreaterThan(0);

        ClickOverviewParentLink(cut);
        OverviewSubItemCount(cut).Should().BeGreaterThan(0);
    }

    [Fact]
    public void OverviewGroup_AutoExpands_ForTournamentsRoute()
    {
        using var ctx = CreateContext("http://localhost/tournaments");
        var cut = RenderNavMenu(ctx);

        OverviewSubItemCount(cut).Should().BeGreaterThan(0);
    }

    [Fact]
    public void OverviewGroup_AutoExpands_ForMyTournamentsRoute()
    {
        using var ctx = CreateContext("http://localhost/my-tournaments?filter=running");
        var cut = RenderNavMenu(ctx);

        OverviewSubItemCount(cut).Should().BeGreaterThan(0);
    }

    // ── Settings group ──────────────────────────────────────────────

    [Fact]
    public void SettingsGroup_ChevronClick_TogglesExpansion()
    {
        using var ctx = CreateContext();
        var cut = RenderNavMenu(ctx);

        SettingsSubItemCount(cut).Should().Be(0);

        ClickSettingsChevron(cut);
        SettingsSubItemCount(cut).Should().BeGreaterThan(0);

        ClickSettingsChevron(cut);
        SettingsSubItemCount(cut).Should().Be(0);
    }

    [Fact]
    public void SettingsGroup_ParentLinkClick_AlwaysExpands()
    {
        using var ctx = CreateContext();
        var cut = RenderNavMenu(ctx);

        ClickSettingsParentLink(cut);
        SettingsSubItemCount(cut).Should().BeGreaterThan(0);

        ClickSettingsParentLink(cut);
        SettingsSubItemCount(cut).Should().BeGreaterThan(0);
    }

    [Fact]
    public void SettingsGroup_AutoExpands_ForSettingsRoute()
    {
        using var ctx = CreateContext("http://localhost/settings?tab=general");
        var cut = RenderNavMenu(ctx);

        SettingsSubItemCount(cut).Should().BeGreaterThan(0);
    }

    [Fact]
    public void SettingsGroup_AutoExpands_ForProfileRoute()
    {
        using var ctx = CreateContext("http://localhost/profile");
        var cut = RenderNavMenu(ctx);

        SettingsSubItemCount(cut).Should().BeGreaterThan(0);
    }

    // ── Player group ────────────────────────────────────────────────

    [Fact]
    public void PlayerGroup_ChevronClick_TogglesExpansion()
    {
        using var ctx = CreateContext();
        var cut = RenderNavMenu(ctx);

        PlayerSubItemCount(cut).Should().Be(0);

        ClickPlayerChevron(cut);
        PlayerSubItemCount(cut).Should().BeGreaterThan(0);

        ClickPlayerChevron(cut);
        PlayerSubItemCount(cut).Should().Be(0);
    }

    [Fact]
    public void PlayerGroup_ParentButtonClick_AlwaysExpands()
    {
        using var ctx = CreateContext();
        var cut = RenderNavMenu(ctx);

        ClickPlayerParentButton(cut);
        PlayerSubItemCount(cut).Should().BeGreaterThan(0);

        ClickPlayerParentButton(cut);
        PlayerSubItemCount(cut).Should().BeGreaterThan(0);
    }

    [Fact]
    public void PlayerGroup_AutoExpands_ForRegisterRoute()
    {
        using var ctx = CreateContext("http://localhost/register");
        var cut = RenderNavMenu(ctx);

        PlayerSubItemCount(cut).Should().BeGreaterThan(0);
    }

    // ── Dashboard renders ───────────────────────────────────────────

    [Fact]
    public void NavMenu_Renders_DashboardLink()
    {
        using var ctx = CreateContext();
        var cut = RenderNavMenu(ctx);

        cut.FindAll("a.nav-link").Any(a => a.TextContent.Contains("Dashboard")).Should().BeTrue();
    }

    [Fact]
    public void NavMenu_Renders_LoginEntry()
    {
        using var ctx = CreateContext();
        var cut = RenderNavMenu(ctx);

        cut.FindAll("a.nav-link").Any(a => a.GetAttribute("href")?.Contains("login") == true).Should().BeTrue();
    }

    // ── Login entry user data ───────────────────────────────────────

    [Fact]
    public void LoginEntry_ShowsLoginText_WhenNotConnected()
    {
        using var ctx = CreateContext();
        var cut = RenderNavMenu(ctx);

        cut.Markup.Should().Contain("Autodarts - Login");
    }

    [Fact]
    public void LoginEntry_ShowsUserName_WhenConnected()
    {
        using var ctx = CreateContext();

        var appState = ctx.Services.GetRequiredService<AppStateService>();
        appState.SetAutodartsProfileSilent(
            new ddpc.DartSuite.Application.Contracts.Autodarts.AutodartsProfileDto(
                Id: "u1",
                DisplayName: "Max Muster",
                Country: null,
                Email: "testuser@example.com"),
            isConnected: true);

        var cut = RenderNavMenu(ctx);

        cut.Markup.Should().Contain("Max Muster");
        cut.Markup.Should().NotContain("Autodarts - Login");
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static IRenderedComponent<NavMenu> RenderNavMenu(TestContext ctx)
        => ctx.RenderComponent<NavMenu>();

    private static int OverviewSubItemCount(IRenderedComponent<NavMenu> cut)
        => cut.FindAll("a.nav-link[href*='my-tournaments']").Count;

    private static int SettingsSubItemCount(IRenderedComponent<NavMenu> cut)
        => cut.FindAll("a.nav-link[href*='profile'], a.nav-link[href*='settings?tab=user']").Count;

    private static int PlayerSubItemCount(IRenderedComponent<NavMenu> cut)
        => cut.FindAll("a.nav-link[href*='register']").Count;

    private static void ClickOverviewChevron(IRenderedComponent<NavMenu> cut)
    {
        // The overview group chevron is the first expand button with title containing "Turnieruebersicht" or "Untermenue"
        var expandBtns = cut.FindAll("button.nav-expand-btn");
        expandBtns[0].Click();
    }

    private static void ClickOverviewParentLink(IRenderedComponent<NavMenu> cut)
    {
        var link = cut.FindAll("a.nav-link").First(a =>
            a.TextContent.Contains("Turnieruebersicht") ||
            a.GetAttribute("href")?.Contains("tournaments?tab") == true);
        link.Click();
    }

    private static void ClickSettingsChevron(IRenderedComponent<NavMenu> cut)
    {
        var expandBtns = cut.FindAll("button.nav-expand-btn");
        expandBtns[2].Click();
    }

    private static void ClickSettingsParentLink(IRenderedComponent<NavMenu> cut)
    {
        var link = cut.FindAll("a.nav-link").First(a =>
            a.TextContent.Contains("Einstellungen") ||
            a.GetAttribute("href")?.Contains("settings?tab=general") == true);
        link.Click();
    }

    private static void ClickPlayerChevron(IRenderedComponent<NavMenu> cut)
    {
        // The player chevron is the second expand button (index 1)
        var expandBtns = cut.FindAll("button.nav-expand-btn");
        expandBtns[1].Click();
    }

    private static void ClickPlayerParentButton(IRenderedComponent<NavMenu> cut)
    {
        // Player parent is a button (not a NavLink), containing "Spieler"
        var btn = cut.FindAll("button.nav-link").First(b => b.TextContent.Contains("Spieler"));
        btn.Click();
    }

    private static TestContext CreateContext(string currentUrl = "http://localhost/")
    {
        var ctx = new TestContext();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Api:BaseUrl"] = "http://localhost"
            })
            .Build();

        // Create instances upfront so all registrations happen before any retrieval
        var navForContainer = new StubNavigationManager(currentUrl);
        var navForApi = new StubNavigationManager();
        var client = new HttpClient(new StubHttpHandler()) { BaseAddress = new Uri("http://localhost") };

        ctx.Services.AddSingleton<IConfiguration>(config);
        ctx.Services.AddSingleton<NavigationManager>(navForContainer);
        ctx.Services.AddSingleton<AppStateService>();
        ctx.Services.AddSingleton(new BoardHubService(config));
        ctx.Services.AddSingleton<IUiHelpService>(new StubUiHelpService());
        ctx.Services.AddSingleton(new DartSuiteApiService(client, navForApi));

        return ctx;
    }

    private sealed class StubUiHelpService : IUiHelpService
    {
        public string GetContent(string key, string? fallback = null) => fallback ?? string.Empty;
        public string GetTooltip(string key, string? fallback = null) => fallback ?? string.Empty;
    }

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private sealed class StubNavigationManager : NavigationManager
    {
        public StubNavigationManager(string uri = "http://localhost/")
        {
            Initialize("http://localhost/", uri);
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }
}
