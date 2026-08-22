using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Rules;

public sealed class RequestEnumWidened : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        foreach (var (previousOperation, currentOperation) in OperationPairing.Matched(previous, current))
        {
            foreach (var (name, previousValues, currentValues) in EnumPairing.MatchedInput(previousOperation, currentOperation))
            {
                if (!EnumPairing.Widened(previousValues, currentValues))
                    continue;

                yield return new ContractChange(
                    "CW016",
                    "RequestEnumWidened",
                    ChangeSeverity.Compatible,
                    new ChangeLocation(currentOperation.Path, currentOperation.Method),
                    $"Request enum widened: {name}: {EnumPairing.Render(previousValues)} → {EnumPairing.Render(currentValues)}");
            }
        }
    }
}
