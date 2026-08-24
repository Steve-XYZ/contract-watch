using System.Text.Json.Nodes;

namespace ContractWatch.Core.Parsing;

public sealed class UnsupportedSpecException(string filePath, string detail)
    : Exception($"No se pudo cargar '{filePath}': {detail}");

public static class AsyncApiLoader
{
    public static ApiContract Parse(JsonObject root, string filePath)
    {
        var version = root["asyncapi"] is JsonValue value && value.TryGetValue<string>(out var parsed)
            ? parsed
            : throw new UnsupportedSpecException(filePath, "falta el campo 'asyncapi' con la versión del documento");

        return version.Split('.')[0] switch
        {
            "2" => MapV2(root, filePath),
            "3" => MapV3(root, filePath),
            _ => throw new UnsupportedSpecException(filePath, $"versión de AsyncAPI '{version}' no soportada (se soportan 2.x y 3.x)"),
        };
    }

    private static ApiContract MapV2(JsonObject root, string filePath)
    {
        var operations = new List<ApiMessageOperation>();

        if (root["channels"] is JsonObject channels)
        {
            foreach (var channelEntry in channels.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                if (channelEntry.Value is not JsonObject channel)
                    continue;

                foreach (var (action, direction) in new[] { ("publish", MessageDirection.Outbound), ("subscribe", MessageDirection.Inbound) })
                {
                    if (Resolve(channel[action], root, filePath) is not JsonObject operation)
                        continue;

                    operations.Add(new ApiMessageOperation(
                        channelEntry.Key,
                        action.ToUpperInvariant(),
                        direction,
                        Payload(operation["message"], root, filePath)));
                }
            }
        }

        return new ApiContract([], operations);
    }

    private static ApiContract MapV3(JsonObject root, string filePath)
    {
        var operations = new List<ApiMessageOperation>();

        if (root["channels"] is JsonObject channels)
        {
            foreach (var channelEntry in channels.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                if (Resolve(channelEntry.Value, root, filePath) is not JsonObject channel)
                    continue;

                var address = channel["address"] is JsonValue value && value.TryGetValue<string>(out var parsed) && parsed.Length > 0
                    ? parsed
                    : channelEntry.Key;

                if (channel["operations"] is not JsonObject channelOperations)
                    continue;

                foreach (var operationEntry in channelOperations.OrderBy(o => o.Key, StringComparer.Ordinal))
                {
                    if (Resolve(operationEntry.Value, root, filePath) is not JsonObject operation)
                        continue;

                    var action = operation["action"] is JsonValue actionValue && actionValue.TryGetValue<string>(out var parsedAction)
                        ? parsedAction.ToLowerInvariant()
                        : null;

                    if (action is not ("send" or "receive"))
                        continue;

                    operations.Add(new ApiMessageOperation(
                        address,
                        action.ToUpperInvariant(),
                        action == "send" ? MessageDirection.Outbound : MessageDirection.Inbound,
                        FirstPayload(operation["messages"], root, filePath)));
                }
            }
        }

        return new ApiContract([], operations);
    }

    private static ApiSchema? Payload(JsonNode? messageNode, JsonObject root, string filePath)
    {
        if (Resolve(messageNode, root, filePath) is not JsonObject message)
            return null;

        if (message["oneOf"] is JsonArray { Count: > 0 } alternatives)
        {
            foreach (var alternative in alternatives)
            {
                if (Resolve(alternative, root, filePath) is JsonObject member)
                    return MapSchema(member["payload"], root, filePath);
            }

            return null;
        }

        return MapSchema(message["payload"], root, filePath);
    }

    private static ApiSchema? FirstPayload(JsonNode? messagesNode, JsonObject root, string filePath)
    {
        if (messagesNode is not JsonArray messages)
            return null;

        foreach (var entry in messages)
        {
            if (Resolve(entry, root, filePath) is JsonObject message)
                return MapSchema(message["payload"], root, filePath);
        }

        return null;
    }

