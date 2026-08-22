using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Rules;

public sealed class MetadataOnlyChanged : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        foreach (var (previousOperation, currentOperation) in OperationPairing.Matched(previous, current))
        {
            if (!OperationStructure.Equal(previousOperation, currentOperation)
                || string.Equals(previousOperation.Summary, currentOperation.Summary, StringComparison.Ordinal)
                    && string.Equals(previousOperation.Description, currentOperation.Description, StringComparison.Ordinal))
                continue;

            yield return new ContractChange(
                "CW018",
                "MetadataOnlyChanged",
                ChangeSeverity.Compatible,
                new ChangeLocation(currentOperation.Path, currentOperation.Method),
                "Operation metadata updated");
        }
    }
}
