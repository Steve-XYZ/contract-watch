using System.Text.Json;
using System.Text.Json.Serialization;
using ContractWatch.Core.Comparison;
using ContractWatch.Core.Explanations;
using ContractWatch.Core.Rules;

namespace ContractWatch.Core;

public sealed record ContractPolicy(
    string? FailOn,
    IReadOnlyDictionary<string, ChangeSeverity> SeverityOverrides,
    string? Explain = null,
    string? ExplainModel = null);

public sealed class PolicyFileException : Exception
{
    public PolicyFileException(string path, string problem)
        : base($"Política inválida en {path}: {problem}")
    {
    }
}

public static class PolicyFile
{
    public const string DefaultFileName = ".contractwatch.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly IReadOnlySet<string> KnownRuleIds = new HashSet<string>(
        RuleCatalog.Default.Select((_, index) => $"CW{index + 1:000}"),
        StringComparer.Ordinal);

    public static ContractPolicy LoadOrDefault(string? explicitPath, string? directory = null)
    {
        var path = explicitPath ?? Path.Combine(directory ?? Directory.GetCurrentDirectory(), DefaultFileName);
        return File.Exists(path) ? Load(path) : Empty;
    }

    public static ContractPolicy Load(string path)
    {
        PolicyDto dto;

        try
        {
            dto = JsonSerializer.Deserialize<PolicyDto>(File.ReadAllText(path), Options)
                  ?? throw new JsonException("el documento está vacío");
        }
        catch (JsonException ex)
        {
            throw new PolicyFileException(path, $"JSON malformado ({ex.Message})");
        }

        var failOn = ValidateFailOn(path, dto.FailOn);
        var overrides = ValidateOverrides(path, dto.SeverityOverrides);
        var explain = ValidateExplain(path, dto.Explain);

        return new ContractPolicy(failOn, overrides, explain, dto.ExplainModel);
    }

    public static ChangeSeverity? ResolveThreshold(string? flagValue, string? policyFailOn)
    {
        var effective = flagValue ?? policyFailOn ?? "breaking";
        return effective switch
        {
            "breaking" => ChangeSeverity.Breaking,
            "potentially" => ChangeSeverity.PotentiallyBreaking,
            _ => null,
        };
    }

    public static ComparisonResult Apply(ComparisonResult result, ContractPolicy policy)
    {
        if (policy.SeverityOverrides.Count == 0)
            return result;

        var remapped = result.Changes
            .Select(change => policy.SeverityOverrides.TryGetValue(change.RuleId, out var severity)
                ? change with { Severity = severity }
                : change);

        return new ComparisonResult(ComparisonOrdering.Apply(remapped));
    }

    private static ContractPolicy Empty { get; } = new(null, new Dictionary<string, ChangeSeverity>());

    private static string? ValidateFailOn(string path, string? failOn)
    {
        if (failOn is null or "breaking" or "potentially" or "never")
            return failOn;

        throw new PolicyFileException(path, $"failOn desconocido '{failOn}' (breaking|potentially|never)");
    }

    private static string? ValidateExplain(string path, string? explain)
    {
        if (explain is null || ExplanationProviders.Known.Contains(explain))
            return explain;

        throw new PolicyFileException(path, $"explain desconocido '{explain}' (fake|openai)");
    }

    private static IReadOnlyDictionary<string, ChangeSeverity> ValidateOverrides(string path, Dictionary<string, string>? overrides)
    {
        if (overrides is null || overrides.Count == 0)
            return new Dictionary<string, ChangeSeverity>();

        var severities = new Dictionary<string, ChangeSeverity>(StringComparer.Ordinal);

        foreach (var (ruleId, value) in overrides)
        {
            if (!KnownRuleIds.Contains(ruleId))
                throw new PolicyFileException(path, $"regla desconocida '{ruleId}' en severityOverrides (se esperan {KnownRuleIds.Min()}..{KnownRuleIds.Max()})");

            var severity = value switch
            {
                "breaking" => ChangeSeverity.Breaking,
                "potentially" => ChangeSeverity.PotentiallyBreaking,
                "compatible" => ChangeSeverity.Compatible,
                _ => throw new PolicyFileException(path, $"severidad inválida '{value}' para {ruleId} (breaking|potentially|compatible)"),
            };

            severities[ruleId] = severity;
        }

        return severities;
    }

    private sealed record PolicyDto(string? FailOn, Dictionary<string, string>? SeverityOverrides, string? Explain, string? ExplainModel);
}
