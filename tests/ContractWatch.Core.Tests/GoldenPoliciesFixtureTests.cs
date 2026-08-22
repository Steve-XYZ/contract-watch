using ContractWatch.Core;
using ContractWatch.Core.Comparison;
using ContractWatch.Core.Parsing;

namespace ContractWatch.Core.Tests;

public class GoldenPoliciesFixtureTests
{
    private static async Task<(ApiContract Previous, ApiContract Current)> LoadPoliciesAsync()
    {
        var previous = await OpenApiLoader.LoadAsync(FixturePath.Resolve("policies-v1.json"));
        var current = await OpenApiLoader.LoadAsync(FixturePath.Resolve("policies-v2.json"));
        return (previous, current);
    }

    [Fact]
    public async Task El_loader_marca_la_nullabilidad_nativa_de_openapi_3_1()
    {
        var (previous, current) = await LoadPoliciesAsync();

        var previousSettledAt = previous.Operations.Single(o => o.Path == "/payments").Responses["200"].JsonSchema!.Properties!["settledAt"];
        var currentSettledAt = current.Operations.Single(o => o.Path == "/payments").Responses["200"].JsonSchema!.Properties!["settledAt"];

        Assert.Equal(SchemaKind.String, previousSettledAt.Kind);
        Assert.True(previousSettledAt.IsNullable);
        Assert.False(currentSettledAt.IsNullable);
    }

    [Fact]
    public async Task El_pipeline_detecta_exactamente_los_cambios_de_politica_esperados()
    {
        var (previous, current) = await LoadPoliciesAsync();
        var result = TestContracts.Compare(previous, current);

        var expected = new HashSet<(string RuleId, string Path, string? Method, string Message, ChangeSeverity Severity)>
        {
            ("CW011", "/orders", "POST", "Required response property added: note", ChangeSeverity.PotentiallyBreaking),
            ("CW012", "/payments", "GET", "Response property settledAt changed: string|null → string", ChangeSeverity.PotentiallyBreaking),
            ("CW018", "/refunds", "GET", "Operation metadata updated", ChangeSeverity.Compatible),
        };

        var actual = result.Changes
            .Select(c => (c.RuleId, c.Location.Path, c.Location.Method, c.Message, c.Severity))
            .ToHashSet();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task El_resumen_refleja_dos_advertencias_y_un_compatible_sin_breaking()
    {
        var (previous, current) = await LoadPoliciesAsync();
        var result = TestContracts.Compare(previous, current);

        Assert.Equal(0, result.Count(ChangeSeverity.Breaking));
        Assert.Equal(2, result.Count(ChangeSeverity.PotentiallyBreaking));
        Assert.Equal(1, result.Count(ChangeSeverity.Compatible));
        Assert.False(result.FailsAt(ChangeSeverity.Breaking));
    }
}
