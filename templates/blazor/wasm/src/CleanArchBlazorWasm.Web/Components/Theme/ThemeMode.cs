namespace CleanArchBlazorWasm.Web.Components.Theme;

/// <summary>
/// User-facing light/dark preference (design B4). <see cref="System"/> is never the value of
/// <see cref="ThemeState.ResolvedMode"/> — it always resolves to <see cref="Light"/> or
/// <see cref="Dark"/> before reaching the DOM.
/// </summary>
public enum ThemeMode
{
    Light,
    Dark,
    System,
}
