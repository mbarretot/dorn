namespace CleanArchBlazorWasm.Architecture.Tests;

// Components/Theme/ThemeInterop is exempt from the JS-interop confinement rule below by design.
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
        // ArchUnitNET can't express "everywhere except this sub-namespace", so this uses reflection.
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
        // "_Imports" is Razor scoped-usings infrastructure, never a real component.
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
