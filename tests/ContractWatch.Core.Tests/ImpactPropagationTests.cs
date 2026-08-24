using ContractWatch.Core;
using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Tests;

public class ImpactPropagationTests
{
    private static ContractChange Change(string ruleId, ChangeSeverity severity, string path, string? method = null) =>
        new(ruleId, "TestRule", severity, new ChangeLocation(path, method), "message");

    private static ConsumerEntry Entry(string service, params string[] operations) => new(service, operations);

    private static ImpactGraph Graph(ConsumerRegistry registry, Dictionary<string, ImpactGraph>? children = null) =>
        new(registry, children ?? new Dictionary<string, ImpactGraph>(StringComparer.Ordinal));

    private static Dictionary<string, ImpactGraph> Children(params (string Service, ImpactGraph Node)[] nodes)
    {
        var children = new Dictionary<string, ImpactGraph>(StringComparer.Ordinal);

        foreach (var (service, node) in nodes)
            children[service] = node;

        return children;
    }

    [Fact]
    public void Cadena_lineal_propaga_el_impacto_al_consumidor_del_consumidor()
    {
        var result = new ComparisonResult([Change("CW004", ChangeSeverity.Breaking, "/bets", "POST")]);
        var orders = Graph(
            new ConsumerRegistry([Entry("checkout-web", ["POST /checkout"])], "orders-api"));
        var root = Graph(
            new ConsumerRegistry(
                [
                    Entry("admin-web", ["GET /players/{id}"]),
                    new ConsumerEntry("orders-api", ["POST /bets"]),
                ],
                "player-api"),
            Children(("orders-api", orders)));

        var impact = ImpactAnalyzer.Analyze(result, root);

        Assert.Equal(["checkout-web", "orders-api"], impact.Consumers.Select(c => c.Service).ToArray());

        var chain = Assert.Single(impact.Chains);
        Assert.Equal(["player-api", "orders-api", "checkout-web"], chain.Services);
        Assert.Equal(ConfidenceLevel.High, chain.Confidence);
        var trigger = Assert.Single(chain.Triggers);
        Assert.Equal("CW004", trigger.RuleId);
        Assert.Equal("POST /bets", trigger.Target);
    }

    [Fact]
    public void El_salto_intermedio_fija_los_cambios_disparadores_de_toda_la_cadena()
    {
        var result = new ComparisonResult(
        [
            Change("CW004", ChangeSeverity.Breaking, "/bets", "POST"),
            Change("CW015", ChangeSeverity.Compatible, "/bets", "POST"),
            Change("CW010", ChangeSeverity.PotentiallyBreaking, "/players", "GET"),
        ]);
        var orders = Graph(new ConsumerRegistry([Entry("checkout-web", ["/checkout"])], "orders-api"));
        var root = Graph(
            new ConsumerRegistry([new ConsumerEntry("orders-api", ["POST /bets"])], "player-api"),
            Children(("orders-api", orders)));

        var impact = ImpactAnalyzer.Analyze(result, root);

        Assert.Equal(1, impact.Consumers.Single(c => c.Service == "checkout-web").Changes);

        var chain = Assert.Single(impact.Chains);
        var trigger = Assert.Single(chain.Triggers);
        Assert.Equal(("CW004", "POST /bets"), (trigger.RuleId, trigger.Target));
    }

    [Fact]
    public void Un_salto_solo_por_path_limita_toda_la_cadena_a_confianza_media()
    {
        var result = new ComparisonResult([Change("CW002", ChangeSeverity.Breaking, "/bets/{id}", "GET")]);
        var orders = Graph(new ConsumerRegistry([Entry("checkout-web", ["POST /checkout"])], "orders-api"));
        var root = Graph(
            new ConsumerRegistry([new ConsumerEntry("orders-api", ["/bets/{id}"])], "player-api"),
            Children(("orders-api", orders)));

        var impact = ImpactAnalyzer.Analyze(result, root);

        Assert.Equal(ConfidenceLevel.Medium, impact.Consumers.Single(c => c.Service == "orders-api").Confidence);
        Assert.Equal(ConfidenceLevel.Medium, impact.Consumers.Single(c => c.Service == "checkout-web").Confidence);
        Assert.Equal(ConfidenceLevel.Medium, Assert.Single(impact.Chains).Confidence);
    }

