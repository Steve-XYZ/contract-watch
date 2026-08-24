using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Rules;

public sealed class MessagePropertyTypeChanged : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        foreach (var (previousOperation, currentOperation) in MessageOperationPairing.Matched(previous, current))
        {
            if (previousOperation.PayloadSchema?.Properties is not { } previousProperties
                || currentOperation.PayloadSchema?.Properties is not { } currentProperties)
                continue;

            foreach (var (name, currentProperty) in currentProperties.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                if (!previousProperties.TryGetValue(name, out var previousProperty)
                    || previousProperty.Kind == currentProperty.Kind)
                    continue;

                yield return new ContractChange(
                    "CW022",
                    "MessagePropertyTypeChanged",
                    ChangeSeverity.Breaking,
                    new ChangeLocation(currentOperation.Channel, currentOperation.Action),
                    $"Message property {name} changed: {previousProperty.RenderType()} → {currentProperty.RenderType()}");
            }
        }
    }
}
