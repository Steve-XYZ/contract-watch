using ContractWatch.Core;
using ContractWatch.Core.Comparison;
using ContractWatch.Core.Reporting;

namespace ContractWatch.Core.Tests;

public class ConsumerImpactTests
{
    private static ContractChange Change(string ruleId, ChangeSeverity severity, string path, string? method = null) =>
        new(ruleId, "TestRule", severity, new ChangeLocation(path, method), "message");

    private static ConsumerRegistry Registry(params ConsumerEntry[] consumers) => new(consumers);

    [Fact]
    public void Coincidencia_exacta_de_metodo_y_path_produce_confianza_alta()
    {
        var result = new ComparisonResult([Change("CW004", ChangeSeverity.Breaking, "/orders", "POST")]);
        var registry = Registry(new ConsumerEntry("admin-web", ["POST /orders"]));

        var impact = ImpactAnalyzer.Analyze(result, registry);

        var consumer = Assert.Single(impact);
        Assert.Equal("admin-web", consumer.Service);
        Assert.Equal(ConfidenceLevel.High, consumer.Confidence);
        Assert.Equal(1, consumer.Changes);
    }

    [Fact]
    public void Coincidencia_solo_por_path_produce_confianza_media()
    {
        var result = new ComparisonResult([Change("CW002", ChangeSeverity.Breaking, "/orders/{id}", "GET")]);
        var registry = Registry(new ConsumerEntry("reporting-service", ["/orders/{id}"]));

        var impact = ImpactAnalyzer.Analyze(result, registry);

        var consumer = Assert.Single(impact);
        Assert.Equal(ConfidenceLevel.Medium, consumer.Confidence);
    }

    [Fact]
    public void Metodo_asterisco_coincide_cualquier_metodo_con_confianza_media()
    {
        var result = new ComparisonResult([Change("CW003", ChangeSeverity.Breaking, "/orders", "PUT")]);
        var registry = Registry(new ConsumerEntry("admin-web", ["* /orders"]));

        var impact = ImpactAnalyzer.Analyze(result, registry);

        var consumer = Assert.Single(impact);
        Assert.Equal(ConfidenceLevel.Medium, consumer.Confidence);
        Assert.Equal(1, consumer.Changes);
    }

    [Fact]
    public void Mismo_path_con_metodo_distinto_no_afecta_al_consumidor()
    {
        var result = new ComparisonResult([Change("CW002", ChangeSeverity.Breaking, "/orders", "GET")]);
        var registry = Registry(new ConsumerEntry("admin-web", ["POST /orders"]));

        Assert.Empty(ImpactAnalyzer.Analyze(result, registry));
    }

    [Fact]
    public void Cambio_a_nivel_de_path_afecta_a_consumidores_declarados_con_metodo()
    {
        var result = new ComparisonResult([Change("CW001", ChangeSeverity.Breaking, "/legacy/orders")]);
        var registry = Registry(new ConsumerEntry("admin-web", ["GET /legacy/orders"]));

        var impact = ImpactAnalyzer.Analyze(result, registry);

        var consumer = Assert.Single(impact);
        Assert.Equal(ConfidenceLevel.High, consumer.Confidence);
        Assert.Equal(1, consumer.Changes);
    }

    [Fact]
    public void Cambio_a_nivel_de_path_no_cruza_hacia_otros_paths()
    {
        var result = new ComparisonResult([Change("CW001", ChangeSeverity.Breaking, "/legacy/orders")]);
        var registry = Registry(new ConsumerEntry("admin-web", ["GET /orders"]));

        Assert.Empty(ImpactAnalyzer.Analyze(result, registry));
    }

    [Fact]
    public void Path_distinto_no_afecta_al_consumidor()
    {
        var result = new ComparisonResult([Change("CW001", ChangeSeverity.Breaking, "/refunds", "GET")]);
        var registry = Registry(new ConsumerEntry("admin-web", ["POST /orders", "/orders/{id}"]));

        Assert.Empty(ImpactAnalyzer.Analyze(result, registry));
    }

    [Theory]
    [InlineData("post")]
    [InlineData("Post")]
    public void La_coincidencia_de_metodo_ignora_mayusculas(string operation)
    {
        var result = new ComparisonResult([Change("CW003", ChangeSeverity.Breaking, "/orders", "POST")]);
        var registry = Registry(new ConsumerEntry("admin-web", [$"{operation} /orders"]));

        var impact = ImpactAnalyzer.Analyze(result, registry);

        Assert.Equal(ConfidenceLevel.High, Assert.Single(impact).Confidence);
    }

    [Fact]
    public void Los_cambios_compatibles_no_impactan_a_nadie()
    {
        var result = new ComparisonResult(
        [
            Change("CW015", ChangeSeverity.Compatible, "/orders", "POST"),
            Change("CW016", ChangeSeverity.Compatible, "/orders"),
        ]);
        var registry = Registry(new ConsumerEntry("admin-web", ["POST /orders", "/orders"]));

        Assert.Empty(ImpactAnalyzer.Analyze(result, registry));
    }

    [Fact]
    public void El_orden_es_confianza_descendente_y_luego_servicio_ordinal()
    {
        var result = new ComparisonResult(
        [
            Change("CW001", ChangeSeverity.Breaking, "/a"),
            Change("CW002", ChangeSeverity.Breaking, "/b", "GET"),
            Change("CW003", ChangeSeverity.Breaking, "/c"),
        ]);
        var registry = Registry(
            new ConsumerEntry("zeta-service", ["/a"]),
            new ConsumerEntry("alpha-service", ["/b"]),
            new ConsumerEntry("mike-service", ["/c"]));

        var impact = ImpactAnalyzer.Analyze(result, registry);

        Assert.Equal(["alpha-service", "mike-service", "zeta-service"], impact.Select(c => c.Service).ToArray());
    }

