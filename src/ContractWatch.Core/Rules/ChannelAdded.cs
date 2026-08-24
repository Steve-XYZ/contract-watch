namespace ContractWatch.Core.Rules;

public sealed class ChannelAdded : IContractRule
{
    public IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current)
    {
        var previousChannels = (previous.MessageOperations ?? []).Select(o => o.Channel).ToHashSet(StringComparer.Ordinal);

        foreach (var channel in (current.MessageOperations ?? []).Select(o => o.Channel).Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal))
        {
            if (previousChannels.Contains(channel))
                continue;

            yield return new ContractChange(
                "CW026",
                "ChannelAdded",
                ChangeSeverity.Compatible,
                new ChangeLocation(channel),
                "Channel added");
        }
    }
}
