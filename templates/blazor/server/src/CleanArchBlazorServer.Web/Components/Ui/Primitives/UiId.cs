namespace CleanArchBlazorServer.Web.Components.Ui.Primitives;

public static class UiId
{
    // Process-global under Server; prerender/interactive ids diverge — accepted, see ADR 0024.
    private static long _counter;

    public static string New(string prefix = "ui") =>
        $"{prefix}-{Interlocked.Increment(ref _counter)}";
}
