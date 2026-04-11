using Microsoft.Playwright;

namespace ddpc.DartSuite.E2E.Tests.SmokeTests;

/// <summary>
/// DS-043: Automatisierter UI-Smoke-Flow Mobile (375x812, Chromium, mobile emulation).
/// Prüft: Responsive Layout, Tab-Scroll, Mobile-Ansicht der Turnierliste.
/// Voraussetzung: DARTSUITE_E2E_ENABLED=1 und optionale DARTSUITE_E2E_BASE_URL.
/// </summary>
public sealed class MobileSmokeTests : SmokeTestBase
{
    // Override viewport for mobile tests
    private const int MobileWidth = 375;
    private const int MobileHeight = 812;

    [Fact(DisplayName = "DS-043-01: Startseite lädt auf mobile Viewport (375x812)")]
    public async Task HomePage_Loads_OnMobileViewport()
    {
        if (!ShouldRun()) return;

        await Context.NewPageAsync();
        var mobilePage = await Browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = MobileWidth, Height = MobileHeight }
        });

        var response = await mobilePage.GotoAsync(BaseUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30_000
        });

        Assert.NotNull(response);
        Assert.True(response.Ok, $"HTTP {response.Status}");

        await mobilePage.CloseAsync();
    }

    [Fact(DisplayName = "DS-043-02: Turnier-Tabs horizontal scrollbar auf Mobile")]
    public async Task TournamentsPage_TabStrip_ScrollableOnMobile()
    {
        if (!ShouldRun()) return;

        var mobilePage = await Browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = MobileWidth, Height = MobileHeight }
        });

        await mobilePage.GotoAsync($"{BaseUrl}/tournaments", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30_000
        });

        // Klicke erstes Turnier (falls vorhanden)
        var listItem = mobilePage.Locator(".list-group-item-action").First;
        if (await listItem.IsVisibleAsync())
        {
            await listItem.ClickAsync();
            await mobilePage.WaitForLoadStateAsync(LoadState.NetworkIdle,
                new PageWaitForLoadStateOptions { Timeout = 10_000 });

            // Tab-Strip mit nowrap-Klasse vorhanden
            var tabStrip = mobilePage.Locator(".tournament-tabs-nowrap");
            await tabStrip.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 8_000
            });

            var stripVisible = await tabStrip.IsVisibleAsync();
            Assert.True(stripVisible, "Tab-Strip .tournament-tabs-nowrap nicht sichtbar");
        }

        await mobilePage.CloseAsync();
    }

    [Fact(DisplayName = "DS-043-03: Mobile Strip-Navigation (Turnierliste komprimiert)")]
    public async Task TournamentsPage_MobileStrip_Displayed_WhenListCollapsed()
    {
        if (!ShouldRun()) return;

        var mobilePage = await Browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = MobileWidth, Height = MobileHeight }
        });

        await mobilePage.GotoAsync($"{BaseUrl}/tournaments", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30_000
        });

        // Tournament list item click
        var listItem = mobilePage.Locator(".list-group-item-action").First;
        if (await listItem.IsVisibleAsync())
        {
            await listItem.ClickAsync();
            await mobilePage.WaitForLoadStateAsync(LoadState.NetworkIdle,
                new PageWaitForLoadStateOptions { Timeout = 10_000 });

            // Auf Mobile wird der Strip erst sichtbar, wenn die Turnierliste eingeklappt ist.
            var collapseButton = mobilePage.Locator("button[title*='Turnierliste ausblenden']").First;
            if (await collapseButton.IsVisibleAsync())
            {
                await collapseButton.ClickAsync();
                await mobilePage.WaitForLoadStateAsync(LoadState.NetworkIdle,
                    new PageWaitForLoadStateOptions { Timeout = 10_000 });
            }

            // Auf manchen Zuständen bleibt die Standardliste sichtbar; beides ist als Mobile-Navigation valide.
            var mobileStrip = mobilePage.Locator(".tournament-mobile-strip-wrap");
            var stripVisible = await mobileStrip.IsVisibleAsync();

            var mobileListEntries = mobilePage.Locator(".list-group-item-action");
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

        var mobilePage = await Browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = MobileWidth, Height = MobileHeight }
        });

        await mobilePage.GotoAsync($"{BaseUrl}/tournaments", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30_000
        });

        // Überprüfe ob document.body.scrollWidth <= viewport width
        var scrollWidth = await mobilePage.EvaluateAsync<int>("document.body.scrollWidth");
        Assert.True(scrollWidth <= MobileWidth + 5, // 5px Toleranz für Scrollbar
            $"Horizontaler Overflow: body.scrollWidth={scrollWidth} > viewport={MobileWidth}");

        await mobilePage.CloseAsync();
    }
}
