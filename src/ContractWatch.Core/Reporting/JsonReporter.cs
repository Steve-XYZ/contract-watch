using System.Text.Json;
using System.Text.Json.Serialization;
using ContractWatch.Core.Comparison;
using ContractWatch.Core.Rules;

namespace ContractWatch.Core.Reporting;

public static class JsonReporter
{
    public const string ToolVersion = "0.1.0";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Render(ComparisonResult result) => Render(result, null);

    public static string Render(ComparisonResult result, IReadOnlyList<AffectedConsumer>? impact)
    {
        var report = new Report(
            "contractwatch",
            ToolVersion,
            new Summary(result.Count(ChangeSeverity.Breaking), result.Count(ChangeSeverity.PotentiallyBreaking), result.Count(ChangeSeverity.Compatible)),
            [.. result.Changes.Select(ToChangeEntry)],
            impact is null ? null : [.. impact.Select(ToAffectedEntry)]);

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
        change.NewValue);

    private sealed record Report(
        string Tool,
        string Version,
        Summary Summary,
        ChangeEntry[] Changes,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AffectedConsumerEntry[]? AffectedConsumers = null);

    private sealed record AffectedConsumerEntry(string Service, ConfidenceLevel Confidence, int Changes);

    private sealed record Summary(int Breaking, int PotentiallyBreaking, int Compatible);

    private sealed record ChangeEntry(
        string RuleId,
        string RuleName,
        ChangeSeverity Severity,
        Location Location,
        string Message,
        string? OldValue,
        string? NewValue);

    private sealed record Location(string Path, string? Method, string? JsonPointer);
}
