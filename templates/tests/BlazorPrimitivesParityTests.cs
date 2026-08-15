using Dorn.Core.Templating;
using Xunit;

namespace TemplateGenerationTests;

// Spec-mandated parity check: compares code with comments/namespace stripped, so it verifies behavioral/structural equivalence, not prose.
public class BlazorPrimitivesParityTests
{
    private const string WasmNamespaceToken = "CleanArchBlazorWasm";
    private const string ServerNamespaceToken = "CleanArchBlazorServer";
    private const string NamespacePlaceholder = "TEMPLATE_NAMESPACE";

    private static readonly string[] PrimitiveFileNames =
    [
        "Cn.cs",
        "ClassGroups.cs",
        "RovingFocusState.cs",
        "TypeaheadBuffer.cs",
        "UiId.cs",
        "UiValueComponent.cs",
        "UiInputBase.cs",
    ];

    private static readonly string[] InteropFileNames =
    [
        "UiInteropModule.cs",
        "ModalInterop.cs",
        "DismissInterop.cs",
        "AnchorInterop.cs",
    ];

    public static IEnumerable<object[]> PrimitiveFiles() =>
        PrimitiveFileNames.Select(name => new object[] { name });

    public static IEnumerable<object[]> InteropFiles() =>
        InteropFileNames.Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(PrimitiveFiles))]
    public void Primitive_MatchesWasmSource_ModuloNamespaceAndComments(string fileName)
    {
        AssertParity(PrimitivesDirectory("wasm"), PrimitivesDirectory("server"), fileName);
    }

    [Theory]
    [MemberData(nameof(InteropFiles))]
    public void Interop_MatchesWasmSource_ModuloNamespaceAndComments(string fileName)
    {
        AssertParity(
            Path.Combine(PrimitivesDirectory("wasm"), "Interop"),
            Path.Combine(PrimitivesDirectory("server"), "Interop"),
            fileName
        );
    }

    private static void AssertParity(string wasmDirectory, string serverDirectory, string fileName)
    {
        var wasmPath = Path.Combine(wasmDirectory, fileName);
        var serverPath = Path.Combine(serverDirectory, fileName);

        Assert.True(File.Exists(wasmPath), $"Expected WASM source at '{wasmPath}'.");
        Assert.True(File.Exists(serverPath), $"Expected Server source at '{serverPath}'.");

        var wasmNormalized = Normalize(File.ReadAllText(wasmPath));
        var serverNormalized = Normalize(File.ReadAllText(serverPath));

        Assert.Equal(wasmNormalized, serverNormalized);
    }

    private static string Normalize(string source)
    {
        var withPlaceholder = source
            .Replace(WasmNamespaceToken, NamespacePlaceholder, StringComparison.Ordinal)
            .Replace(ServerNamespaceToken, NamespacePlaceholder, StringComparison.Ordinal);

        var codeLines = withPlaceholder
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.TrimEnd())
            .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            .Where(line => line.Length > 0);

        return string.Join('\n', codeLines);
    }

    private static string PrimitivesDirectory(string hostingModel)
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        var projectName =
            hostingModel == "wasm" ? "CleanArchBlazorWasm.Web" : "CleanArchBlazorServer.Web";
        return Path.Combine(
            templatesRoot,
            "blazor",
            hostingModel,
            "src",
            projectName,
            "Components",
            "Ui",
            "Primitives"
        );
    }
}
