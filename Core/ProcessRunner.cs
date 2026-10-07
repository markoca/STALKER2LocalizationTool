namespace LocalizationWorkbench.Core;

public sealed class ProcessResult
{
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
}

public static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        Action<string>? log = null,
        string? workingDirectory = null,
        bool throwOnNonZero = true,
        IReadOnlyDictionary<string, string?>? environment = null,
        Action<string>? outputLine = null,
        bool captureStandardOutput = true,
        ProcessPriorityClass? priorityClass = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
            throw new FileNotFoundException($"Executable not found: {executable}", executable);

        var argumentList = arguments.Select(x => x ?? string.Empty).ToList();
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? (Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory)
                : workingDirectory,
        };

        foreach (var argument in argumentList)
            psi.ArgumentList.Add(argument);

        if (environment is not null)
        {
            foreach (var pair in environment)
            {
                if (pair.Value is null)
                    psi.Environment.Remove(pair.Key);
                else
                    psi.Environment[pair.Key] = pair.Value;
            }
        }

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;

            if (captureStandardOutput)
            {
                lock (stdout)
                    stdout.AppendLine(e.Data);
            }

            outputLine?.Invoke(e.Data);
            log?.Invoke(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (stderr) stderr.AppendLine(e.Data);
            log?.Invoke(e.Data);
        };

        if (!process.Start())
            throw new InvalidOperationException($"Could not start: {executable}");

        if (priorityClass is not null)
        {
            try
            {
                process.PriorityClass = priorityClass.Value;
            }
            catch
            {
                // Priority is an optimization only. Wine and restricted Windows
                // environments may reject priority changes.
            }
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);

            // Flush any remaining asynchronous stdout/stderr callbacks before
            // returning to callers that consume streamed lines.
            process.WaitForExit();
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch { }
            throw;
        }

        var result = new ProcessResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdout.ToString(),
            StandardError = stderr.ToString(),
        };

        if (throwOnNonZero && result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Command failed ({result.ExitCode}): {Path.GetFileName(executable)} {string.Join(" ", argumentList)}\r\n" +
                result.StandardError.Trim()
            );
        }

        return result;
    }
}
