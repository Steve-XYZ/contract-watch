using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Reporting;

public static class ConsoleReporter
{
    public static string Render(IReadOnlyList<ContractChange> changes)
    {
        var ordered = ComparisonOrdering.Apply(changes);

        var lines = new List<string>();

        foreach (var change in ordered)
        {
            lines.Add(RenderHeader(change));
            lines.Add(RenderDetail(change));
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

        return string.Join(Environment.NewLine, lines);
    }

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
