using ContractWatch.Core;
using ContractWatch.Core.Comparison;
using ContractWatch.Core.Parsing;

namespace ContractWatch.Core.Tests;

public class GoldenAsyncApiFixtureTests
{
    private static async Task<ComparisonResult> CompareExamplesAsync()
    {
        var previous = await SpecLoader.LoadAsync(FixturePath.Resolve("asyncapi-v1.json"));
        var current = await SpecLoader.LoadAsync(FixturePath.Resolve("asyncapi-v2.json"));
        SpecLoader.EnsureSameKind(previous, current, "v1", "v2");

        return TestContracts.Compare(previous.Contract, current.Contract);
    }

    [Fact]
    public async Task El_pipeline_detecta_exactamente_los_cambios_esperados()
    {
        var result = await CompareExamplesAsync();

        var expected = new HashSet<(string RuleId, string Channel, string? Action, string Message)>
        {
            ("CW019", "legacy/audit", null, "Channel removed"),
            ("CW020", "orders/events", "PUBLISH", "Action PUBLISH removed"),
            ("CW021", "payments/instructions", "SUBSCRIBE", "Required message property added: referenceId"),
            ("CW025", "payments/instructions", "SUBSCRIBE", "Message enum narrowed: method: card, transfer → card"),
            ("CW022", "shipments/status", "PUBLISH", "Message property eta changed: string → number"),
            ("CW024", "shipments/status", "PUBLISH", "Message enum widened: state: pending, shipped, delivered → pending, shipped, delivered, cancelled"),
            ("CW023", "users/signedup", "PUBLISH", "Message property removed: email"),
            ("CW026", "refunds/issued", null, "Channel added"),
            ("CW027", "users/signedup", "PUBLISH", "Optional message property added: marketingOptIn"),
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
        Assert.Equal(1, result.Count(ChangeSeverity.PotentiallyBreaking));
        Assert.Equal(2, result.Count(ChangeSeverity.Compatible));

        Assert.True(result.FailsAt(ChangeSeverity.Breaking));
        Assert.True(result.FailsAt(ChangeSeverity.PotentiallyBreaking));
        Assert.False(result.FailsAt(null));
    }

    [Fact]
    public async Task Cada_cambio_lleva_su_sugerencia_de_remediacion()
    {
        var result = await CompareExamplesAsync();

        Assert.All(result.Changes, c => Assert.False(string.IsNullOrWhiteSpace(c.Suggestion)));
    }
}
