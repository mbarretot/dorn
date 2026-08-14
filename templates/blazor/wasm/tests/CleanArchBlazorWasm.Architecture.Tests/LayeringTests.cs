namespace CleanArchBlazorWasm.Architecture.Tests;

/// <summary>
/// Enforces the two structural rules the design calls out as "enforced by the Architecture
/// tier, not convention": <c>Components/Ui/</c> (the design system) never references
/// <c>Features/</c> (app code), and <c>IJSRuntime</c> usage is confined to
/// <c>Components/Ui/Primitives/Interop/</c>. <c>Components/Theme/</c> is intentionally out of
/// scope for the JS-interop rule — the spec's requirement text scopes it to
/// <c>Components/Ui/</c> only, and <c>ThemeInterop</c> legitimately injects
/// <see cref="IJSRuntime"/> from outside that folder.
/// </summary>
public sealed class LayeringTests
{
    private const string UiRoot = "CleanArchBlazorWasm.Web.Components.Ui";
    private const string UiPrimitivesInteropRoot =
        "CleanArchBlazorWasm.Web.Components.Ui.Primitives.Interop";
    private const string FeaturesRoot = "CleanArchBlazorWasm.Web.Features";

    private static readonly System.Reflection.Assembly WebAssembly = typeof(App).Assembly;

    private static readonly ArchitectureModel Architecture = new ArchLoader()
        .LoadAssembliesIncludingDependencies(WebAssembly)
        .Build();

    private static IObjectProvider<IType> InNamespace(string root) =>
        Types().That().ResideInNamespaceMatching($@"^{Regex.Escape(root)}(\.|$)");

    private static readonly IObjectProvider<IType> ComponentsUi = InNamespace(UiRoot);
    private static readonly IObjectProvider<IType> Features = InNamespace(FeaturesRoot);

    [Fact]
    public void ComponentsUi_ShouldNot_DependOnFeatures()
    {
        Types()
            .That()
            .Are(ComponentsUi)
            .Should()
            .NotDependOnAny(Types().That().Are(Features))
            .Check(Architecture);
    }

    [Fact]
    public void Features_ShouldNot_DependOnJsInterop()
    {
        Types()
            .That()
            .Are(Features)
            .Should()
            .NotDependOnAny(Types().That().ResideInNamespaceMatching(@"^Microsoft\.JSInterop"))
            .Check(Architecture);
    }

    [Fact]
    public void JsRuntimeUsage_Should_BeConfinedToUiPrimitivesInterop()
    {
        // ArchUnitNET's fluent predicates don't reliably express "everywhere except this one
        // sub-namespace", so this rule uses plain reflection instead — same precedent as
        // webapi/grpc/worker's RequestHandlers_Should_ResideInApplicationAssembly.
        var violators = WebAssembly
            .GetTypes()
            .Where(type =>
                type.Namespace is not null
                && type.Namespace.StartsWith(UiRoot, StringComparison.Ordinal)
                && !type.Namespace.StartsWith(UiPrimitivesInteropRoot, StringComparison.Ordinal)
            )
            .Where(InjectsJsRuntime)
            .ToList();

        Assert.Empty(violators);
    }

    [Fact]
    public void NoTypeOutsideComponentsUi_Should_ShareANameWithAUiComponent()
    {
        // "_Imports" is Razor tooling infrastructure generated once per folder that opts into
        // scoped @using directives (Components/Ui/_Imports.razor is a legitimate example) — it
        // is never a real component and must not count as a collision candidate.
        var uiTypeNames = WebAssembly
            .GetTypes()
            .Where(type =>
                type.Namespace is not null
                && type.Namespace.StartsWith(UiRoot, StringComparison.Ordinal)
            )
            .Where(type => !type.Name.Contains('<', StringComparison.Ordinal))
            .Where(type => !type.Name.StartsWith('_'))
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        var collisions = WebAssembly
            .GetTypes()
            .Where(type =>
                type.Namespace is not null
                && !type.Namespace.StartsWith(UiRoot, StringComparison.Ordinal)
            )
            .Where(type => !type.Name.StartsWith('_'))
            .Where(type => uiTypeNames.Contains(type.Name))
            .ToList();

        Assert.Empty(collisions);
    }

    private static bool InjectsJsRuntime(System.Type type)
    {
        const BindingFlags flags =
            BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.DeclaredOnly;

        return type.GetFields(flags).Any(f => typeof(IJSRuntime).IsAssignableFrom(f.FieldType))
            || type.GetProperties(flags)
                .Any(p => typeof(IJSRuntime).IsAssignableFrom(p.PropertyType));
    }
}