    private static JsonNode? Resolve(JsonNode? node, JsonObject root, string filePath)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);

        while (node?["$ref"] is JsonValue value && value.TryGetValue<string>(out var reference))
        {
            if (!reference.StartsWith("#/", StringComparison.Ordinal))
                throw new UnsupportedSpecException(filePath, $"solo se soportan $ref locales al documento ('$ref': '{reference}')");

            if (!visited.Add(reference))
                throw new UnsupportedSpecException(filePath, $"ciclo de $ref en '{reference}'");

            node = Navigate(root, reference[2..]) ?? throw new UnsupportedSpecException(filePath, $"$ref no resuelto: '{reference}'");
        }

        return node;
    }

    private static JsonNode? Navigate(JsonObject root, string pointer)
    {
        var current = root as JsonNode;

        foreach (var rawToken in pointer.Split('/'))
        {
            var token = rawToken.Replace("~1", "/").Replace("~0", "~");

            current = current switch
            {
                JsonObject obj => obj.TryGetPropertyValue(token, out var property) ? property : null,
                JsonArray array when int.TryParse(token, out var index) && index >= 0 && index < array.Count => array[index],
                _ => null,
            };

            if (current is null)
                return null;
        }

        return current;
    }

    private static ApiSchema? MapSchema(JsonNode? node, JsonObject root, string filePath)
    {
        if (Resolve(node, root, filePath) is not JsonObject schema)
            return null;

        var (kind, isNullable) = ParseType(schema);
        var properties = Properties(schema, root, filePath);

        return new ApiSchema(
            kind,
            isNullable,
            schema["format"] is JsonValue format && format.TryGetValue<string>(out var formatText) ? formatText : null,
            EnumValues(schema),
            RequiredProperties(schema),
            properties);
    }

    private static (SchemaKind? Kind, bool IsNullable) ParseType(JsonObject schema)
    {
        var nullable = schema["nullable"] is JsonValue flag && flag.TryGetValue<bool>(out var isNullableFlag) && isNullableFlag;

        switch (schema["type"])
        {
            case JsonValue value when value.TryGetValue<string>(out var text):
                return (KindOf(text), nullable || text == "null");
            case JsonArray options:
            {
                SchemaKind? kind = null;

                foreach (var option in options)
                {
                    if (option is not JsonValue item || !item.TryGetValue<string>(out var name))
                        continue;

                    if (name == "null")
                        nullable = true;
                    else
                        kind ??= KindOf(name);
                }

                return (kind, nullable);
            }
            default:
                return (null, nullable);
        }
    }

    private static SchemaKind? KindOf(string type) => type switch
    {
        "string" => SchemaKind.String,
        "number" => SchemaKind.Number,
        "integer" => SchemaKind.Integer,
        "boolean" => SchemaKind.Boolean,
        "array" => SchemaKind.Array,
        "object" => SchemaKind.Object,
        _ => null,
    };

    private static IReadOnlyList<string>? EnumValues(JsonObject schema) =>
        schema["enum"] is not JsonArray values || values.Count == 0
            ? null
            : [.. values.Where(v => v is not null).Select(RenderNode)];

    private static string RenderNode(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text)
        ? text
        : node.ToJsonString();

    private static IReadOnlySet<string>? RequiredProperties(JsonObject schema) =>
        schema["required"] is not JsonArray required || required.Count == 0
            ? null
            : new HashSet<string>(
                required.OfType<JsonValue>().Select(v => v.TryGetValue<string>(out var name) ? name : null).Where(n => n is not null).Select(n => n!),
                StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, ApiSchema>? Properties(JsonObject schema, JsonObject root, string filePath) =>
        schema["properties"] is not JsonObject properties || properties.Count == 0
            ? null
            : properties.ToDictionary(
                p => p.Key,
                p => MapSchema(p.Value, root, filePath) ?? new ApiSchema(null, false, null, null, null, null));
}
