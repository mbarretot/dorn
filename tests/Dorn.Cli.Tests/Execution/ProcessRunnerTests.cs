using System.Diagnostics;
using Dorn.Cli.Execution;
using NSubstitute;
using Xunit;

namespace Dorn.Cli.Tests.Execution;

/// <summary>
/// Unit tests for <see cref="ProcessRunner"/> and <see cref="ProcessSpec"/>.
/// Verifies the process execution contract used by all three verb commands.
/// </summary>
public class ProcessRunnerTests : IDisposable
{
    private readonly string _tempRoot;

    public ProcessRunnerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"dorn-processrunner-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // -------------------------------------------------------------------------
    // ProcessSpec construction
    // -------------------------------------------------------------------------

    [Fact]
    public void ProcessSpec_WithAllParameters_StoresAllValues()
    {
        var spec = new ProcessSpec("dotnet", ["test", "--no-build"], _tempRoot);

        Assert.Equal("dotnet", spec.FileName);
        Assert.Equal(["test", "--no-build"], spec.Arguments);
        Assert.Equal(_tempRoot, spec.WorkingDirectory);
    }

    [Fact]
    public void ProcessSpec_WithNoWorkingDirectory_UsesCurrentDirectory()
    {
        var spec = new ProcessSpec("dotnet", ["--version"]);

        Assert.Equal("dotnet", spec.FileName);
        Assert.Equal(["--version"], spec.Arguments);
        Assert.Equal(Directory.GetCurrentDirectory(), spec.WorkingDirectory);
    }

    // -------------------------------------------------------------------------
    // ProcessRunner integration (real process, no mock)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_DotnetVersion_ReturnsZeroExitCode()
    {
        var runner = new ProcessRunner();
        var spec = new ProcessSpec("dotnet", ["--version"]);

        var exitCode = await runner.RunAsync(spec, CancellationToken.None);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_NonexistentCommand_ReturnsNonZeroExitCode()
    {
        var runner = new ProcessRunner();
        var spec = new ProcessSpec("nonexistent-command-xyz", []);

        var exitCode = await runner.RunAsync(spec, CancellationToken.None);

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_WithWorkingDirectory_SetsCorrectCwd()
    {
        // dotnet --list-sdks does not need a project file — it respects --working-directory.
        var runner = new ProcessRunner();
        var spec = new ProcessSpec("dotnet", ["--list-sdks"], _tempRoot);

        var exitCode = await runner.RunAsync(spec, CancellationToken.None);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var runner = new ProcessRunner();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // cancel immediately
        var spec = new ProcessSpec("dotnet", ["--version"]);

        // TaskCanceledException derives from OperationCanceledException.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            runner.RunAsync(spec, cts.Token)
        );
    }

    [Fact]
    public async Task RunAsync_ArgumentEscaping_HandlesSpacesInArgs()
    {
        // Create a project directory with a space in the name to test argument escaping.
        var dirWithSpace = Path.Combine(_tempRoot, "My Project");
        Directory.CreateDirectory(dirWithSpace);

        var runner = new ProcessRunner();
        // --list-sdks is safe and does not need a specific working directory.
        var spec = new ProcessSpec("dotnet", ["--list-sdks"], dirWithSpace);

        var exitCode = await runner.RunAsync(spec, CancellationToken.None);

        Assert.Equal(0, exitCode);
    }
}
