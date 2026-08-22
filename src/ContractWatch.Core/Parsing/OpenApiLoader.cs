using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;

namespace ContractWatch.Core.Parsing;

public sealed class ContractLoadException(string filePath, IEnumerable<string> errors)
    : Exception($"No se pudo cargar '{filePath}' como OpenAPI: {string.Join("; ", errors)}");

public static class OpenApiLoader
{
    public static async Task<ApiContract> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var result = await OpenApiDocument.LoadAsync(filePath, new OpenApiReaderSettings(), cancellationToken);
        var errors = result.Diagnostic?.Errors;

        if (result.Document is null || errors is { Count: > 0 })
            throw new ContractLoadException(filePath, errors?.Select(e => e.Message) ?? ["documento ilegible"]);

        return Map(result.Document);
    }

    private static ApiContract Map(OpenApiDocument document)
    {
        var operations = new List<ApiOperation>();

        if (document.Paths is not null)
        {
            foreach (var pathEntry in document.Paths)
            {
                if (pathEntry.Value is not { } pathItem)
                    continue;

                if (pathItem.Operations is not { } operationsByMethod)
                    continue;

                foreach (var operationEntry in operationsByMethod)
                {
                    if (operationEntry.Key is not { } httpMethod || operationEntry.Value is not { } operation)
                        continue;

                    operations.Add(MapOperation(pathEntry.Key, httpMethod.Method.ToUpperInvariant(), operation, pathItem.Parameters));
                }
            }
        }

        operations.Sort((a, b) =>
        {
            var byPath = string.CompareOrdinal(a.Path, b.Path);
            return byPath != 0 ? byPath : string.CompareOrdinal(a.Method, b.Method);
        });

        return new ApiContract(operations);
    }

    private static ApiOperation MapOperation(string path, string method, OpenApiOperation operation, IEnumerable<IOpenApiParameter>? pathLevelParameters)
    {
        var parameters = new Dictionary<(string Name, string In), IOpenApiParameter>();

        foreach (var parameter in pathLevelParameters ?? [])
            parameters[(parameter.Name ?? string.Empty, InToString(parameter.In))] = parameter;

        foreach (var parameter in operation.Parameters ?? [])
            parameters[(parameter.Name ?? string.Empty, InToString(parameter.In))] = parameter;

        return new ApiOperation(
            path,
            method,
            [.. parameters.Select(p => new ApiParameter(p.Key.Name, p.Key.In, p.Value.Required == true, MapSchema(p.Value.Schema)))],
            RequestJsonSchema(operation.RequestBody),
            MapResponses(operation.Responses),
            operation.Summary,
            operation.Description);
    }

    private static ApiSchema? RequestJsonSchema(IOpenApiRequestBody? requestBody) =>
        JsonContentSchema(requestBody?.Content);

    private static IReadOnlyDictionary<string, ApiResponse> MapResponses(OpenApiResponses? responses)
    {
        var mapped = new Dictionary<string, ApiResponse>();

        if (responses is null)
            return mapped;

        foreach (var responseEntry in responses)
            mapped[responseEntry.Key] = new ApiResponse(responseEntry.Key, JsonContentSchema(responseEntry.Value.Content));

        return mapped;
    }

    private static ApiSchema? JsonContentSchema(IDictionary<string, IOpenApiMediaType>? content) =>
        content is not null && content.TryGetValue("application/json", out var mediaType)
            ? MapSchema(mediaType.Schema)
            : null;

    private static ApiSchema? MapSchema(IOpenApiSchema? schema)
    {
        if (schema is null)
            return null;

        var target = schema is OpenApiSchemaReference reference ? reference.Target : schema;

        if (target is null)
            return null;

        return new ApiSchema(
            PrimaryKind(target.Type),
            target.Type is { } type && type.HasFlag(JsonSchemaType.Null),
            target.Format,
            EnumValues(target),
            SetOrNull(target.Required),
            Properties(target));
    }

    private static IReadOnlyList<string>? EnumValues(IOpenApiSchema schema) => schema.Enum switch
    {
        null or { Count: 0 } => null,
        { } values => [.. values.Select(RenderNode)],
    };

    private static string RenderNode(JsonNode node) => node is JsonValue value && value.TryGetValue<string>(out var text)
        ? text
        : node.ToJsonString();

    private static IReadOnlyDictionary<string, ApiSchema>? Properties(IOpenApiSchema schema) => schema.Properties switch
    {
        null or { Count: 0 } => null,
        { } properties => properties.ToDictionary(p => p.Key, p => MapSchema(p.Value)!),
    };

    private static IReadOnlySet<string>? SetOrNull(ISet<string>? values) =>
        values is null or { Count: 0 } ? null : new HashSet<string>(values);

    private static SchemaKind? PrimaryKind(JsonSchemaType? type)
    {
        var t = (type ?? JsonSchemaType.Null) & ~JsonSchemaType.Null;

        if (t == JsonSchemaType.Null)
            return null;

        if (t.HasFlag(JsonSchemaType.Array)) return SchemaKind.Array;
        if (t.HasFlag(JsonSchemaType.Object)) return SchemaKind.Object;
        if (t.HasFlag(JsonSchemaType.Integer)) return SchemaKind.Integer;
        if (t.HasFlag(JsonSchemaType.Number)) return SchemaKind.Number;
        if (t.HasFlag(JsonSchemaType.String)) return SchemaKind.String;
        if (t.HasFlag(JsonSchemaType.Boolean)) return SchemaKind.Boolean;
        return null;
    }

    private static string InToString(ParameterLocation? location) =>
        location?.ToString().ToLowerInvariant() ?? string.Empty;
}
