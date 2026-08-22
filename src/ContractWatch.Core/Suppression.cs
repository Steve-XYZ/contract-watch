using ContractWatch.Core.Comparison;

namespace ContractWatch.Core;

public sealed record Suppression(string RuleId, string Path, string? Method, string Reason);

public sealed class SuppressionFileException : Exception
{
    public SuppressionFileException(string path, int line, string problem)
        : base($"Supresión inválida en {path}:{line}: {problem}")
    {
    }
}

public static class SuppressionFile
{
    public const string DefaultFileName = ".contractwatchignore";

    public static IReadOnlyList<Suppression> LoadOrDefault(string? explicitPath, string? directory = null)
    {
        var path = explicitPath ?? Path.Combine(directory ?? Directory.GetCurrentDirectory(), DefaultFileName);
        return File.Exists(path) ? Load(path) : [];
    }

    public static IReadOnlyList<Suppression> Load(string path)
    {
        var lines = File.ReadAllLines(path);
        var suppressions = new List<Suppression>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            suppressions.Add(Parse(path, i + 1, line));
        }

        return suppressions;
    }

    private static Suppression Parse(string path, int lineNumber, string line)
    {
        var separator = line.IndexOf("::", StringComparison.Ordinal);

        if (separator < 0)
            throw new SuppressionFileException(path, lineNumber, "falta '::' con la justificación");

        var reason = line[(separator + 2)..].Trim();

        if (reason.Length == 0)
            throw new SuppressionFileException(path, lineNumber, "la justificación es obligatoria");

        var tokens = line[..separator].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length is < 2 or > 3)
            throw new SuppressionFileException(path, lineNumber, "se espera '<ruleId> <path> [<method>] :: <razón>'");

        return new Suppression(
            tokens[0],
            tokens[1],
            tokens.Length == 3 ? tokens[2].ToUpperInvariant() : null,
            reason);
    }

    public static bool Matches(Suppression suppression, ContractChange change) =>
        suppression.RuleId == change.RuleId
        && string.Equals(suppression.Path, change.Location.Path, StringComparison.Ordinal)
        && (suppression.Method is null
            || string.Equals(suppression.Method, change.Location.Method, StringComparison.OrdinalIgnoreCase));

    public static ComparisonResult Apply(ComparisonResult result, IReadOnlyList<Suppression> suppressions)
    {
        if (suppressions.Count == 0)
            return result;

        var remaining = result.Changes.Where(c => !suppressions.Any(s => Matches(s, c))).ToList();
        return new ComparisonResult(remaining);
    }

    public static int CountSuppressed(ComparisonResult original, ComparisonResult filtered) =>
        original.Changes.Count - filtered.Changes.Count;
}
