using System.Text.Json;
using ContractWatch.Core;
using ContractWatch.Core.Comparison;
using ContractWatch.Core.Reporting;

namespace ContractWatch.Core.Tests;

public class SarifReporterTests
{
    [Fact]
    public void Renderiza_estructura_sarif_con_tool_driver_y_version()
    {
        var json = SarifReporter.Render(Result(), "openapi.json");
        using var document = JsonDocument.Parse(json);

        var root = document.RootElement;
        Assert.Equal("https://json.schemastore.org/sarif-2.1.0.json", root.GetProperty("$schema").GetString());
        Assert.Equal("2.1.0", root.GetProperty("version").GetString());

        var driver = root.GetProperty("runs")[0].GetProperty("tool").GetProperty("driver");
        Assert.Equal("contractwatch", driver.GetProperty("name").GetString());
        Assert.Equal(JsonReporter.ToolVersion, driver.GetProperty("version").GetString());
        Assert.Equal("https://github.com/Steve-XYZ/contract-watch", driver.GetProperty("informationUri").GetString());
    }

    [Fact]
    public void Results_excluyen_los_cambios_compatibles_y_mantienen_el_orden_del_resultado()
    {
        var json = SarifReporter.Render(Result(), "openapi.json");
        using var document = JsonDocument.Parse(json);

        var results = document.RootElement.GetProperty("runs")[0].GetProperty("results");

        Assert.Equal(2, results.GetArrayLength());
        Assert.Equal("CW003", results[0].GetProperty("ruleId").GetString());
        Assert.Equal("CW010", results[1].GetProperty("ruleId").GetString());
    }

    [Fact]
    public void Levels_son_error_para_breaking_y_warning_para_potentially()
    {
        var json = SarifReporter.Render(Result(), "openapi.json");
        using var document = JsonDocument.Parse(json);

        var results = document.RootElement.GetProperty("runs")[0].GetProperty("results");

        Assert.Equal("error", results[0].GetProperty("level").GetString());
        Assert.Equal("warning", results[1].GetProperty("level").GetString());
    }

    [Fact]
    public void Rules_declara_las_reglas_de_los_resultados_y_ruleIndex_es_consistente()
    {
        var json = SarifReporter.Render(Result(), "openapi.json");
        using var document = JsonDocument.Parse(json);

        var run = document.RootElement.GetProperty("runs")[0];
        var rules = run.GetProperty("tool").GetProperty("driver").GetProperty("rules");
        var results = run.GetProperty("results");

        Assert.Equal(2, rules.GetArrayLength());
        Assert.Equal("CW003", rules[0].GetProperty("id").GetString());
        Assert.Equal("RequiredParameterAdded", rules[0].GetProperty("name").GetString());
        Assert.Equal("CW010", rules[1].GetProperty("id").GetString());

        for (var i = 0; i < results.GetArrayLength(); i++)
        {
            var ruleId = results[i].GetProperty("ruleId").GetString()!;
            var expectedIndex = Enumerable.Range(0, rules.GetArrayLength())
                .Single(j => rules[j].GetProperty("id").GetString() == ruleId);

            Assert.Equal(expectedIndex, results[i].GetProperty("ruleIndex").GetInt32());
        }
    }

    [Fact]
    public void Message_locations_y_properties_llegan_al_resultado()
    {
        var json = SarifReporter.Render(Result(), "specs/openapi.json");
        using var document = JsonDocument.Parse(json);

        var first = document.RootElement.GetProperty("runs")[0].GetProperty("results")[0];

        Assert.Equal("Required parameter added: page", first.GetProperty("message").GetProperty("text").GetString());
        Assert.Equal("specs/openapi.json", first.GetProperty("locations")[0]
            .GetProperty("physicalLocation").GetProperty("artifactLocation").GetProperty("uri").GetString());

        var properties = first.GetProperty("properties");
        Assert.Equal("Breaking", properties.GetProperty("severity").GetString());
        Assert.Equal("/orders", properties.GetProperty("path").GetString());
        Assert.Equal("POST", properties.GetProperty("method").GetString());
    }

    [Fact]
    public void Method_ausente_se_serializa_como_null_en_properties()
    {
        var json = SarifReporter.Render(Result(), "openapi.json");
        using var document = JsonDocument.Parse(json);

        var second = document.RootElement.GetProperty("runs")[0].GetProperty("results")[1];
        var properties = second.GetProperty("properties");

        Assert.Equal("/payments", properties.GetProperty("path").GetString());
        Assert.Equal(JsonValueKind.Null, properties.GetProperty("method").ValueKind);
    }

    [Fact]
    public void Properties_incluye_la_sugerencia_del_cambio()
    {
        var result = new ComparisonResult(
        [
            new("CW003", "RequiredParameterAdded", ChangeSeverity.Breaking,
                new ChangeLocation("/orders", "POST"), "Required parameter added: page",
                Suggestion: "Introduce the parameter as optional with a server-side default."),
        ]);

        var json = SarifReporter.Render(result, "openapi.json");
        using var document = JsonDocument.Parse(json);

        var properties = document.RootElement.GetProperty("runs")[0].GetProperty("results")[0].GetProperty("properties");
        Assert.Equal("Introduce the parameter as optional with a server-side default.", properties.GetProperty("suggestion").GetString());
    }

    [Fact]
    public void Properties_incluye_la_explicacion_cuando_existe_y_null_cuando_no()
    {
        var explained = new ComparisonResult(
        [
            new("CW003", "RequiredParameterAdded", ChangeSeverity.Breaking,
                new ChangeLocation("/orders", "POST"), "Required parameter added: page",
                Explanation: "[fake] CW003 at POST /orders."),
            new("CW010", "EnumWidened", ChangeSeverity.PotentiallyBreaking,
                new ChangeLocation("/payments"), "Response enum widened: + PENDING"),
        ]);

        var json = SarifReporter.Render(explained, "openapi.json");
        using var document = JsonDocument.Parse(json);

        var results = document.RootElement.GetProperty("runs")[0].GetProperty("results");
        Assert.Equal("[fake] CW003 at POST /orders.", results[0].GetProperty("properties").GetProperty("explanation").GetString());
        Assert.Equal(JsonValueKind.Null, results[1].GetProperty("properties").GetProperty("explanation").ValueKind);
    }

    private static ComparisonResult Result() => new(
    [
        new("CW003", "RequiredParameterAdded", ChangeSeverity.Breaking,
            new ChangeLocation("/orders", "POST"), "Required parameter added: page"),
        new("CW010", "EnumWidened", ChangeSeverity.PotentiallyBreaking,
            new ChangeLocation("/payments"), "Response enum widened: + PENDING"),
        new("CW015", "OptionalPropertyAdded", ChangeSeverity.Compatible,
            new ChangeLocation("/orders", "POST"), "Optional property added: metadata"),
    ]);
}

public class SarifArtifactUriTests
{
    [Fact]
    public void Ruta_bajo_el_directorio_actual_se_vuelve_relativa()
    {
        var absolute = Path.Combine(Environment.CurrentDirectory, "specs", "openapi.json");

        var uri = SarifReporter.NormalizeArtifactUri(absolute);

        Assert.False(Path.IsPathRooted(uri));
        Assert.Equal(Path.Join("specs", "openapi.json"), uri);
    }

    [Fact]
    public void Ruta_fuera_del_directorio_actual_no_queda_absoluta()
    {
        var uri = SarifReporter.NormalizeArtifactUri(Path.Combine(Path.GetTempPath(), "otro.json"));

        Assert.False(Path.IsPathRooted(uri));
        Assert.StartsWith("..", uri);
    }
}
