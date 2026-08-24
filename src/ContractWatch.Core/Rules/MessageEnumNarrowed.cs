using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Rules;

public sealed class MessageEnumNarrowed : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        foreach (var (previousOperation, currentOperation) in MessageOperationPairing.Matched(previous, current))
        {
            if (currentOperation.Direction is not MessageDirection.Inbound)
                continue;

            foreach (var (name, previousValues, currentValues) in MessageEnumWidened.MatchedPayloadEnums(previousOperation, currentOperation))
            {
                if (!EnumPairing.Narrowed(previousValues, currentValues))
                    continue;

                yield return new ContractChange(
                    "CW025",
                    "MessageEnumNarrowed",
                    ChangeSeverity.Breaking,
                    new ChangeLocation(currentOperation.Channel, currentOperation.Action),
                    $"Message enum narrowed: {name}: {EnumPairing.Render(previousValues)} → {EnumPairing.Render(currentValues)}");
            }
        }
    }
}
