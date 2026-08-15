namespace CleanArchBlazorServer.Web.Components.Theme;

// System never reaches the DOM directly; ResolveMode always turns it into Light or Dark first.
public enum ThemeMode
{
    Light,
    Dark,
    System,
}
