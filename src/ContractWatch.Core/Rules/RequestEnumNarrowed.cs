using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Rules;

public sealed class RequestEnumNarrowed : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        foreach (var (previousOperation, currentOperation) in OperationPairing.Matched(previous, current))
        {
            foreach (var (name, previousValues, currentValues) in EnumPairing.MatchedInput(previousOperation, currentOperation))
            {
                if (!EnumPairing.Narrowed(previousValues, currentValues))
                    continue;

                yield return new ContractChange(
                    "CW006",
                    "RequestEnumNarrowed",
                    ChangeSeverity.Breaking,
                    new ChangeLocation(currentOperation.Path, currentOperation.Method),
                    $"Request enum narrowed: {name}: {EnumPairing.Render(previousValues)} → {EnumPairing.Render(currentValues)}");
            }
        }
    }
}
