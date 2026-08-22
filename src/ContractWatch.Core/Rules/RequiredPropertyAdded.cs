using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Rules;

public sealed class RequiredPropertyAdded : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        foreach (var (previousOperation, currentOperation) in OperationPairing.Matched(previous, current))
        {
            var previousSchema = previousOperation.RequestJsonSchema;
            var currentSchema = currentOperation.RequestJsonSchema;

            if (previousSchema is null || currentSchema?.RequiredProperties is not { } requiredProperties)
                continue;

            var previouslyRequired = previousSchema.RequiredProperties ?? EmptySet;

            foreach (var name in requiredProperties.Order(StringComparer.Ordinal))
            {
                if (previouslyRequired.Contains(name))
                    continue;

                yield return new ContractChange(
                    "CW004",
                    "RequiredPropertyAdded",
                    ChangeSeverity.Breaking,
                    new ChangeLocation(currentOperation.Path, currentOperation.Method),
                    $"Required request property added: {name}");
            }
        }
    }

    private static readonly IReadOnlySet<string> EmptySet = new HashSet<string>();
}
