namespace ContractWatch.Core;

public sealed record ApiParameter(string Name, string In, bool IsRequired, ApiSchema? Schema);

public sealed record ApiResponse(string StatusCode, ApiSchema? JsonSchema);

public sealed record ApiOperation(
    string Path,
    string Method,
    IReadOnlyList<ApiParameter> Parameters,
    ApiSchema? RequestJsonSchema,
    IReadOnlyDictionary<string, ApiResponse> Responses,
    string? Summary = null,
    string? Description = null);

public enum MessageDirection
{
    Outbound,
    Inbound,
}

public sealed record ApiMessageOperation(
    string Channel,
    string Action,
    MessageDirection Direction,
    ApiSchema? PayloadSchema);

public sealed record ApiContract(
    IReadOnlyList<ApiOperation> Operations,
    IReadOnlyList<ApiMessageOperation>? MessageOperations = null);
