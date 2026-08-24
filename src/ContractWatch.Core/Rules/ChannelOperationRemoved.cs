using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Rules;

public sealed class ChannelOperationRemoved : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        var currentChannels = (current.MessageOperations ?? []).Select(o => o.Channel).ToHashSet(StringComparer.Ordinal);
        var currentKeys = (current.MessageOperations ?? []).Select(o => (o.Channel, o.Action)).ToHashSet();

        foreach (var operation in (previous.MessageOperations ?? []).OrderBy(o => o.Channel, StringComparer.Ordinal).ThenBy(o => o.Action, StringComparer.Ordinal))
        {
            if (!currentChannels.Contains(operation.Channel) || currentKeys.Contains((operation.Channel, operation.Action)))
                continue;

            yield return new ContractChange(
                "CW020",
                "ChannelOperationRemoved",
                ChangeSeverity.Breaking,
                new ChangeLocation(operation.Channel, operation.Action),
                $"Action {operation.Action} removed");
        }
    }
}
