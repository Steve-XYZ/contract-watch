using System.Diagnostics;

namespace ContractWatch.Core.Parsing;

public sealed class GitSpecException(string gitRef, string path, string detail)
    : Exception($"No se pudo leer '{path}' en el ref '{gitRef}': {detail}");

public static class GitSpecSource
{
    public static async Task<LoadedSpec> LoadAsync(string gitRef, string repoRelativePath, CancellationToken cancellationToken = default)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"show \"{gitRef}:{repoRelativePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        }) ?? throw new GitSpecException(gitRef, repoRelativePath, "no se pudo ejecutar git");

        var standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
            throw new GitSpecException(gitRef, repoRelativePath, standardError.Trim());

        var tempFile = Path.Combine(Path.GetTempPath(), $"contractwatch-baseline-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempFile, standardOutput, cancellationToken);
            return await SpecLoader.LoadAsync(tempFile, cancellationToken);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
