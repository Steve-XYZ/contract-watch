namespace ContractWatch.Core.Rules;

public sealed class EndpointAdded : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        var previousPaths = previous.Operations.Select(o => o.Path).ToHashSet(StringComparer.Ordinal);

        foreach (var path in current.Operations.Select(o => o.Path).Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal))
        {
            if (previousPaths.Contains(path))
                continue;

            yield return new ContractChange(
                "CW013",
                "EndpointAdded",
                ChangeSeverity.Compatible,
                new ChangeLocation(path),
                "Endpoint added");
        }
    }
}
