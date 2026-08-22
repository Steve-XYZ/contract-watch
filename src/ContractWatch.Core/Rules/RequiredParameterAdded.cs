using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Rules;

public sealed class RequiredParameterAdded : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        foreach (var (previousOperation, currentOperation) in OperationPairing.Matched(previous, current))
        {
            var previousParameters = previousOperation.Parameters.ToDictionary(p => (p.Name, p.In));

            foreach (var parameter in currentOperation.Parameters.Where(p => p.IsRequired).OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                if (!previousParameters.TryGetValue((parameter.Name, parameter.In), out var existing))
                {
                    yield return Change(currentOperation, $"Required parameter added: {parameter.Name}");
                }
                else if (!existing.IsRequired)
                {
                    yield return Change(currentOperation, $"Parameter became required: {parameter.Name}");
                }
            }
        }
    }

    private static ContractChange Change(ApiOperation operation, string message) => new(
        "CW003",
        "RequiredParameterAdded",
        ChangeSeverity.Breaking,
        new ChangeLocation(operation.Path, operation.Method),
        message);
}
