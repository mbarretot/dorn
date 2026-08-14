namespace CleanArchBlazorWasm.Web.Components.Theme;

/// <summary>
/// Scoped runtime theme/mode state (design B7). Persistence and <c>matchMedia</c> subscription
/// live in <c>theme-boot.js</c>, where they belong; this service holds only the observable
/// state a component tree renders from and delegates every mutation to
/// <see cref="ThemeInterop"/>.
/// </summary>
public sealed class ThemeState(ThemeInterop interop)
{
    public string Theme { get; private set; } = "";

    public ThemeMode Mode { get; private set; } = ThemeMode.System;

    public ThemeMode ResolvedMode { get; private set; } = ThemeMode.Light;

    public event Action? Changed;

    /// <summary>
    /// Reads the theme/mode <c>theme-boot.js</c> already applied to <c>&lt;html&gt;</c>. Must be
    /// called from <c>OnAfterRenderAsync</c> once <see cref="IJSRuntime"/> calls are safe.
    /// </summary>
    public async Task InitializeAsync()
    {
        var snapshot = await interop.GetSnapshotAsync();
        Theme = snapshot.Theme;
        Mode = ParseMode(snapshot.Mode);
        ResolvedMode = ResolveMode(Mode, snapshot.SystemPrefersDark);
        Changed?.Invoke();
    }

    public async Task SetThemeAsync(string theme)
    {
        await interop.SetThemeAsync(theme);
        Theme = theme;
        Changed?.Invoke();
    }

    public async Task SetModeAsync(ThemeMode mode)
    {
        await interop.SetModeAsync(mode.ToString().ToLowerInvariant());
        var snapshot = await interop.GetSnapshotAsync();
        Mode = mode;
        ResolvedMode = ResolveMode(mode, snapshot.SystemPrefersDark);
        Changed?.Invoke();
    }

    /// <summary>
    /// Pure resolution: <see cref="ThemeMode.System"/> resolves against the current OS
    /// preference, everything else is already concrete.
    /// </summary>
    public static ThemeMode ResolveMode(ThemeMode mode, bool systemPrefersDark) =>
        mode switch
        {
            ThemeMode.Light => ThemeMode.Light,
            ThemeMode.Dark => ThemeMode.Dark,
            ThemeMode.System => systemPrefersDark ? ThemeMode.Dark : ThemeMode.Light,
            _ => ThemeMode.Light,
        };

    private static ThemeMode ParseMode(string raw) =>
        raw switch
        {
            "light" => ThemeMode.Light,
            "dark" => ThemeMode.Dark,
            _ => ThemeMode.System,
        };
}
