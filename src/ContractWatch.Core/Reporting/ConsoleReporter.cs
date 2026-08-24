using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Reporting;

public static class ConsoleReporter
{
    public static string Render(IReadOnlyList<ContractChange> changes) => Render(changes, null);

    public static string Render(IReadOnlyList<ContractChange> changes, IReadOnlyList<AffectedConsumer>? impact) =>
        Render(changes, impact, null);

    public static string Render(IReadOnlyList<ContractChange> changes, IReadOnlyList<AffectedConsumer>? impact, IReadOnlyList<ImpactChain>? chains)
    {
        var ordered = ComparisonOrdering.Apply(changes);

        var lines = new List<string>();

        foreach (var change in ordered)
        {
            lines.Add(RenderHeader(change));
            lines.Add(RenderDetail(change));

            if (change.Suggestion is { } suggestion)
                lines.Add($"    ↳ {suggestion}");
        }

        if (ordered.Count > 0)
        {
            lines.Add(new string('─', 37));
        }
        else
        {
            lines.Add("No contract changes detected.");
        }

        lines.Add(RenderSummary(ordered));

        if (impact is not null)
            lines.AddRange(RenderImpact(impact, ordered));

        if (chains is { Count: > 0 })
            lines.AddRange(RenderChains(chains));

        return string.Join(Environment.NewLine, lines);
    }

    private static IEnumerable<string> RenderImpact(IReadOnlyList<AffectedConsumer> impact, List<ContractChange> ordered)
    {
        if (impact.Count == 0)
        {
            if (ordered.Any(c => c.Severity != ChangeSeverity.Compatible))
                yield return "Sin consumidores afectados.";

            yield break;
        }

        yield return string.Empty;
        yield return "Consumidores afectados:";

        foreach (var consumer in impact)
            yield return RenderAffected(consumer);
    }

    private static IEnumerable<string> RenderChains(IReadOnlyList<ImpactChain> chains)
    {
        yield return string.Empty;
        yield return "Cadenas de impacto:";

        foreach (var chain in chains)
        {
            yield return $"  {string.Join(" → ", chain.Services)} · confianza {(chain.Confidence == ConfidenceLevel.High ? "alta" : "media")}";

            foreach (var trigger in chain.Triggers)
                yield return $"      ↳ {trigger.RuleId} {trigger.Target}";
        }
    }

    private static string RenderAffected(AffectedConsumer consumer) =>
        $"  {consumer.Service} · confianza {(consumer.Confidence == ConfidenceLevel.High ? "alta" : "media")} · {consumer.Changes} cambio(s)";

    private static string RenderHeader(ContractChange change) => change.Severity switch
    {
        ChangeSeverity.Breaking => $"✗ BREAKING {RenderTarget(change.Location)}",
        ChangeSeverity.PotentiallyBreaking => $"⚠ POTENTIAL  {RenderTarget(change.Location)}",
        _ => $"✓ COMPATIBLE {RenderTarget(change.Location)}",
    };

    private static string RenderTarget(ChangeLocation location) =>
        location.Method is null ? location.Path : $"{location.Method} {location.Path}";

    private static string RenderDetail(ContractChange change) =>
        $"  {change.Message}".PadRight(53) + $" [{change.RuleId}]";

    private static string RenderSummary(List<ContractChange> changes)
    {
        int breaking = 0, potential = 0, compatible = 0;

        foreach (var change in changes)
        {
            switch (change.Severity)
            {
                case ChangeSeverity.Breaking: breaking++; break;
                case ChangeSeverity.PotentiallyBreaking: potential++; break;
                default: compatible++; break;
            }
        }

        return $"{breaking} breaking · {potential} potentially breaking · {compatible} compatible";
    }
}
