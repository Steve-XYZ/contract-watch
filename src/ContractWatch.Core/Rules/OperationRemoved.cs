namespace ContractWatch.Core.Rules;

public sealed class OperationRemoved : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        var currentKeys = current.Operations.Select(o => (o.Path, o.Method)).ToHashSet();
        var alivePaths = current.Operations.Select(o => o.Path).ToHashSet(StringComparer.Ordinal);

        foreach (var operation in previous.Operations.OrderBy(o => o.Path, StringComparer.Ordinal).ThenBy(o => o.Method, StringComparer.Ordinal))
        {
            if (!alivePaths.Contains(operation.Path))
                continue;

            if (currentKeys.Contains((operation.Path, operation.Method)))
                continue;

            yield return new ContractChange(
                "CW002",
                "OperationRemoved",
                ChangeSeverity.Breaking,
                new ChangeLocation(operation.Path, operation.Method),
                $"Method {operation.Method} removed");
        }
    }
}
