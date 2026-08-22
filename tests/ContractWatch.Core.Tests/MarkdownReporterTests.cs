using ContractWatch.Core;
using ContractWatch.Core.Comparison;
using ContractWatch.Core.Reporting;

namespace ContractWatch.Core.Tests;

public class MarkdownReporterTests
{
    [Fact]
    public void Con_cambios_breaking_el_veredicto_es_failed()
    {
        var result = new ComparisonResult(
        [
            new("CW004", "RequiredPropertyAdded", ChangeSeverity.Breaking,
                new ChangeLocation("/orders", "POST"), "Required request property added: customerId"),
            new("CW015", "OptionalPropertyAdded", ChangeSeverity.Compatible,
                new ChangeLocation("/orders", "POST"), "Optional property added: metadata"),
        ]);

        var markdown = MarkdownReporter.Render(result);

        Assert.Contains("## API compatibility: FAILED", markdown);
        Assert.Contains("This PR introduces **1 breaking** contract changes.", markdown);
        Assert.Contains("| ✗ Breaking | `POST /orders` | Required request property added: customerId | CW004 |", markdown);
        Assert.Contains("| ✓ Compatible | `POST /orders` | Optional property added: metadata | CW015 |", markdown);
        Assert.EndsWith("1 breaking · 0 potentially breaking · 1 compatible", markdown);
    }

    [Fact]
    public void Solo_potentially_breaking_produce_warning()
    {
        var result = new ComparisonResult(
        [
            new("CW010", "ResponseEnumWidened", ChangeSeverity.PotentiallyBreaking,
                new ChangeLocation("/payments", "GET"), "Response enum widened: status"),
        ]);

        var markdown = MarkdownReporter.Render(result);

        Assert.Contains("## API compatibility: WARNING", markdown);
        Assert.Contains("No breaking contract changes detected.", markdown);
        Assert.Contains("⚠ Potentially breaking", markdown);
    }

    [Fact]
    public void Sin_cambios_produce_passed()
    {
        var markdown = MarkdownReporter.Render(new ComparisonResult([]));

        Assert.Contains("## API compatibility: PASSED", markdown);
        Assert.DoesNotContain("| ✗ Breaking |", markdown);
        Assert.EndsWith("0 breaking · 0 potentially breaking · 0 compatible", markdown);
    }

    [Fact]
    public void Cambios_sin_metodo_se_renderizan_solo_con_path()
    {
        var result = new ComparisonResult(
        [
            new("CW001", "EndpointRemoved", ChangeSeverity.Breaking,
                new ChangeLocation("/legacy/orders"), "Endpoint removed"),
        ]);

        var markdown = MarkdownReporter.Render(result);

        Assert.Contains("| ✗ Breaking | `/legacy/orders` | Endpoint removed | CW001 |", markdown);
    }

    [Fact]
    public void Escapa_pipes_para_no_romper_la_tabla()
    {
        var result = new ComparisonResult(
        [
            new("CW008", "ResponsePropertyTypeChanged", ChangeSeverity.Breaking,
                new ChangeLocation("/x|y", "GET"), "Response property changed: a|b → c"),
        ]);

        var markdown = MarkdownReporter.Render(result);

        Assert.Contains("`GET /x\\|y`", markdown);
        Assert.Contains("a\\|b → c", markdown);
    }
}
