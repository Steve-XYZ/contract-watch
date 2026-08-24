namespace ContractWatch.Core.Comparison;

internal static class MessageOperationPairing
{
    public static IEnumerable<(ApiMessageOperation Previous, ApiMessageOperation Current)> Matched(ApiContract previous, ApiContract current)
    {
        var previousByKey = (previous.MessageOperations ?? []).ToDictionary(o => (o.Channel, o.Action));

        foreach (var operation in current.MessageOperations ?? [])
        {
            if (previousByKey.TryGetValue((operation.Channel, operation.Action), out var matched))
                yield return (matched, operation);
        }
    }
}
