using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ContractWatch.Core.Comparison;
using ContractWatch.Core.Rules;

namespace ContractWatch.Core.Reporting;

public static class JsonReporter
{
    public static string ToolVersion { get; } = DeriveToolVersion();

    private static string DeriveToolVersion()
    {
        var assembly = typeof(JsonReporter).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrEmpty(informational))
        {
            return assembly.GetName().Version!.ToString(3);
        }

        var metadataSeparator = informational.IndexOf('+');
        return metadataSeparator < 0 ? informational : informational[..metadataSeparator];
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Render(ComparisonResult result) => Render(result, null);

    public static string Render(ComparisonResult result, IReadOnlyList<AffectedConsumer>? impact) => Render(result, impact, null);

    public static string Render(ComparisonResult result, IReadOnlyList<AffectedConsumer>? impact, ReportMeta? meta)
    {
        var report = new Report(
            "contractwatch",
            ToolVersion,
            new Summary(result.Count(ChangeSeverity.Breaking), result.Count(ChangeSeverity.PotentiallyBreaking), result.Count(ChangeSeverity.Compatible)),
            [.. result.Changes.Select(ToChangeEntry)],
            impact is null ? null : [.. impact.Select(ToAffectedEntry)],
            meta);

        return JsonSerializer.Serialize(report, Options);
    }

    private static AffectedConsumerEntry ToAffectedEntry(AffectedConsumer consumer) =>
        new(consumer.Service, consumer.Confidence, consumer.Changes);

    private static ChangeEntry ToChangeEntry(ContractChange change) => new(
        change.RuleId,
        change.RuleName,
        change.Severity,
        new Location(change.Location.Path, change.Location.Method, change.Location.JsonPointer),
        change.Message,
        change.OldValue,
        change.NewValue,
        change.Suggestion,
        change.Explanation);

    private sealed record Report(
        string Tool,
        string Version,
        Summary Summary,
        ChangeEntry[] Changes,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AffectedConsumerEntry[]? AffectedConsumers = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReportMeta? Meta = null);

    private sealed record AffectedConsumerEntry(string Service, ConfidenceLevel Confidence, int Changes);

    private sealed record Summary(int Breaking, int PotentiallyBreaking, int Compatible);

    private sealed record ChangeEntry(
        string RuleId,
        string RuleName,
        ChangeSeverity Severity,
        Location Location,
        string Message,
        string? OldValue,
        string? NewValue,
        string? Suggestion,
        string? Explanation);

    private sealed record Location(string Path, string? Method, string? JsonPointer);
}

public record ReportMeta(string SavedAt, string Command, IReadOnlyList<string> Inputs);
