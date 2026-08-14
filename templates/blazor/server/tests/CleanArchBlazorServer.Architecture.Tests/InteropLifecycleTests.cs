namespace CleanArchBlazorServer.Architecture.Tests;

// S-P fitness function. Reflection fallback per design (IL call-site walking judged brittle):
// no Components.Ui type injecting an Interop service may override a pre-connect lifecycle hook.
public sealed class InteropLifecycleTests
{
    private const string UiRoot = "CleanArchBlazorServer.Web.Components.Ui";
    private const string InteropRoot = "CleanArchBlazorServer.Web.Components.Ui.Primitives.Interop";

    private static readonly System.Reflection.Assembly WebAssembly = typeof(Program).Assembly;

    // OnParametersSet (sync) is excluded: the design's "set pending flag" shape overrides it
    // legitimately without ever touching JS there — only the Async hooks and OnInitialized run
    // early enough during prerender to be a real risk.
    private static readonly string[] PreConnectHooks =
    [
        "OnInitialized",
        "OnInitializedAsync",
        "OnParametersSetAsync",
    ];

    [Fact]
    public void InteropInjectingComponents_Should_NotOverridePreConnectLifecycleHooks()
    {
        var violators = WebAssembly
            .GetTypes()
            .Where(type =>
                type.Namespace is not null
                && type.Namespace.StartsWith(UiRoot, StringComparison.Ordinal)
                && !type.Namespace.StartsWith(InteropRoot, StringComparison.Ordinal)
            )
            .Where(InjectsAnInteropModule)
            .Where(OverridesAPreConnectHook)
            .ToList();

        Assert.Empty(violators);
    }

    [Fact]
    public void GeneratedTree_Should_NotConfigureCircuitOptionsOrShipReconnectUi()
    {
        var violators = WebAssembly
            .GetTypes()
            .Where(type =>
                type.GetFields(AllDeclared).Any(f => f.FieldType.Name == "CircuitOptions")
                || type.GetProperties(AllDeclared).Any(p => p.PropertyType.Name == "CircuitOptions")
                || type.Name.Contains("Reconnect", StringComparison.OrdinalIgnoreCase)
            )
            .ToList();

        Assert.Empty(violators);
    }

    private static bool InjectsAnInteropModule(System.Type type) =>
        type.GetFields(AllDeclared)
            .Any(f => f.FieldType.Name.EndsWith("Interop", StringComparison.Ordinal))
        || type.GetProperties(AllDeclared)
            .Any(p => p.PropertyType.Name.EndsWith("Interop", StringComparison.Ordinal));

    private static bool OverridesAPreConnectHook(System.Type type) =>
        PreConnectHooks.Any(hook =>
            type.GetMethod(
                hook,
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            )
                is not null
        );

    private const BindingFlags AllDeclared =
        BindingFlags.Instance
        | BindingFlags.Static
        | BindingFlags.Public
        | BindingFlags.NonPublic
        | BindingFlags.DeclaredOnly;
}
