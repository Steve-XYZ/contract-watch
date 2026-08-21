namespace ContractWatch.Core;

public sealed record ApiOperation(string Path, string Method);

public sealed record ApiContract(string OpenApiVersion, IReadOnlyList<ApiOperation> Operations);
