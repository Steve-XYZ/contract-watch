using System.Reflection;
using System.Text.Json;
using ContractWatch.Core;
using ContractWatch.Core.Comparison;
using ContractWatch.Core.Reporting;

namespace ContractWatch.Core.Tests;

public class JsonReporterTests
{
    [Fact]
    public void La_version_reportada_deriva_del_ensamblado_y_no_esta_hardcodeada()
    {
        var assembly = typeof(JsonReporter).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        var expected = informational.Contains('+') ? informational.Split('+')[0] : informational;

        Assert.Equal(expected, JsonReporter.ToolVersion);
        Assert.NotEqual("0.0.0", JsonReporter.ToolVersion);
    }

    [Fact]
    public void Renderiza_json_estable_con_resumen_y_cambios_en_camel_case()
    {
        var changes = new List<ContractChange>
        {
            new("CW004", "RequiredPropertyAdded", ChangeSeverity.Breaking,
                new ChangeLocation("/orders", "POST", "/paths/~1orders/post/requestBody"),
                "Required request property added: currency",
                OldValue: null, NewValue: "currency"),
            new("CW010", "EnumWidened", ChangeSeverity.PotentiallyBreaking,
                new ChangeLocation("/payments"),
                "Response enum widened: + PENDING"),
        };
        var result = new ComparisonResult(changes);

        var json = JsonReporter.Render(result);
        using var document = JsonDocument.Parse(json);

        var root = document.RootElement;
        Assert.Equal("contractwatch", root.GetProperty("tool").GetString());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("breaking").GetInt32());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("potentiallyBreaking").GetInt32());

        var first = root.GetProperty("changes")[0];
        Assert.Equal("CW004", first.GetProperty("ruleId").GetString());
        Assert.Equal("Breaking", first.GetProperty("severity").GetString());
        Assert.Equal("/orders", first.GetProperty("location").GetProperty("path").GetString());
        Assert.Equal("POST", first.GetProperty("location").GetProperty("method").GetString());
        Assert.Equal(JsonValueKind.Null, first.GetProperty("oldValue").ValueKind);

        var second = root.GetProperty("changes")[1];
        Assert.Equal("PotentiallyBreaking", second.GetProperty("severity").GetString());
        Assert.Equal(JsonValueKind.Null, second.GetProperty("location").GetProperty("method").ValueKind);
    }

    [Fact]
    public void La_sugerencia_se_serializa_por_entrada_y_puede_ser_null()
    {
        var changes = new List<ContractChange>
        {
            new("CW004", "RequiredPropertyAdded", ChangeSeverity.Breaking,
                new ChangeLocation("/orders", "POST"), "Required request property added: currency",
                Suggestion: "Introduce the property as optional with a default value."),
            new("CW015", "OptionalPropertyAdded", ChangeSeverity.Compatible,
                new ChangeLocation("/orders", "POST"), "Optional property added: metadata"),
        };
        var result = new ComparisonResult(changes);

        var json = JsonReporter.Render(result);
        using var document = JsonDocument.Parse(json);

        var entries = document.RootElement.GetProperty("changes");
        Assert.Equal(JsonValueKind.String, entries[0].GetProperty("suggestion").ValueKind);
        Assert.Contains("optional with a default", entries[0].GetProperty("suggestion").GetString());
        Assert.Equal(JsonValueKind.Null, entries[1].GetProperty("suggestion").ValueKind);
    }
}
