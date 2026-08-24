using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Rules;

public sealed class MessageRequiredPropertyAdded : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        foreach (var (previousOperation, currentOperation) in MessageOperationPairing.Matched(previous, current))
        {
            if (currentOperation.Direction is not MessageDirection.Inbound
                || previousOperation.PayloadSchema is null
                || currentOperation.PayloadSchema?.RequiredProperties is not { } requiredProperties)
                continue;

            var previouslyRequired = previousOperation.PayloadSchema.RequiredProperties ?? EmptySet;

            foreach (var name in requiredProperties.Order(StringComparer.Ordinal))
            {
                if (previouslyRequired.Contains(name))
                    continue;

                yield return new ContractChange(
                    "CW021",
                    "MessageRequiredPropertyAdded",
                    ChangeSeverity.Breaking,
                    new ChangeLocation(currentOperation.Channel, currentOperation.Action),
                    $"Required message property added: {name}");
            }
        }
    }

    private static readonly IReadOnlySet<string> EmptySet = new HashSet<string>();
}
