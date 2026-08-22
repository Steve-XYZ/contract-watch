using ContractWatch.Core;
using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Tests;

public class ConsumerRegistryFileTests : IDisposable
{
    private readonly string _directory;

    public ConsumerRegistryFileTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"cw-consumers-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private string Write(string content, string name = "consumers.json")
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Parse_registro_valido_con_metodos_y_solo_paths()
    {
        var path = Write("""
            {
              "consumers": [
                { "service": "admin-web", "operations": ["GET /players/{id}", "POST /bets"] },
                { "service": "reporting-service", "operations": ["/players/{id}"] }
              ]
            }
            """);

        var registry = ConsumerRegistryFile.Load(path);

        Assert.Equal(2, registry.Consumers.Count);
        Assert.Equal("admin-web", registry.Consumers[0].Service);
        Assert.Equal(["GET /players/{id}", "POST /bets"], registry.Consumers[0].Operations);
        Assert.Equal(["/players/{id}"], registry.Consumers[1].Operations);
    }

    [Fact]
    public void Json_malformado_lanza_error_con_la_ruta()
    {
        var path = Write("{ consumers: ");

        var exception = Assert.Throws<ConsumerRegistryException>(() => ConsumerRegistryFile.Load(path));

        Assert.Contains(path, exception.Message);
        Assert.Contains("JSON malformado", exception.Message);
    }

    [Fact]
    public void Servicio_duplicado_lanza_error()
    {
        var path = Write("""
            {
              "consumers": [
                { "service": "admin-web", "operations": ["/orders"] },
                { "service": "admin-web", "operations": ["/payments"] }
              ]
            }
            """);

        var exception = Assert.Throws<ConsumerRegistryException>(() => ConsumerRegistryFile.Load(path));

        Assert.Contains("duplicado", exception.Message);
    }

    [Theory]
    [InlineData("""{ "consumers": [ { "service": "admin-web", "operations": [] } ] }""")]
    [InlineData("""{ "consumers": [ { "service": "admin-web" } ] }""")]
    public void Consumidor_sin_operaciones_lanza_error(string json)
    {
        var path = Write(json);

        var exception = Assert.Throws<ConsumerRegistryException>(() => ConsumerRegistryFile.Load(path));

        Assert.Contains("no tiene operaciones", exception.Message);
    }

    [Fact]
    public void Entrada_malformada_sin_path_valido_lanza_error()
    {
        var path = Write("""{ "consumers": [ { "service": "admin-web", "operations": ["solo-un-token"] } ] }""");

        var exception = Assert.Throws<ConsumerRegistryException>(() => ConsumerRegistryFile.Load(path));

        Assert.Contains("solo-un-token", exception.Message);
        Assert.Contains("'METHOD /path' o '/path'", exception.Message);
    }

    [Fact]
    public void Servicio_vacio_lanza_error()
    {
        var path = Write("""{ "consumers": [ { "service": "  ", "operations": ["/orders"] } ] }""");

        Assert.Throws<ConsumerRegistryException>(() => ConsumerRegistryFile.Load(path));
    }

    [Fact]
    public void LoadOrDefault_sin_archivo_devuelve_registro_vacio_y_analisis_vacio()
    {
        var registry = ConsumerRegistryFile.LoadOrDefault(null, _directory);

        Assert.Empty(registry.Consumers);
        Assert.Empty(ImpactAnalyzer.Analyze(new ComparisonResult(
        [
            new("CW001", "EndpointRemoved", ChangeSeverity.Breaking, new ChangeLocation("/orders", "POST"), "Endpoint removed"),
        ]), registry));
    }

    [Fact]
    public void LoadOrDefault_detecta_el_archivo_por_defecto_en_el_directorio_dado()
    {
        Write("""{ "consumers": [ { "service": "admin-web", "operations": ["/orders"] } ] }""");

        var registry = ConsumerRegistryFile.LoadOrDefault(null, _directory);

        Assert.Single(registry.Consumers);
    }
}
