namespace ContractWatch.Core;

public enum SchemaKind
{
    String,
    Number,
    Integer,
    Boolean,
    Array,
    Object,
}

public sealed record ApiSchema(
    SchemaKind? Kind,
    bool IsNullable,
    string? Format,
    IReadOnlyList<string>? EnumValues,
    IReadOnlySet<string>? RequiredProperties,
    IReadOnlyDictionary<string, ApiSchema>? Properties)
{
    public string RenderType() => Kind is null ? "unknown" : IsNullable
        ? $"{RenderKind(Kind)}|null"
        : RenderKind(Kind);

    private static string RenderKind(SchemaKind? kind) => kind switch
    {
        SchemaKind.String => "string",
        SchemaKind.Number => "number",
        SchemaKind.Integer => "integer",
        SchemaKind.Boolean => "boolean",
        SchemaKind.Array => "array",
        SchemaKind.Object => "object",
        _ => "unknown",
    };
}
