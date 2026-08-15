namespace CleanArchBlazorServer.Web.Components.Theme;

public sealed class ThemeState(ThemeInterop interop)
{
    public string Theme { get; private set; } = "";

    public ThemeMode Mode { get; private set; } = ThemeMode.System;

    public ThemeMode ResolvedMode { get; private set; } = ThemeMode.Light;

    public event Action? Changed;

    // Must run from OnAfterRenderAsync — never fires during Server's prerender pass, so Theme stays "" until connect (S-B2).
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
