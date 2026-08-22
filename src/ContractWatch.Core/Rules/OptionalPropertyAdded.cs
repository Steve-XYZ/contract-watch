using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Rules;

public sealed class OptionalPropertyAdded : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        foreach (var (previousOperation, currentOperation) in OperationPairing.Matched(previous, current))
        {
            var previousSchema = previousOperation.RequestJsonSchema;
            var currentSchema = currentOperation.RequestJsonSchema;

            if (previousSchema?.Properties is null || currentSchema?.Properties is not { } properties)
                continue;

            var previouslyRequired = previousSchema.RequiredProperties ?? EmptySet();
            var currentlyRequired = currentSchema.RequiredProperties ?? EmptySet();
            var previousNames = previousSchema.Properties.Keys.ToHashSet(StringComparer.Ordinal);

            foreach (var name in properties.Keys.Order(StringComparer.Ordinal))
            {
                if (previousNames.Contains(name) || currentlyRequired.Contains(name) || previouslyRequired.Contains(name))
                    continue;

                yield return new ContractChange(
                    "CW015",
                    "OptionalPropertyAdded",
                    ChangeSeverity.Compatible,
                    new ChangeLocation(currentOperation.Path, currentOperation.Method),
                    $"Optional property added: {name}");
            }
        }
    }

    private static IReadOnlySet<string> EmptySet() => new HashSet<string>();
}
