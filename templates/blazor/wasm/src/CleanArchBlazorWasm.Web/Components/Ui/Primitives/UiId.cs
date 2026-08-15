namespace CleanArchBlazorWasm.Web.Components.Ui.Primitives;

// Generates unique DOM ids for a11y wiring (FormField's Label/@for and Input/@id pairing).
public static class UiId
{
    private static long _counter;

    public static string New(string prefix = "ui") =>
        $"{prefix}-{Interlocked.Increment(ref _counter)}";
}
