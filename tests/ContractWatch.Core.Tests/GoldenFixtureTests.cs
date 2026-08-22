using ContractWatch.Core;
using ContractWatch.Core.Comparison;
using ContractWatch.Core.Parsing;

namespace ContractWatch.Core.Tests;

public class GoldenFixtureTests
{
    private static async Task<ComparisonResult> CompareExamplesAsync()
    {
        var previous = await OpenApiLoader.LoadAsync(FixturePath.Resolve("v1.json"));
        var current = await OpenApiLoader.LoadAsync(FixturePath.Resolve("v2.json"));
        return TestContracts.Compare(previous, current);
    }

    [Fact]
    public async Task El_pipeline_detecta_exactamente_los_cambios_esperados()
    {
        var result = await CompareExamplesAsync();

        var expected = new HashSet<(string RuleId, string Path, string? Method, string Message)>
        {
            ("CW001", "/legacy/orders", null, "Endpoint removed"),
            ("CW002", "/orders/{id}", "DELETE", "Method DELETE removed"),
            ("CW003", "/orders", "POST", "Parameter became required: idempotency-key"),
            ("CW003", "/orders", "POST", "Required parameter added: x-trace-id"),
            ("CW004", "/orders", "POST", "Required request property added: currency"),
            ("CW007", "/orders", "POST", "Response status removed: 404"),
            ("CW013", "/refunds", null, "Endpoint added"),
            ("CW014", "/orders", "GET", "Optional parameter added: locale"),
            ("CW015", "/orders", "POST", "Optional property added: note"),
            ("CW017", "/orders/{id}", "GET", "Response status added: 404"),
        };

        var actual = result.Changes
            .Select(c => (c.RuleId, c.Location.Path, c.Location.Method, c.Message))
            .ToHashSet();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task El_resumen_y_los_exit_codes_reflejan_la_severidad()
    {
        var result = await CompareExamplesAsync();

        Assert.Equal(6, result.Count(ChangeSeverity.Breaking));
        Assert.Equal(0, result.Count(ChangeSeverity.PotentiallyBreaking));
        Assert.Equal(4, result.Count(ChangeSeverity.Compatible));

        Assert.True(result.FailsAt(ChangeSeverity.Breaking));
        Assert.True(result.FailsAt(ChangeSeverity.PotentiallyBreaking));
        Assert.False(result.FailsAt(null));
    }
}
