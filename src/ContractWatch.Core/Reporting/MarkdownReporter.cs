using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Reporting;

public static class MarkdownReporter
{
    public static string Render(ComparisonResult result)
    {
        var breaking = result.Count(ChangeSeverity.Breaking);
        var potentiallyBreaking = result.Count(ChangeSeverity.PotentiallyBreaking);
        var compatible = result.Count(ChangeSeverity.Compatible);

        var verdict = breaking > 0 ? "FAILED" : potentiallyBreaking > 0 ? "WARNING" : "PASSED";
        var lines = new List<string>
        {
            $"## API compatibility: {verdict}",
            string.Empty,
            breaking > 0
                ? $"This PR introduces **{breaking} breaking** contract changes."
                : "No breaking contract changes detected.",
            string.Empty,
            "| Severity | Operation | Change | Rule |",
            "|---|---|---|---|",
        };

        foreach (var change in result.Changes)
        {
            var severity = change.Severity switch
            {
                ChangeSeverity.Breaking => "✗ Breaking",
                ChangeSeverity.PotentiallyBreaking => "⚠ Potentially breaking",
                _ => "✓ Compatible",
            };
            var target = change.Location.Method is null
                ? $"`{Escape(change.Location.Path)}`"
                : $"`{change.Location.Method} {Escape(change.Location.Path)}`";

            lines.Add($"| {severity} | {target} | {Escape(change.Message)} | {change.RuleId} |");
        }

        lines.Add(string.Empty);
        lines.Add($"{breaking} breaking · {potentiallyBreaking} potentially breaking · {compatible} compatible");

        return string.Join(Environment.NewLine, lines);
    }

    private static string Escape(string text) => text.Replace("|", "\\|");
}
