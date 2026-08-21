namespace ContractWatch.Core;

public sealed record ContractChange(
    string RuleId,
    string RuleName,
    ChangeSeverity Severity,
    ChangeLocation Location,
    string Message,
    string? OldValue = null,
    string? NewValue = null);
