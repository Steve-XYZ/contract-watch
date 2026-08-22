using ContractWatch.Core;

namespace ContractWatch.Core.Comparison;

internal static class EnumPairing
{
    public static IEnumerable<(string Name, IReadOnlyList<string> Previous, IReadOnlyList<string> Current)> MatchedInput(ApiOperation previousOperation, ApiOperation currentOperation)
    {
        var previousParameters = previousOperation.Parameters.ToDictionary(p => (p.Name, p.In));

        foreach (var parameter in currentOperation.Parameters.OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (previousParameters.TryGetValue((parameter.Name, parameter.In), out var previousParameter)
                && previousParameter.Schema?.EnumValues is { } previousValues
                && parameter.Schema?.EnumValues is { } currentValues)
                yield return (parameter.Name, previousValues, currentValues);
        }

        foreach (var (name, previousValues, currentValues) in MatchedProperties(previousOperation.RequestJsonSchema?.Properties, currentOperation.RequestJsonSchema?.Properties))
            yield return (name, previousValues, currentValues);
    }

    public static IEnumerable<(string Status, string Name, IReadOnlyList<string> Previous, IReadOnlyList<string> Current)> MatchedResponse(ApiOperation previousOperation, ApiOperation currentOperation)
    {
        foreach (var status in currentOperation.Responses.Keys.Order(StringComparer.Ordinal))
        {
            if (!previousOperation.Responses.TryGetValue(status, out var previousResponse)
                || previousResponse.JsonSchema is not { } previousSchema
                || currentOperation.Responses[status].JsonSchema is not { } currentSchema)
                continue;

            foreach (var (name, previousValues, currentValues) in MatchedProperties(previousSchema.Properties, currentSchema.Properties))
                yield return (status, name, previousValues, currentValues);
        }
    }

    public static bool Narrowed(IReadOnlyList<string> previous, IReadOnlyList<string> current) =>
        current.All(previous.Contains) && DistinctCount(previous) > DistinctCount(current);

    public static bool Widened(IReadOnlyList<string> previous, IReadOnlyList<string> current) =>
        Narrowed(current, previous);

    public static string Render(IReadOnlyList<string> values) => string.Join(", ", values);

    private static IEnumerable<(string Name, IReadOnlyList<string> Previous, IReadOnlyList<string> Current)> MatchedProperties(IReadOnlyDictionary<string, ApiSchema>? previousProperties, IReadOnlyDictionary<string, ApiSchema>? currentProperties)
    {
        if (previousProperties is null || currentProperties is null)
            yield break;

        foreach (var (name, currentProperty) in currentProperties.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (currentProperty.EnumValues is not { } currentValues
                || !previousProperties.TryGetValue(name, out var previousProperty)
                || previousProperty.EnumValues is not { } previousValues)
                continue;

            yield return (name, previousValues, currentValues);
        }
    }

    private static int DistinctCount(IReadOnlyList<string> values) => new HashSet<string>(values, StringComparer.Ordinal).Count;
}
