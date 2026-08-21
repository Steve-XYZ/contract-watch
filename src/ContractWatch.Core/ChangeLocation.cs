namespace ContractWatch.Core;

public sealed record ChangeLocation(string Path, string? Method = null, string? JsonPointer = null);
