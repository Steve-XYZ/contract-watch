using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Rules;

public sealed class ResponseStatusRemoved : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        foreach (var (previousOperation, currentOperation) in OperationPairing.Matched(previous, current))
        {
            var currentStatuses = currentOperation.Responses.Keys.ToHashSet(StringComparer.Ordinal);

            foreach (var status in previousOperation.Responses.Keys.Order(StringComparer.Ordinal))
            {
                if (currentStatuses.Contains(status))
                    continue;

                yield return new ContractChange(
                    "CW007",
                    "ResponseStatusRemoved",
                    ChangeSeverity.Breaking,
                    new ChangeLocation(currentOperation.Path, currentOperation.Method),
                    $"Response status removed: {status}");
            }
        }
    }
}
