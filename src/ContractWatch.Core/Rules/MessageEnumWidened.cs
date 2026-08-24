using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Rules;

public sealed class MessageEnumWidened : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        foreach (var (previousOperation, currentOperation) in MessageOperationPairing.Matched(previous, current))
        {
            if (currentOperation.Direction is not MessageDirection.Outbound)
                continue;

            foreach (var (name, previousValues, currentValues) in MatchedPayloadEnums(previousOperation, currentOperation))
            {
                if (!EnumPairing.Widened(previousValues, currentValues))
                    continue;

                yield return new ContractChange(
                    "CW024",
                    "MessageEnumWidened",
                    ChangeSeverity.PotentiallyBreaking,
                    new ChangeLocation(currentOperation.Channel, currentOperation.Action),
                    $"Message enum widened: {name}: {EnumPairing.Render(previousValues)} → {EnumPairing.Render(currentValues)}");
            }
        }
    }

    internal static IEnumerable<(string Name, IReadOnlyList<string> Previous, IReadOnlyList<string> Current)> MatchedPayloadEnums(ApiMessageOperation previousOperation, ApiMessageOperation currentOperation)
    {
        if (previousOperation.PayloadSchema?.Properties is not { } previousProperties
            || currentOperation.PayloadSchema?.Properties is not { } currentProperties)
            yield break;

        foreach (var (name, currentProperty) in currentProperties.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (currentProperty.EnumValues is not { } currentValues
                || !previousProperties.TryGetValue(name, out var previousProperty)
                || previousProperty.EnumValues is not { } previousValues)
                continue;

            yield return (name, previousValues, currentValues);
        }
    }
}
