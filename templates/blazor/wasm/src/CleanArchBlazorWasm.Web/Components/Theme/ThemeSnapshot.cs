namespace CleanArchBlazorWasm.Web.Components.Theme;

/// <summary>
/// The theme/mode state <c>wwwroot/theme-boot.js</c> already applied to <c>&lt;html&gt;</c>
/// before Blazor started. <see cref="ThemeState"/> reads this once on initialization instead of
/// owning its own default (design B6).
/// </summary>
/// <param name="Theme">The active theme name (e.g. <c>slate</c>, <c>rose</c>).</param>
/// <param name="Mode">The stored preference: <c>light</c>, <c>dark</c>, or <c>system</c>.</param>
/// <param name="SystemPrefersDark">The OS-level <c>prefers-color-scheme</c> at read time.</param>
public sealed record ThemeSnapshot(string Theme, string Mode, bool SystemPrefersDark);
