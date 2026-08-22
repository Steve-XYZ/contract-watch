using ContractWatch.Core;
using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Tests;

public class ScaffoldingTests : IDisposable
{
    private readonly string _directory;

    public ScaffoldingTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"cw-init-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private string PathOf(string name) => Path.Combine(_directory, name);

    [Fact]
    public void Directorio_vacio_crea_los_tres_archivos_con_plantillas_validas()
    {
        var results = ContractWatchInit.Init(_directory);

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.Equal("created", r.Status));
        Assert.Equal([".contractwatch.json", ".contractwatchignore", "consumers.json"], results.Select(r => r.FileName));

        var policy = PolicyFile.Load(PathOf(".contractwatch.json"));
        Assert.Null(policy.FailOn);
        Assert.Empty(policy.SeverityOverrides);

        Assert.Empty(SuppressionFile.Load(PathOf(".contractwatchignore")));

        var registry = ConsumerRegistryFile.Load(PathOf("consumers.json"));
        Assert.Empty(registry.Consumers);
    }

    [Fact]
    public void Segunda_vuelta_no_sobreescribe_y_reporta_exists()
    {
        var first = ContractWatchInit.Init(_directory);

        var before = File.ReadAllBytes(PathOf(".contractwatch.json"))
            .Concat(File.ReadAllBytes(PathOf(".contractwatchignore")))
            .Concat(File.ReadAllBytes(PathOf("consumers.json")))
            .ToArray();

        var second = ContractWatchInit.Init(_directory);

        var after = File.ReadAllBytes(PathOf(".contractwatch.json"))
            .Concat(File.ReadAllBytes(PathOf(".contractwatchignore")))
            .Concat(File.ReadAllBytes(PathOf("consumers.json")))
            .ToArray();

        Assert.Equal(before, after);
        Assert.All(second, r => Assert.Equal("exists", r.Status));
        Assert.Equal(first.Select(r => r.FileName), second.Select(r => r.FileName));
    }

    [Fact]
    public void Contenido_custom_preexistente_se_preserva_y_solo_se_crea_lo_faltante()
    {
        File.WriteAllText(PathOf(".contractwatch.json"), """{ "failOn": "potentially" }""");
        File.WriteAllText(PathOf("consumers.json"), """
            {
              "consumers": [
                { "service": "admin-web", "operations": ["GET /players/{id}"] }
              ]
            }
            """);

        var results = ContractWatchInit.Init(_directory);

        Assert.Equal(
        [
            new ScaffoldFileResult(".contractwatch.json", "exists"),
            new ScaffoldFileResult(".contractwatchignore", "created"),
            new ScaffoldFileResult("consumers.json", "exists"),
        ], results);

        Assert.Equal("potentially", PolicyFile.Load(PathOf(".contractwatch.json")).FailOn);
        Assert.Single(ConsumerRegistryFile.Load(PathOf("consumers.json")).Consumers);
    }
}
