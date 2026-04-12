using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ddpc.DartSuite.Web.Components;

public partial class App : IAsyncDisposable
{
    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    private TooltipToast? _tooltipToast;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // TooltipToast initialisiert sich selbst in OnAfterRenderAsync
        await base.OnAfterRenderAsync(firstRender);
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        // Cleanup wenn nötig
        await Task.CompletedTask;
    }
}
