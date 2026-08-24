using ContractWatch.Core;
using ContractWatch.Core.Comparison;
using ContractWatch.Core.Parsing;

namespace ContractWatch.Core.Tests;

internal static class FixturePath
{
    public static string Resolve(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "examples", fileName)))
            directory = directory.Parent!;

        return directory is null
            ? throw new FileNotFoundException($"No se encontró examples/{fileName} hacia arriba desde {AppContext.BaseDirectory}")
            : Path.Combine(directory.FullName, "examples", fileName);
    }
}

internal static class TestContracts
{
    public static ApiOperation Operation(
        string path,
        string method,
        ApiParameter[]? parameters = null,
        ApiSchema? requestSchema = null,
        Dictionary<string, ApiResponse>? responses = null) => new(
        path,
        method,
        parameters ?? [],
        requestSchema,
        responses ?? new Dictionary<string, ApiResponse>());

    public static ApiParameter Parameter(string name, string @in, bool required) =>
        new(name, @in, required, null);

    public static ApiMessageOperation MessageOperation(string channel, string action, MessageDirection direction, ApiSchema? payload = null) =>
        new(channel, action, direction, payload);

    public static ApiResponse Response(string statusCode) => new(statusCode, null);

    public static ApiSchema ObjectSchema(string[]? required = null, params (string Name, ApiSchema Schema)[] properties) => new(
        SchemaKind.Object,
        false,
        null,
        null,
        required is { Length: > 0 } ? new HashSet<string>(required) : null,
        properties.Length > 0
            ? properties.ToDictionary(p => p.Name, p => p.Schema)
            : null);

    public static ApiSchema StringSchema(params string[] enumValues) => new(
        SchemaKind.String,
        false,
        null,
        enumValues.Length > 0 ? enumValues : null,
        null,
        null);

    public static ComparisonResult Compare(ApiContract previous, ApiContract current) =>
        new ContractComparer().Compare(previous, current);
}
