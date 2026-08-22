using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Rules;

public sealed class ParameterTypeChanged : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        foreach (var (previousOperation, currentOperation) in OperationPairing.Matched(previous, current))
        {
            var previousParameters = previousOperation.Parameters.ToDictionary(p => (p.Name, p.In));

            foreach (var parameter in currentOperation.Parameters.OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                if (!previousParameters.TryGetValue((parameter.Name, parameter.In), out var existing)
                    || existing.Schema is not { } previousSchema
                    || parameter.Schema is not { } currentSchema
                    || previousSchema.Kind == currentSchema.Kind)
                    continue;

                yield return new ContractChange(
                    "CW005",
                    "ParameterTypeChanged",
                    ChangeSeverity.Breaking,
                    new ChangeLocation(currentOperation.Path, currentOperation.Method),
                    $"Parameter {parameter.Name} changed: {previousSchema.RenderType()} → {currentSchema.RenderType()}");
            }
        }
    }
}
