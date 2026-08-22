using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Rules;

public sealed class ResponsePropertyTypeChanged : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        foreach (var (previousOperation, currentOperation) in OperationPairing.Matched(previous, current))
        {
            foreach (var status in currentOperation.Responses.Keys.Order(StringComparer.Ordinal))
            {
                if (!previousOperation.Responses.TryGetValue(status, out var previousResponse)
                    || previousResponse.JsonSchema?.Properties is not { } previousProperties
                    || currentOperation.Responses[status].JsonSchema?.Properties is not { } currentProperties)
                    continue;

                foreach (var (name, currentProperty) in currentProperties.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    if (!previousProperties.TryGetValue(name, out var previousProperty)
                        || previousProperty.Kind == currentProperty.Kind)
                        continue;

                    yield return new ContractChange(
                        "CW008",
                        "ResponsePropertyTypeChanged",
                        ChangeSeverity.Breaking,
                        new ChangeLocation(currentOperation.Path, currentOperation.Method),
                        $"Response property {name} changed: {previousProperty.RenderType()} → {currentProperty.RenderType()}");
                }
            }
        }
    }
}
