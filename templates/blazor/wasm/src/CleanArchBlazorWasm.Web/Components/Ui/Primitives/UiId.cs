namespace CleanArchBlazorWasm.Web.Components.Ui.Primitives;

/// <summary>
/// Generates unique DOM ids for a11y wiring (e.g. <c>FormField</c>'s <c>Label/@for</c> and
/// <c>Input/@id</c> pairing, design D's Input+Label part).
/// </summary>
public static class UiId
{
    private static long _counter;

    public static string New(string prefix = "ui") =>
        $"{prefix}-{Interlocked.Increment(ref _counter)}";
}
