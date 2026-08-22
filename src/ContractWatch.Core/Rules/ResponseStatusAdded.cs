using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Rules;

public sealed class ResponseStatusAdded : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        foreach (var (previousOperation, currentOperation) in OperationPairing.Matched(previous, current))
        {
            var previousStatuses = previousOperation.Responses.Keys.ToHashSet(StringComparer.Ordinal);

            foreach (var status in currentOperation.Responses.Keys.Order(StringComparer.Ordinal))
            {
                if (previousStatuses.Contains(status))
                    continue;

                yield return new ContractChange(
                    "CW017",
                    "ResponseStatusAdded",
                    ChangeSeverity.Compatible,
                    new ChangeLocation(currentOperation.Path, currentOperation.Method),
                    $"Response status added: {status}");
            }
        }
    }
}
