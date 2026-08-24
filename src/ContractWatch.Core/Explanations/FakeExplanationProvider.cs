namespace ContractWatch.Core.Explanations;

public sealed class FakeExplanationProvider : IExplanationProvider
{
    public string Name => ExplanationProviders.Fake;

    public Task<string?> ExplainAsync(ContractChange change, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(
            $"[fake] {change.RuleId} ({change.RuleName}) at {Describe(change.Location)}: {change.Message}.");

    private static string Describe(ChangeLocation location) =>
        location.Method is null ? location.Path : $"{location.Method} {location.Path}";
}
