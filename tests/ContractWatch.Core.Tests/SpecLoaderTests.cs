using ContractWatch.Core.Parsing;

namespace ContractWatch.Core.Tests;

public class SpecLoaderTests
{
    [Fact]
    public async Task Detecta_openapi_por_contenido()
    {
        var spec = await SpecLoader.LoadAsync(FixturePath.Resolve("v1.json"));

        Assert.Equal(SpecKind.OpenApi, spec.Kind);
        Assert.Equal(5, spec.Contract.Operations.Count);
    }

    [Fact]
    public async Task Detecta_asyncapi_por_contenido()
    {
        var spec = await SpecLoader.LoadAsync(FixturePath.Resolve("asyncapi-v1.json"));

        Assert.Equal(SpecKind.AsyncApi, spec.Kind);
        Assert.Equal(6, spec.Contract.MessageOperations!.Count);
    }

    [Fact]
    public async Task Mezclar_kinds_lanza_error_con_rutas_y_tipos()
    {
        var openApi = await SpecLoader.LoadAsync(FixturePath.Resolve("v1.json"));
        var asyncApi = await SpecLoader.LoadAsync(FixturePath.Resolve("asyncapi-v1.json"));

        var exception = Assert.Throws<MixedSpecKindsException>(
            () => SpecLoader.EnsureSameKind(openApi, asyncApi, "main/openapi.json", "pr/asyncapi.json"));

        Assert.Contains("main/openapi.json", exception.Message);
        Assert.Contains("pr/asyncapi.json", exception.Message);
        Assert.Contains("OpenAPI", exception.Message);
        Assert.Contains("AsyncAPI", exception.Message);

        SpecLoader.EnsureSameKind(openApi, openApi, "a", "b");
        SpecLoader.EnsureSameKind(asyncApi, asyncApi, "a", "b");
    }

    [Fact]
    public async Task Asyncapi_en_yaml_lanza_error_que_sugiere_json()
    {
        var path = Path.Combine(Path.GetTempPath(), $"contractwatch-async-{Guid.NewGuid():N}.yaml");
        await File.WriteAllTextAsync(path, """
            asyncapi: '2.6.0'
            info:
              title: Orders Events
              version: 1.0.0
            channels:
              orders/created:
                publish:
                  message:
                    payload:
                      type: object
            """);

        try
        {
            var exception = await Assert.ThrowsAsync<UnsupportedSpecException>(() => SpecLoader.LoadAsync(path));

            Assert.Contains("YAML", exception.Message);
            Assert.Contains(path, exception.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Un_json_que_no_es_ni_openapi_ni_asyncapi_cae_en_el_error_de_openapi()
    {
        var path = Path.Combine(Path.GetTempPath(), $"contractwatch-otro-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{ \"swagger\": \"2.0\" }");

        try
        {
            await Assert.ThrowsAsync<ContractLoadException>(() => SpecLoader.LoadAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
