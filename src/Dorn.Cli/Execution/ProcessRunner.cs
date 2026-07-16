using System.Diagnostics;

namespace Dorn.Cli.Execution;

/// <summary>
/// Default implementation of <see cref="IProcessRunner"/> that wraps <see cref="Process"/>.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    public async Task<int> RunAsync(ProcessSpec spec, CancellationToken ct)
    {
        var workingDir = spec.WorkingDirectory ?? Directory.GetCurrentDirectory();

        var psi = new ProcessStartInfo
        {
            FileName = spec.FileName,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // Don't use Shell=true — we want real process isolation.
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in spec.Arguments)
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };

        var tcs = new TaskCompletionSource<int>();

        process.EnableRaisingEvents = true;

        process.Exited += (_, _) =>
        {
            tcs.TrySetResult(process.ExitCode);
        };

        ct.Register(() =>
        {
            tcs.TrySetCanceled();
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort; process may have already exited.
            }
        });

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            // "File not found" — command does not exist. Return a non-zero exit code
            // instead of crashing.
            return 127; // standard "command not found" shell code
        }

        // We must read stdout/stderr to avoid deadlock when the OS pipes fill up.
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        // Wait for either exit or cancellation.
        try
        {
            await using (ct.Register(() => tcs.TrySetCanceled()))
            {
                await tcs.Task;
            }
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch { }
            throw;
        }

        return process.ExitCode;
    }
}