    [Fact]
    public void El_minimo_de_confianza_predomina_aunque_los_siguientes_saltos_fijen_metodo()
    {
        var result = new ComparisonResult([Change("CW003", ChangeSeverity.Breaking, "/bets", "PUT")]);
        var deep = Graph(new ConsumerRegistry([Entry("audit-log", ["POST /events"])], "reporting-service"));
        var middle = Graph(
            new ConsumerRegistry([new ConsumerEntry("reporting-service", ["/events"])], "orders-api"),
            Children(("reporting-service", deep)));
        var root = Graph(
            new ConsumerRegistry([new ConsumerEntry("orders-api", ["/bets"])], "player-api"),
            Children(("orders-api", middle)));

        var impact = ImpactAnalyzer.Analyze(result, root);

        Assert.All(impact.Consumers, c => Assert.Equal(ConfidenceLevel.Medium, c.Confidence));
        Assert.Equal(2, impact.Chains.Count);
        Assert.All(impact.Chains, c => Assert.Equal(ConfidenceLevel.Medium, c.Confidence));
    }

    [Fact]
    public void Diamante_lista_el_servicio_una_vez_con_maxima_confianza_y_dos_cadenas()
    {
        var result = new ComparisonResult([Change("CW004", ChangeSeverity.Breaking, "/bets", "POST")]);
        var leaf = Graph(new ConsumerRegistry([], "shared-leaf"));
        var left = Graph(new ConsumerRegistry([Entry("admin-web", ["/dashboard"])], "orders-api"), Children(("admin-web", leaf)));
        var right = Graph(new ConsumerRegistry([Entry("admin-web", ["/dashboard"])], "billing-api"), Children(("admin-web", leaf)));
        var root = Graph(
            new ConsumerRegistry(
                [
                    new ConsumerEntry("orders-api", ["POST /bets"]),
                    new ConsumerEntry("billing-api", ["/bets"]),
                ],
                "player-api"),
            Children(("orders-api", left), ("billing-api", right)));

        var impact = ImpactAnalyzer.Analyze(result, root);

        var listed = Assert.Single(impact.Consumers, c => c.Service == "admin-web");
        Assert.Equal(ConfidenceLevel.Medium, listed.Confidence);
        Assert.Equal(1, listed.Changes);

        Assert.Equal(2, impact.Chains.Count);
        Assert.All(impact.Chains, c => Assert.Equal(ConfidenceLevel.Medium, c.Confidence));
        Assert.Equal(
            [["player-api", "billing-api", "admin-web"], ["player-api", "orders-api", "admin-web"]],
            impact.Chains.Select(c => c.Services).ToArray());
    }

    [Fact]
    public void Los_cambios_compatibles_no_generan_ni_cadenas_ni_impacto()
    {
        var result = new ComparisonResult([Change("CW015", ChangeSeverity.Compatible, "/bets", "POST")]);
        var orders = Graph(new ConsumerRegistry([Entry("checkout-web", ["/checkout"])], "orders-api"));
        var root = Graph(
            new ConsumerRegistry([new ConsumerEntry("orders-api", ["POST /bets"])], "player-api"),
            Children(("orders-api", orders)));

        var impact = ImpactAnalyzer.Analyze(result, root);

        Assert.Empty(impact.Consumers);
        Assert.Empty(impact.Chains);
    }

