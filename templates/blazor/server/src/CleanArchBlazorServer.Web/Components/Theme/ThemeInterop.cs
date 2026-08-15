using Microsoft.JSInterop;

namespace CleanArchBlazorServer.Web.Components.Theme;

// Wraps window.dornTheme, exposed by wwwroot/theme-boot.js which already ran before Blazor started.
public sealed class ThemeInterop(IJSRuntime jsRuntime)
{
    public ValueTask<ThemeSnapshot> GetSnapshotAsync() =>
        jsRuntime.InvokeAsync<ThemeSnapshot>("dornTheme.getSnapshot");

    public ValueTask SetThemeAsync(string theme) =>
        jsRuntime.InvokeVoidAsync("dornTheme.setTheme", theme);

    public ValueTask SetModeAsync(string mode) =>
        jsRuntime.InvokeVoidAsync("dornTheme.setMode", mode);
}
