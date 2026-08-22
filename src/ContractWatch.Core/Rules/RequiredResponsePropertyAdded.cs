using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Rules;

public sealed class RequiredResponsePropertyAdded : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        foreach (var (previousOperation, currentOperation) in OperationPairing.Matched(previous, current))
        {
            foreach (var status in currentOperation.Responses.Keys.Order(StringComparer.Ordinal))
            {
                if (!previousOperation.Responses.TryGetValue(status, out var previousResponse)
                    || previousResponse.JsonSchema is not { } previousSchema
                    || currentOperation.Responses[status].JsonSchema?.RequiredProperties is not { } requiredProperties)
                    continue;

                var previouslyRequired = previousSchema.RequiredProperties ?? EmptySet;

                foreach (var name in requiredProperties.Order(StringComparer.Ordinal))
                {
                    if (previouslyRequired.Contains(name))
                        continue;

                    yield return new ContractChange(
                        "CW011",
                        "RequiredResponsePropertyAdded",
                        ChangeSeverity.PotentiallyBreaking,
                        new ChangeLocation(currentOperation.Path, currentOperation.Method),
                        $"Required response property added: {name}");
                }
            }
        }
    }

    private static readonly IReadOnlySet<string> EmptySet = new HashSet<string>();
}