    [Fact]
    public void Mezcla_de_confianzas_para_el_mismo_consumidor_prevalece_la_alta()
    {
        var result = new ComparisonResult(
        [
            Change("CW004", ChangeSeverity.Breaking, "/orders", "POST"),
            Change("CW002", ChangeSeverity.Breaking, "/orders/{id}", "GET"),
        ]);
        var registry = Registry(new ConsumerEntry("admin-web", ["POST /orders", "GET /orders/{id}"]));

        var impact = ImpactAnalyzer.Analyze(result, registry);

        var consumer = Assert.Single(impact);
        Assert.Equal(ConfidenceLevel.High, consumer.Confidence);
        Assert.Equal(2, consumer.Changes);
    }

    [Fact]
    public void Un_cambio_se_cuenta_una_vez_aunque_varias_entradas_del_consumidor_coincidan()
    {
        var result = new ComparisonResult([Change("CW003", ChangeSeverity.Breaking, "/orders", "POST")]);
        var registry = Registry(new ConsumerEntry("admin-web", ["POST /orders", "/orders"]));

        var impact = ImpactAnalyzer.Analyze(result, registry);

        var consumer = Assert.Single(impact);
        Assert.Equal(ConfidenceLevel.High, consumer.Confidence);
        Assert.Equal(1, consumer.Changes);
    }

    [Fact]
    public void Console_agrega_bloque_de_consumidores_afectados_tras_el_resumen()
    {
        var changes = new List<ContractChange>
        {
            Change("CW004", ChangeSeverity.Breaking, "/orders", "POST"),
            Change("CW015", ChangeSeverity.Compatible, "/orders", "POST"),
        };
        var impact = new List<AffectedConsumer> { new("admin-web", ConfidenceLevel.High, 1), new("reporting-service", ConfidenceLevel.Medium, 2) };

        var output = ConsoleReporter.Render(changes, impact);

        Assert.EndsWith($"""
            1 breaking · 0 potentially breaking · 1 compatible

            Consumidores afectados:
              admin-web · confianza alta · 1 cambio(s)
              reporting-service · confianza media · 2 cambio(s)
            """, output);
    }

    [Fact]
    public void Console_anuncia_que_nadie_se_rompe_cuando_hay_cambios_pero_sin_consumidores()
    {
        var changes = new List<ContractChange>
        {
            Change("CW010", ChangeSeverity.PotentiallyBreaking, "/payments", "GET"),
        };

        var output = ConsoleReporter.Render(changes, []);

        Assert.EndsWith("""
            0 breaking · 1 potentially breaking · 0 compatible
            Sin consumidores afectados.
            """, output);
    }

    [Fact]
    public void Console_sin_impacto_no_agrega_nada_sobre_consumidores()
    {
        var changes = new List<ContractChange>
        {
            Change("CW010", ChangeSeverity.PotentiallyBreaking, "/payments", "GET"),
        };

        var output = ConsoleReporter.Render(changes);

        Assert.DoesNotContain("Consumidores afectados", output);
        Assert.DoesNotContain("Sin consumidores afectados.", output);
    }

    [Fact]
    public void Markdown_incluye_seccion_solo_cuando_hay_afectados()
    {
        var result = new ComparisonResult([Change("CW004", ChangeSeverity.Breaking, "/orders", "POST")]);
        var impact = new List<AffectedConsumer> { new("admin-web", ConfidenceLevel.High, 1) };

        var markdown = MarkdownReporter.Render(result, impact);

        Assert.Contains("### Affected consumers", markdown);
        Assert.Contains("| Service | Confidence | Changes |", markdown);
        Assert.Contains("| admin-web | High | 1 |", markdown);
    }

    [Fact]
    public void Markdown_omite_la_seccion_cuando_no_hay_afectados_o_no_hay_impacto()
    {
        var result = new ComparisonResult([Change("CW004", ChangeSeverity.Breaking, "/orders", "POST")]);

        Assert.DoesNotContain("### Affected consumers", MarkdownReporter.Render(result, []));
        Assert.DoesNotContain("### Affected consumers", MarkdownReporter.Render(result));
    }

    [Fact]
    public void Json_incluye_affectedConsumers_solo_cuando_hay_impacto()
    {
        var result = new ComparisonResult([Change("CW004", ChangeSeverity.Breaking, "/orders", "POST")]);
        var impact = new List<AffectedConsumer> { new("admin-web", ConfidenceLevel.High, 1) };

        var json = JsonReporter.Render(result, impact);

        Assert.Contains("\"affectedConsumers\": [", json);
        Assert.Contains("\"service\": \"admin-web\"", json);
        Assert.Contains("\"confidence\": \"High\"", json);
        Assert.Contains("\"changes\": 1", json);
    }

    [Fact]
    public void Json_con_la_firma_vieja_omite_affectedConsumers()
    {
        var result = new ComparisonResult([Change("CW004", ChangeSeverity.Breaking, "/orders", "POST")]);

        var json = JsonReporter.Render(result);

        Assert.DoesNotContain("affectedConsumers", json);
    }
}
