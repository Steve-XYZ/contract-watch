using ContractWatch.Core;

namespace ContractWatch.Core.Comparison;

internal static class OperationStructure
{
    public static bool Equal(ApiOperation previous, ApiOperation current)
    {
        return ParametersEqual(previous.Parameters, current.Parameters)
            && SchemaEqual(previous.RequestJsonSchema, current.RequestJsonSchema)
            && ResponsesEqual(previous.Responses, current.Responses);
    }

    private static bool ParametersEqual(IReadOnlyList<ApiParameter> previous, IReadOnlyList<ApiParameter> current)
    {
        var previousByKey = previous.ToDictionary(p => (p.Name, p.In));

        if (previousByKey.Count != current.Count)
            return false;

        foreach (var parameter in current)
        {
            if (!previousByKey.TryGetValue((parameter.Name, parameter.In), out var matchedParameter)
                || matchedParameter.IsRequired != parameter.IsRequired
                || !SchemaEqual(matchedParameter.Schema, parameter.Schema))
                return false;
        }

        return true;
    }

    private static bool ResponsesEqual(IReadOnlyDictionary<string, ApiResponse> previous, IReadOnlyDictionary<string, ApiResponse> current)
    {
        if (previous.Count != current.Count)
            return false;

        foreach (var (status, currentResponse) in current)
        {
            if (!previous.TryGetValue(status, out var previousResponse)
                || !SchemaEqual(previousResponse.JsonSchema, currentResponse.JsonSchema))
                return false;
        }

        return true;
    }

    private static bool SchemaEqual(ApiSchema? previous, ApiSchema? current)
    {
        if (ReferenceEquals(previous, current))
            return true;

        if (previous is null || current is null)
            return false;

        return previous.Kind == current.Kind
            && previous.IsNullable == current.IsNullable
            && string.Equals(previous.Format, current.Format, StringComparison.Ordinal)
            && EnumValuesEqual(previous.EnumValues, current.EnumValues)
            && RequiredEqual(previous.RequiredProperties, current.RequiredProperties)
            && PropertiesEqual(previous.Properties, current.Properties);
    }

    private static bool EnumValuesEqual(IReadOnlyList<string>? previous, IReadOnlyList<string>? current) =>
        HasValues(previous) == HasValues(current)
        && new HashSet<string>(previous ?? [], StringComparer.Ordinal).SetEquals(current ?? []);

    private static bool RequiredEqual(IReadOnlySet<string>? previous, IReadOnlySet<string>? current) =>
        HasValues(previous as IEnumerable<string>) == HasValues(current as IEnumerable<string>)
        && (previous ?? EmptySet).SetEquals(current ?? EmptySet);

    private static bool PropertiesEqual(IReadOnlyDictionary<string, ApiSchema>? previous, IReadOnlyDictionary<string, ApiSchema>? current)
    {
        if (HasValues(previous?.Keys) != HasValues(current?.Keys))
            return false;

        foreach (var (name, previousProperty) in previous ?? EmptyProperties)
        {
            if (!(current ?? EmptyProperties).TryGetValue(name, out var matchedProperty)
                || !SchemaEqual(previousProperty, matchedProperty))
                return false;
        }

        return true;
    }

    private static bool HasValues(IEnumerable<string>? values) => values is { } source && source.Any();

    private static readonly IReadOnlySet<string> EmptySet = new HashSet<string>();
    private static readonly IReadOnlyDictionary<string, ApiSchema> EmptyProperties = new Dictionary<string, ApiSchema>();
}
