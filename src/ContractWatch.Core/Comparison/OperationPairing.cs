namespace ContractWatch.Core.Comparison;

internal static class OperationPairing
{
    public static IEnumerable<(ApiOperation Previous, ApiOperation Current)> Matched(ApiContract previous, ApiContract current)
    {
        var previousByKey = previous.Operations.ToDictionary(o => (o.Path, o.Method));

        foreach (var operation in current.Operations)
        {
            if (previousByKey.TryGetValue((operation.Path, operation.Method), out var matched))
                yield return (matched, operation);
        }
    }
}
