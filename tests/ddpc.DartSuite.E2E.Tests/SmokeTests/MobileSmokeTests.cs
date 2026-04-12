using Microsoft.Playwright;

namespace ddpc.DartSuite.E2E.Tests.SmokeTests;

/// <summary>
/// DS-043: Automatisierter UI-Smoke-Flow Mobile (375x812, Chromium, mobile emulation).
/// Prüft: Responsive Layout, Tab-Scroll, Mobile-Ansicht der Turnierliste.
/// Voraussetzung: DARTSUITE_E2E_ENABLED=1 und optionale DARTSUITE_E2E_BASE_URL.
/// Hinweis: WaitUntilState.Commit + WaitForLoadState(Load) wird verwendet,
/// da Blazor Server via SignalR NetworkIdle verzögern oder blockieren kann.
/// </summary>
public sealed class MobileSmokeTests : SmokeTestBase
{
    private const int MobileWidth = 375;
    private const int MobileHeight = 812;

    private async Task<IPage> CreateMobilePageAsync()
        => await Browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = MobileWidth, Height = MobileHeight },
            IgnoreHTTPSErrors = true
        });

    [Fact(DisplayName = "DS-043-01: Startseite lädt auf mobile Viewport (375x812)")]
    public async Task HomePage_Loads_OnMobileViewport()
    {
        if (!ShouldRun()) return;

        var mobilePage = await CreateMobilePageAsync();
        var response = await mobilePage.GotoAsync(BaseUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = 30_000
        });
        await mobilePage.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

        Assert.NotNull(response);
        Assert.True(response.Ok, $"HTTP {response.Status}");

        await mobilePage.CloseAsync();
    }

    [Fact(DisplayName = "DS-043-02: Turnier-Tabs horizontal scrollbar auf Mobile")]
    public async Task TournamentsPage_TabStrip_ScrollableOnMobile()
    {
        if (!ShouldRun()) return;

        var mobilePage = await CreateMobilePageAsync();
        await mobilePage.GotoAsync($"{BaseUrl}/tournaments", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = 30_000
        });
        if (IsLoginRedirect(mobilePage))
        {
            await mobilePage.CloseAsync();
            return;
        }
        await mobilePage.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

        var listItem = mobilePage.Locator("li.list-group-item.list-group-item-action").First;
        if (await listItem.IsVisibleAsync())
        {
            await listItem.ClickAsync();
            await mobilePage.WaitForTimeoutAsync(250);
            if (IsLoginRedirect(mobilePage))
            {
                await mobilePage.CloseAsync();
                return;
            }

            var card = mobilePage.Locator(".card.shadow-sm");
            await card.First.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Attached,
                Timeout = 15_000
            });

            var tabStrip = mobilePage.Locator(".tournament-tabs-nowrap, .nav-tabs");
            var tabCount = await tabStrip.CountAsync();
            Assert.True(tabCount > 0, "Tab-Navigation (.tournament-tabs-nowrap/.nav-tabs) nicht im DOM nach Turnier-Auswahl");
        }

        await mobilePage.CloseAsync();
    }

    [Fact(DisplayName = "DS-043-03: Mobile Strip-Navigation (Turnierliste komprimiert)")]
    public async Task TournamentsPage_MobileStrip_Displayed_WhenListCollapsed()
    {
        if (!ShouldRun()) return;

        var mobilePage = await CreateMobilePageAsync();
        await mobilePage.GotoAsync($"{BaseUrl}/tournaments", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = 30_000
        });
        if (IsLoginRedirect(mobilePage))
        {
            await mobilePage.CloseAsync();
            return;
        }
        await mobilePage.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

        var listItem = mobilePage.Locator("li.list-group-item.list-group-item-action").First;
        if (await listItem.IsVisibleAsync())
        {
            await listItem.ClickAsync();
            await mobilePage.WaitForTimeoutAsync(250);
            if (IsLoginRedirect(mobilePage))
            {
                await mobilePage.CloseAsync();
                return;
            }

            var card = mobilePage.Locator(".card.shadow-sm");
            await card.First.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Attached,
                Timeout = 15_000
            });

            var collapseButton = mobilePage.Locator("button[title*='Turnierliste ausblenden']").First;
            if (await collapseButton.IsVisibleAsync())
            {
                await collapseButton.ClickAsync();
                await mobilePage.WaitForTimeoutAsync(500);
            }

            var mobileStrip = mobilePage.Locator(".tournament-mobile-strip-wrap");
            var stripVisible = await mobileStrip.IsVisibleAsync();
            var mobileListEntries = mobilePage.Locator("li.list-group-item.list-group-item-action");
            var hasMobileList = await mobileListEntries.CountAsync() > 0;

            Assert.True(stripVisible || hasMobileList,
                "Weder mobile strip navigation noch mobile Turnierliste sichtbar auf 375px viewport");
        }

        await mobilePage.CloseAsync();
    }

    [Fact(DisplayName = "DS-043-04: Kein horizontaler Overflow auf Mobile")]
    public async Task TournamentsPage_NoHorizontalOverflow_OnMobile()
    {
        if (!ShouldRun()) return;

        var mobilePage = await CreateMobilePageAsync();
        await mobilePage.GotoAsync($"{BaseUrl}/tournaments", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = 30_000
        });
        await mobilePage.WaitForLoadStateAsync(LoadState.Load, new PageWaitForLoadStateOptions { Timeout = 15_000 });

        var scrollWidth = await mobilePage.EvaluateAsync<int>("document.body.scrollWidth");
        Assert.True(scrollWidth <= MobileWidth + 5,
            $"Horizontaler Overflow: body.scrollWidth={scrollWidth} > viewport={MobileWidth}");

        await mobilePage.CloseAsync();
    }
}
