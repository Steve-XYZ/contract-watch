using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Rules;

public sealed class MessagePropertyRemoved : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        foreach (var (previousOperation, currentOperation) in MessageOperationPairing.Matched(previous, current))
        {
            if (currentOperation.Direction is not MessageDirection.Outbound
                || previousOperation.PayloadSchema?.Properties is not { } previousProperties
                || currentOperation.PayloadSchema?.Properties is not { } currentProperties)
                continue;

            foreach (var name in previousProperties.Keys.Where(n => !currentProperties.ContainsKey(n)).Order(StringComparer.Ordinal))
            {
                yield return new ContractChange(
                    "CW023",
                    "MessagePropertyRemoved",
                    ChangeSeverity.Breaking,
                    new ChangeLocation(currentOperation.Channel, currentOperation.Action),
                    $"Message property removed: {name}");
            }
        }
    }
}
