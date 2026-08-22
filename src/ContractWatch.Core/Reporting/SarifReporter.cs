using System.Text.Json;
using System.Text.Json.Serialization;
using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Reporting;

public static class SarifReporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string Render(ComparisonResult result, string artifactUri)
    {
        var findings = result.Changes
            .Where(c => c.Severity != ChangeSeverity.Compatible)
            .ToList();

        var rules = findings
            .Select(c => (c.RuleId, c.RuleName))
            .Distinct()
            .OrderBy(r => r.RuleId, StringComparer.Ordinal)
            .Select((rule, index) => (rule.RuleId, rule.RuleName, Index: index))
            .ToList();

        var ruleIndices = rules.ToDictionary(r => r.RuleId, r => r.Index);

        var sarif = new SarifLog(
            "https://json.schemastore.org/sarif-2.1.0.json",
            "2.1.0",
            [
                new SarifRun(
                    new SarifTool(new SarifDriver(
                        "contractwatch",
                        JsonReporter.ToolVersion,
                        "https://github.com/Steve-XYZ/contract-watch",
                        [.. rules.Select(r => new SarifRule(r.RuleId, r.RuleName))])),
                    [.. findings.Select(f => ToSarifResult(f, artifactUri, ruleIndices[f.RuleId]))]),
            ]);

        return JsonSerializer.Serialize(sarif, Options);
    }

    private static SarifResult ToSarifResult(ContractChange change, string artifactUri, int ruleIndex) => new(
        change.RuleId,
        ruleIndex,
        change.Severity == ChangeSeverity.Breaking ? "error" : "warning",
        new SarifMessage(change.Message),
        [new SarifLocation(new SarifPhysicalLocation(new SarifArtifactLocation(artifactUri)))],
        new Dictionary<string, string?>
        {
            ["severity"] = change.Severity.ToString(),
            ["path"] = change.Location.Path,
            ["method"] = change.Location.Method,
        });

    private sealed record SarifLog(
        [property: JsonPropertyName("$schema")] string Schema,
        string Version,
        SarifRun[] Runs);

    private sealed record SarifRun(SarifTool Tool, SarifResult[] Results);

    private sealed record SarifTool(SarifDriver Driver);

    private sealed record SarifDriver(string Name, string Version, string InformationUri, SarifRule[] Rules);

    private sealed record SarifRule(string Id, string Name);

    private sealed record SarifResult(
        string RuleId,
        int RuleIndex,
        string Level,
        SarifMessage Message,
        SarifLocation[] Locations,
        IReadOnlyDictionary<string, string?> Properties);

    private sealed record SarifMessage(string Text);

    private sealed record SarifLocation(SarifPhysicalLocation PhysicalLocation);

    private sealed record SarifPhysicalLocation(SarifArtifactLocation ArtifactLocation);

    private sealed record SarifArtifactLocation(string Uri);
}
