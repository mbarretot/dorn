namespace CleanArchBlazorWasm.Web.Components.Theme;

/// <summary>
/// Thin wrapper around the <c>window.dornTheme</c> global that <c>wwwroot/theme-boot.js</c>
/// exposes. Unlike the primitives layer's modal/dismiss/anchor interop (design C8), this is not
/// a lazily-imported ES module: <c>theme-boot.js</c> is a classic script guaranteed to have run
/// before Blazor started, so calls are safe from the very first render.
/// </summary>
public sealed class ThemeInterop(IJSRuntime jsRuntime)
{
    public ValueTask<ThemeSnapshot> GetSnapshotAsync() =>
        jsRuntime.InvokeAsync<ThemeSnapshot>("dornTheme.getSnapshot");

    public ValueTask SetThemeAsync(string theme) =>
        jsRuntime.InvokeVoidAsync("dornTheme.setTheme", theme);

    public ValueTask SetModeAsync(string mode) =>
        jsRuntime.InvokeVoidAsync("dornTheme.setMode", mode);
}
