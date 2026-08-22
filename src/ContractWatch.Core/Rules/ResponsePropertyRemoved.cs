using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Rules;

public sealed class ResponsePropertyRemoved : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        foreach (var (previousOperation, currentOperation) in OperationPairing.Matched(previous, current))
        {
            foreach (var status in previousOperation.Responses.Keys.Order(StringComparer.Ordinal))
            {
                if (!currentOperation.Responses.TryGetValue(status, out var currentResponse)
                    || currentResponse.JsonSchema?.Properties is not { } currentProperties
                    || previousOperation.Responses[status].JsonSchema?.Properties is not { } previousProperties)
                    continue;

                foreach (var name in previousProperties.Keys.Where(n => !currentProperties.ContainsKey(n)).Order(StringComparer.Ordinal))
                {
                    yield return new ContractChange(
                        "CW009",
                        "ResponsePropertyRemoved",
                        ChangeSeverity.Breaking,
                        new ChangeLocation(currentOperation.Path, currentOperation.Method),
                        $"Response property removed: {name}");
                }
            }
        }
    }
}
