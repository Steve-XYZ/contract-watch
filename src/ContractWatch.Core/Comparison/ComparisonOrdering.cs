namespace ContractWatch.Core.Comparison;

internal static class ComparisonOrdering
{
    public static List<ContractChange> Apply(IEnumerable<ContractChange> changes) =>
        changes
            .OrderByDescending(c => c.Severity)
            .ThenBy(c => c.Location.Path, StringComparer.Ordinal)
            .ThenBy(c => c.Location.Method ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(c => c.RuleId, StringComparer.Ordinal)
            .ToList();
}
