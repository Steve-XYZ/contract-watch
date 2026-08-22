using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Rules;

public sealed class ResponseEnumWidened : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        foreach (var (previousOperation, currentOperation) in OperationPairing.Matched(previous, current))
        {
            foreach (var (_, name, previousValues, currentValues) in EnumPairing.MatchedResponse(previousOperation, currentOperation))
            {
                if (!EnumPairing.Widened(previousValues, currentValues))
                    continue;

                yield return new ContractChange(
                    "CW010",
                    "ResponseEnumWidened",
                    ChangeSeverity.PotentiallyBreaking,
                    new ChangeLocation(currentOperation.Path, currentOperation.Method),
                    $"Response enum widened: {name}: {EnumPairing.Render(previousValues)} → {EnumPairing.Render(currentValues)}");
            }
        }
    }
}
