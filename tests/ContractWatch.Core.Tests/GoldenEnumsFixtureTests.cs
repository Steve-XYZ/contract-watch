using ContractWatch.Core;
using ContractWatch.Core.Comparison;
using ContractWatch.Core.Parsing;

namespace ContractWatch.Core.Tests;

public class GoldenEnumsFixtureTests
{
    private static async Task<ComparisonResult> CompareEnumsExamplesAsync()
    {
        var previous = await OpenApiLoader.LoadAsync(FixturePath.Resolve("enums-v1.json"));
        var current = await OpenApiLoader.LoadAsync(FixturePath.Resolve("enums-v2.json"));
        return TestContracts.Compare(previous, current);
    }

    [Fact]
    public async Task El_pipeline_detecta_exactamente_las_asimetrias_de_enum_esperadas()
    {
        var result = await CompareEnumsExamplesAsync();

        var expected = new HashSet<(string RuleId, string Path, string? Method, string Message, ChangeSeverity Severity)>
        {
            ("CW006", "/payments", "POST", "Request enum narrowed: currency: USD, EUR → USD", ChangeSeverity.Breaking),
            ("CW010", "/bets/{id}", "GET", "Response enum widened: status: PAID, FAILED → PAID, FAILED, PENDING", ChangeSeverity.PotentiallyBreaking),
            ("CW016", "/bets", "POST", "Request enum widened: region: EU → EU, LATAM", ChangeSeverity.Compatible),
        };

        var actual = result.Changes
            .Select(c => (c.RuleId, c.Location.Path, c.Location.Method, c.Message, c.Severity))
            .ToHashSet();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task El_resumen_refleja_una_asimetria_por_severidad()
    {
        var result = await CompareEnumsExamplesAsync();

        Assert.Equal(1, result.Count(ChangeSeverity.Breaking));
        Assert.Equal(1, result.Count(ChangeSeverity.PotentiallyBreaking));
        Assert.Equal(1, result.Count(ChangeSeverity.Compatible));
        Assert.True(result.FailsAt(ChangeSeverity.Breaking));
    }
}
