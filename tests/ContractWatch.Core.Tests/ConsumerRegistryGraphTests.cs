using ContractWatch.Core;
using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Tests;

public class ConsumerRegistryGraphTests : IDisposable
{
    private readonly string _directory;

    public ConsumerRegistryGraphTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"cw-graph-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private void WriteRoot(string content) => File.WriteAllText(Path.Combine(_directory, "consumers.json"), content);

    private string NewService(string name, string consumersContent)
    {
        var directory = Path.Combine(_directory, name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "openapi.json"), "{}");
        File.WriteAllText(Path.Combine(directory, "consumers.json"), consumersContent);
        return directory;
    }

    private ImpactGraph LoadGraph() =>
        ConsumerRegistryFile.LoadGraphOrDefault(null, _directory);

    [Fact]
    public void Parse_service_raiz_y_spec_por_consumidor()
    {
        WriteRoot("""
            {
              "service": "player-api",
              "consumers": [
                { "service": "orders-api", "operations": ["POST /bets"], "spec": "orders/openapi.json" }
              ]
            }
            """);

        var registry = ConsumerRegistryFile.Load(Path.Combine(_directory, "consumers.json"));

        Assert.Equal("player-api", registry.Service);
        Assert.Equal("orders/openapi.json", registry.Consumers[0].Spec);
    }

    [Fact]
    public void Service_raiz_vacio_lanza_error()
    {
        WriteRoot("""{ "service": "  ", "consumers": [] }""");

        var exception = Assert.Throws<ConsumerRegistryException>(
            () => ConsumerRegistryFile.Load(Path.Combine(_directory, "consumers.json")));

        Assert.Contains("el nombre del servicio es obligatorio", exception.Message);
    }

    [Fact]
    public void Spec_vacio_lanza_error()
    {
        WriteRoot("""{ "consumers": [ { "service": "orders-api", "operations": ["/bets"], "spec": "   " } ] }""");

        var exception = Assert.Throws<ConsumerRegistryException>(() => LoadGraph());

        Assert.Contains("la ruta del spec está vacía", exception.Message);
    }

    [Fact]
    public void LoadGraph_resuelve_el_consumers_json_junto_al_spec_de_forma_recursiva()
    {
        WriteRoot("""
            {
              "service": "player-api",
              "consumers": [
                { "service": "orders-api", "operations": ["POST /bets"], "spec": "orders/openapi.json" }
              ]
            }
            """);
        NewService("orders", """
            {
              "service": "orders-api",
              "consumers": [
                { "service": "checkout-web", "operations": ["POST /checkout"], "spec": "../checkout/openapi.json" }
              ]
            }
            """);
        NewService("checkout", """
            {
              "service": "checkout-web",
              "consumers": []
            }
            """);

        var graph = LoadGraph();

        var orders = graph.Children["orders-api"];
        Assert.Equal(["checkout-web"], orders.Registry.Consumers.Select(c => c.Service));
        Assert.Equal("player-api", graph.Registry.Service);

        var checkout = Assert.Single(orders.Children.Values).Registry;
        Assert.Equal("checkout-web", checkout.Service);
        Assert.Empty(checkout.Consumers);
    }

    [Fact]
    public void Las_rutas_relativas_se_resuelven_contra_el_directorio_del_archivo_que_declara()
    {
        WriteRoot("""
            {
              "consumers": [
                { "service": "orders-api", "operations": ["/bets"], "spec": "./orders/../orders/openapi.json" }
              ]
            }
            """);
        NewService("orders", """{ "consumers": [] }""");

        var graph = LoadGraph();

        Assert.Single(graph.Children);
    }

    [Fact]
    public void Spec_inexistente_lanza_error_con_la_ruta_declarada()
    {
        WriteRoot("""
            {
              "consumers": [
                { "service": "orders-api", "operations": ["/bets"], "spec": "../ghost/openapi.json" }
              ]
            }
            """);

        var exception = Assert.Throws<ConsumerRegistryException>(() => LoadGraph());

        Assert.Contains("no existe el spec '../ghost/openapi.json'", exception.Message);
        Assert.Contains("orders-api", exception.Message);
    }

    [Fact]
    public void Falta_el_consumers_json_junto_al_spec_lanza_error_claro()
    {
        WriteRoot("""
            {
              "consumers": [
                { "service": "orders-api", "operations": ["/bets"], "spec": "orders/openapi.json" }
              ]
            }
            """);
        Directory.CreateDirectory(Path.Combine(_directory, "orders"));
        File.WriteAllText(Path.Combine(_directory, "orders", "openapi.json"), "{}");

        var exception = Assert.Throws<ConsumerRegistryException>(() => LoadGraph());

        Assert.Contains("orders-api", exception.Message);
        Assert.Contains($"no hay un '{ConsumerRegistryFile.DefaultFileName}' junto a él", exception.Message);
    }

    [Fact]
    public void Ciclo_entre_archivos_lanza_error_nombrando_la_cadena()
    {
        WriteRoot("""
            {
              "service": "raiz",
              "consumers": [
                { "service": "a", "operations": ["/a"], "spec": "a/openapi.json" }
              ]
            }
            """);
        NewService("a", """
            {
              "service": "a",
              "consumers": [
                { "service": "b", "operations": ["/b"], "spec": "../b/openapi.json" }
              ]
            }
            """);
        NewService("b", """
            {
              "service": "b",
              "consumers": [
                { "service": "a", "operations": ["/a"], "spec": "../a/openapi.json" }
              ]
            }
            """);

        var exception = Assert.Throws<ConsumerRegistryException>(() => LoadGraph());

        Assert.Contains("ciclo de consumidores detectado", exception.Message);
        Assert.Contains("raiz → a → b → a", exception.Message);
    }

    [Fact]
    public void Auto_referencia_al_propio_directorio_lanza_ciclo()
    {
        WriteRoot("""
            {
              "service": "solo",
              "consumers": [
                { "service": "solo", "operations": ["/self"], "spec": "./openapi.json" }
              ]
            }
            """);
        File.WriteAllText(Path.Combine(_directory, "openapi.json"), "{}");

        var exception = Assert.Throws<ConsumerRegistryException>(() => LoadGraph());

        Assert.Contains("ciclo de consumidores detectado", exception.Message);
    }

    [Fact]
    public void Diamante_de_archivos_comparte_subarbol_sin_falso_ciclo()
    {
        WriteRoot("""
            {
              "service": "player-api",
              "consumers": [
                { "service": "orders-api", "operations": ["/bets"], "spec": "orders/openapi.json" },
                { "service": "billing-api", "operations": ["/invoices"], "spec": "billing/openapi.json" }
              ]
            }
            """);
        NewService("orders", """
            {
              "service": "orders-api",
              "consumers": [
                { "service": "admin-web", "operations": ["/dashboard"], "spec": "../admin/openapi.json" }
              ]
            }
            """);
        NewService("billing", """
            {
              "service": "billing-api",
              "consumers": [
                { "service": "admin-web", "operations": ["/dashboard"], "spec": "../admin/openapi.json" }
              ]
            }
            """);
        NewService("admin", """{ "service": "admin-web", "consumers": [] }""");

        var graph = LoadGraph();

        Assert.Equal(2, graph.Children.Count);
        Assert.All(graph.Children.Values, node => Assert.Single(node.Children));
    }

    [Fact]
    public void Archivo_anidado_invalido_reporta_su_propia_ruta_en_el_error()
    {
        WriteRoot("""
            {
              "consumers": [
                { "service": "orders-api", "operations": ["/bets"], "spec": "orders/openapi.json" }
              ]
            }
            """);
        Directory.CreateDirectory(Path.Combine(_directory, "orders"));
        File.WriteAllText(Path.Combine(_directory, "orders", "openapi.json"), "{}");
        File.WriteAllText(Path.Combine(_directory, "orders", "consumers.json"), "{ consumers: ");

        var nestedPath = Path.Combine(_directory, "orders", "consumers.json");
        var exception = Assert.Throws<ConsumerRegistryException>(() => LoadGraph());

        Assert.Contains(nestedPath, exception.Message);
        Assert.Contains("JSON malformado", exception.Message);
    }

    [Fact]
    public void Sin_registro_raiz_devuelve_grafo_vacio_y_analisis_vacio()
    {
        var graph = ConsumerRegistryFile.LoadGraphOrDefault(null, _directory);

        Assert.Empty(graph.Registry.Consumers);
        Assert.Null(graph.Registry.Service);
        Assert.Empty(graph.Children);

        var impact = ImpactAnalyzer.Analyze(new ComparisonResult(
        [
            new ContractChange("CW001", "EndpointRemoved", ChangeSeverity.Breaking, new ChangeLocation("/orders", null), "Endpoint removed"),
        ]), graph);

        Assert.Empty(impact.Consumers);
        Assert.Empty(impact.Chains);
    }

    [Fact]
    public void El_analisis_desde_un_archivo_encadenado_usa_el_service_del_archivo_como_cabeza()
    {
        NewService("orders", """
            {
              "service": "orders-api",
              "consumers": [
                { "service": "checkout-web", "operations": ["POST /checkout"], "spec": "../checkout/openapi.json" }
              ]
            }
            """);
        NewService("checkout", """
            {
              "service": "checkout-web",
              "consumers": [
                { "service": "web-ui", "operations": ["/pay"] }
              ]
            }
            """);

        var graph = ConsumerRegistryFile.LoadGraphOrDefault(null, Path.Combine(_directory, "orders"));
        var result = new ComparisonResult(
        [
            new ContractChange("CW001", "EndpointRemoved", ChangeSeverity.Breaking, new ChangeLocation("/checkout", "POST"), "Endpoint removed"),
        ]);

        var impact = ImpactAnalyzer.Analyze(result, graph);

        Assert.Equal(["checkout-web", "web-ui"], impact.Consumers.Select(c => c.Service).ToArray());
        var chain = Assert.Single(impact.Chains);
        Assert.Equal(["orders-api", "checkout-web", "web-ui"], chain.Services);
        var trigger = Assert.Single(chain.Triggers);
        Assert.Equal(("CW001", "POST /checkout"), (trigger.RuleId, trigger.Target));
    }
}
