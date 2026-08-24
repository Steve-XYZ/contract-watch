using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Reporting;

public static class MarkdownReporter
{
    public static string Render(ComparisonResult result) => Render(result, null);

    public static string Render(ComparisonResult result, IReadOnlyList<AffectedConsumer>? impact) =>
        Render(result, impact, null);

    public static string Render(ComparisonResult result, IReadOnlyList<AffectedConsumer>? impact, IReadOnlyList<ImpactChain>? chains)
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
            "| Severity | Operation | Change | Rule | Suggestion |",
            "|---|---|---|---|---|",
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
            var suggestion = change.Suggestion is null ? string.Empty : Escape(change.Suggestion);

            lines.Add($"| {severity} | {target} | {Escape(change.Message)} | {change.RuleId} | {suggestion} |");
        }

        lines.Add(string.Empty);
        lines.Add($"{breaking} breaking · {potentiallyBreaking} potentially breaking · {compatible} compatible");

        if (impact is { Count: > 0 })
            lines.AddRange(RenderImpact(impact));

        if (chains is { Count: > 0 })
            lines.AddRange(RenderChains(chains));

        return string.Join(Environment.NewLine, lines);
    }

    private static IEnumerable<string> RenderChains(IReadOnlyList<ImpactChain> chains)
    {
        yield return string.Empty;
        yield return "#### Impact chains";
        yield return string.Empty;
        yield return "| Chain | Confidence | Triggered by |";
        yield return "|---|---|---|";

        foreach (var chain in chains)
        {
            var triggers = string.Join("; ", chain.Triggers.Select(t => $"{t.RuleId} {t.Target}"));
            yield return $"| `{Escape(string.Join(" → ", chain.Services))}` | {chain.Confidence} | {Escape(triggers)} |";
        }
    }

    private static IEnumerable<string> RenderImpact(IReadOnlyList<AffectedConsumer> impact)
    {
        yield return string.Empty;
        yield return "### Affected consumers";
        yield return string.Empty;
        yield return "| Service | Confidence | Changes |";
        yield return "|---|---|---|";

        foreach (var consumer in impact)
            yield return $"| {Escape(consumer.Service)} | {consumer.Confidence} | {consumer.Changes} |";
    }

    private static string Escape(string text) => text.Replace("|", "\\|");
}
