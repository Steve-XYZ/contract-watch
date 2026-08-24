namespace ContractWatch.Core.Rules;

public sealed class ChannelRemoved : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        var currentChannels = (current.MessageOperations ?? []).Select(o => o.Channel).ToHashSet(StringComparer.Ordinal);

        foreach (var channel in (previous.MessageOperations ?? []).Select(o => o.Channel).Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal))
        {
            if (currentChannels.Contains(channel))
                continue;

            yield return new ContractChange(
                "CW019",
                "ChannelRemoved",
                ChangeSeverity.Breaking,
                new ChangeLocation(channel),
                "Channel removed");
        }
    }
}
