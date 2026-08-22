using ContractWatch.Core.Rules;

namespace ContractWatch.Core.Comparison;

public sealed record ComparisonResult(IReadOnlyList<ContractChange> Changes)
{
    public int Count(ChangeSeverity severity) => Changes.Count(c => c.Severity == severity);

    public bool FailsAt(ChangeSeverity? minimumSeverity) =>
        minimumSeverity is { } threshold && Changes.Any(c => c.Severity >= threshold);
}

public sealed class ContractComparer(IReadOnlyList<IContractRule>? rules = null)
{
    private readonly IReadOnlyList<IContractRule> _rules = rules ?? RuleCatalog.Default;

    public ComparisonResult Compare(ApiContract previous, ApiContract current)
    {
        var changes = _rules
            .SelectMany(rule => rule.Evaluate(previous, current))
            .OrderByDescending(c => c.Severity)
            .ThenBy(c => c.Location.Path, StringComparer.Ordinal)
            .ThenBy(c => c.Location.Method ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(c => c.RuleId, StringComparer.Ordinal)
            .ToList();

        return new ComparisonResult(changes);
    }
}
