using ContractWatch.Core;
using ContractWatch.Core.Comparison;
using ContractWatch.Core.Parsing;

namespace ContractWatch.Core.Tests;

public class GoldenTypesFixtureTests
{
    private static async Task<ComparisonResult> CompareTypesExamplesAsync()
    {
        var previous = await OpenApiLoader.LoadAsync(FixturePath.Resolve("types-v1.json"));
        var current = await OpenApiLoader.LoadAsync(FixturePath.Resolve("types-v2.json"));
        return TestContracts.Compare(previous, current);
    }

    [Fact]
    public async Task El_pipeline_detecta_exactamente_los_cambios_de_tipo_esperados()
    {
        var result = await CompareTypesExamplesAsync();

        var expected = new HashSet<(string RuleId, string Path, string? Method, string Message)>
        {
            ("CW005", "/orders", "POST", "Parameter limit changed: integer → string"),
            ("CW008", "/orders/{id}", "GET", "Response property amount changed: number → string"),
            ("CW009", "/orders/{id}", "GET", "Response property removed: createdAt"),
        };

        var actual = result.Changes
            .Select(c => (c.RuleId, c.Location.Path, c.Location.Method, c.Message))
            .ToHashSet();

        Assert.Equal(expected, actual);
    }
}
