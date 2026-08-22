namespace ContractWatch.Core.Rules;

public sealed class EndpointRemoved : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        var currentPaths = current.Operations.Select(o => o.Path).ToHashSet(StringComparer.Ordinal);

        foreach (var path in previous.Operations.Select(o => o.Path).Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal))
        {
            if (currentPaths.Contains(path))
                continue;

            yield return new ContractChange(
                "CW001",
                "EndpointRemoved",
                ChangeSeverity.Breaking,
                new ChangeLocation(path),
                "Endpoint removed");
        }
    }
}
