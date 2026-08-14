namespace CleanArchBlazorServer.Web.Components.Ui.Primitives;

public static class UiId
{
    // Process-global under Server (shared across all circuits); prerender and interactive passes mint different ids for the same component — accepted, ARIA stays consistent within each pass. See ADR 0024.
    private static long _counter;

    public static string New(string prefix = "ui") =>
        $"{prefix}-{Interlocked.Increment(ref _counter)}";
}
