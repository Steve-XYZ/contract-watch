using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Rules;

public sealed class OptionalParameterAdded : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        foreach (var (previousOperation, currentOperation) in OperationPairing.Matched(previous, current))
        {
            var previousParameters = previousOperation.Parameters.Select(p => (p.Name, p.In)).ToHashSet();

            foreach (var parameter in currentOperation.Parameters.Where(p => !p.IsRequired).OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                if (previousParameters.Contains((parameter.Name, parameter.In)))
                    continue;

                yield return new ContractChange(
                    "CW014",
                    "OptionalParameterAdded",
                    ChangeSeverity.Compatible,
                    new ChangeLocation(currentOperation.Path, currentOperation.Method),
                    $"Optional parameter added: {parameter.Name}");
            }
        }
    }
}