    [Fact]
    public void Sin_coincidencia_directa_no_hay_propagacion()
    {
        var result = new ComparisonResult([Change("CW001", ChangeSeverity.Breaking, "/refunds", "POST")]);
        var orders = Graph(new ConsumerRegistry([Entry("checkout-web", ["/checkout"])], "orders-api"));
        var root = Graph(
            new ConsumerRegistry([new ConsumerEntry("orders-api", ["POST /bets"])], "player-api"),
            Children(("orders-api", orders)));

        var impact = ImpactAnalyzer.Analyze(result, root);

        Assert.Empty(impact.Consumers);
        Assert.Empty(impact.Chains);
    }

    [Fact]
    public void Registro_heredado_sin_specs_mantiene_el_comportamiento_previo()
    {
        var result = new ComparisonResult([Change("CW004", ChangeSeverity.Breaking, "/bets", "POST")]);
        var registry = new ConsumerRegistry([Entry("orders-api", ["POST /bets"])]);

        var impact = ImpactAnalyzer.Analyze(result, registry);

        var consumer = Assert.Single(impact);
        Assert.Equal("orders-api", consumer.Service);
        Assert.Equal(ConfidenceLevel.High, consumer.Confidence);
    }

    [Fact]
    public void Disparador_a_nivel_de_path_no_lleva_metodo_en_la_anotacion()
    {
        var result = new ComparisonResult([Change("CW001", ChangeSeverity.Breaking, "/legacy/bets")]);
        var orders = Graph(new ConsumerRegistry([Entry("checkout-web", ["GET /checkout"])], "orders-api"));
        var root = Graph(
            new ConsumerRegistry([new ConsumerEntry("orders-api", ["GET /legacy/bets"])], "player-api"),
            Children(("orders-api", orders)));

        var impact = ImpactAnalyzer.Analyze(result, root);

        var trigger = Assert.Single(Assert.Single(impact.Chains).Triggers);
        Assert.Equal("/legacy/bets", trigger.Target);
    }

    [Fact]
    public void Las_cadenas_se_ordenan_por_confianza_descendente_y_luego_secuencia_ordinal()
    {
        var result = new ComparisonResult([Change("CW004", ChangeSeverity.Breaking, "/bets", "POST")]);
        var zetaLeaf = Graph(new ConsumerRegistry([], "web-zeta"));
        var alphaLeaf = Graph(new ConsumerRegistry([], "web-alpha"));
        var zeta = Graph(new ConsumerRegistry([Entry("web-zeta", ["POST /z"])], "zeta-api"), Children(("web-zeta", zetaLeaf)));
        var alpha = Graph(new ConsumerRegistry([Entry("web-alpha", ["POST /a"])], "alpha-api"), Children(("web-alpha", alphaLeaf)));
        var root = Graph(
            new ConsumerRegistry(
                [
                    new ConsumerEntry("zeta-api", ["POST /bets"]),
                    new ConsumerEntry("alpha-api", ["POST /bets"]),
                ],
                "player-api"),
            Children(("zeta-api", zeta), ("alpha-api", alpha)));

        var impact = ImpactAnalyzer.Analyze(result, root);

        Assert.Equal(
            ["player-api → alpha-api → web-alpha", "player-api → zeta-api → web-zeta"],
            impact.Chains.Select(c => string.Join(" → ", c.Services)).ToArray());
    }

    [Fact]
    public void Un_ciclo_en_memoria_lanza_error_durante_la_propagacion()
    {
        var result = new ComparisonResult([Change("CW004", ChangeSeverity.Breaking, "/bets", "POST")]);
        var loop = Graph(new ConsumerRegistry([new ConsumerEntry("orders-api", ["POST /bets"])], "orders-api"));
        var root = Graph(
            new ConsumerRegistry([new ConsumerEntry("orders-api", ["POST /bets"])], "player-api"),
            Children(("orders-api", loop)));

        var exception = Assert.Throws<ConsumerRegistryException>(() => ImpactAnalyzer.Analyze(result, root));

        Assert.Contains("ciclo de consumidores detectado", exception.Message);
    }
}
