using ContractWatch.Core.Explanations;

namespace ContractWatch.Core.Comparison;

public sealed record ExplanationOutcome(ComparisonResult Result, int Failures, string? FirstFailureReason)
{
    public static ExplanationOutcome Unchanged(ComparisonResult result) => new(result, 0, null);
}

public static class ExplanationEnricher
{
    public static async Task<ExplanationOutcome> EnrichAsync(ComparisonResult result, IExplanationProvider provider, CancellationToken cancellationToken = default)
    {
        var failures = 0;
        string? firstFailureReason = null;
        var enriched = new List<ContractChange>(result.Changes.Count);

        foreach (var change in result.Changes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                enriched.Add(change with { Explanation = await provider.ExplainAsync(change, cancellationToken) });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures++;
                firstFailureReason ??= ex.Message;
                enriched.Add(change);
            }
        }

        return new ExplanationOutcome(new ComparisonResult(enriched), failures, Truncate(firstFailureReason));
    }

    private static string? Truncate(string? reason) =>
        reason is { Length: > 200 } longReason ? $"{longReason[..200]}…" : reason;
}
