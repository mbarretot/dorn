namespace Dorn.WebUI.Primitives.Theme;

// Snapshot of what theme-boot.js already applied to <html> before Blazor started.
public sealed record ThemeSnapshot(string Theme, string Mode, bool SystemPrefersDark);
