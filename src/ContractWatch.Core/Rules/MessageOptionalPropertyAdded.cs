using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Rules;

public sealed class MessageOptionalPropertyAdded : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        foreach (var (previousOperation, currentOperation) in MessageOperationPairing.Matched(previous, current))
        {
            if (currentOperation.Direction is not MessageDirection.Outbound
                || previousOperation.PayloadSchema?.Properties is null
                || currentOperation.PayloadSchema?.Properties is not { } properties)
                continue;

            var previouslyRequired = previousOperation.PayloadSchema.RequiredProperties ?? EmptySet();
            var currentlyRequired = currentOperation.PayloadSchema.RequiredProperties ?? EmptySet();
            var previousNames = previousOperation.PayloadSchema.Properties.Keys.ToHashSet(StringComparer.Ordinal);

            foreach (var name in properties.Keys.Order(StringComparer.Ordinal))
            {
                if (previousNames.Contains(name) || currentlyRequired.Contains(name) || previouslyRequired.Contains(name))
                    continue;

                yield return new ContractChange(
                    "CW027",
                    "MessageOptionalPropertyAdded",
                    ChangeSeverity.Compatible,
                    new ChangeLocation(currentOperation.Channel, currentOperation.Action),
                    $"Optional message property added: {name}");
            }
        }
    }

    private static IReadOnlySet<string> EmptySet() => new HashSet<string>();
}
