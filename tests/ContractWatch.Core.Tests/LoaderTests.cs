using ContractWatch.Core.Parsing;

namespace ContractWatch.Core.Tests;

public class LoaderTests
{
    [Fact]
    public async Task Carga_todas_las_operaciones_con_metodos_en_mayusculas()
    {
        var contract = await OpenApiLoader.LoadAsync(FixturePath.Resolve("v1.json"));

        var expected = new HashSet<(string Path, string Method)>
        {
            ("/orders", "POST"),
            ("/orders", "GET"),
            ("/orders/{id}", "GET"),
            ("/orders/{id}", "DELETE"),
            ("/legacy/orders", "POST"),
        };

        Assert.Equal(expected, contract.Operations.Select(o => (o.Path, o.Method)).ToHashSet());
    }

    [Fact]
    public async Task Resuelve_refs_del_request_body_y_mapea_el_schema()
    {
        var contract = await OpenApiLoader.LoadAsync(FixturePath.Resolve("v1.json"));

        var schema = contract.Operations.Single(o => o.Path == "/orders" && o.Method == "POST").RequestJsonSchema;

        Assert.NotNull(schema);
        Assert.Equal(SchemaKind.Object, schema!.Kind);
        Assert.Equal(new[] { "customerId" }, schema.RequiredProperties);
        Assert.Equal(["USD", "EUR"], schema.Properties!["currency"].EnumValues);
        Assert.Equal(SchemaKind.String, schema.Properties!["customerId"].Kind);
    }

    [Fact]
    public async Task Combina_parametros_del_path_item_y_de_la_operacion()
    {
        var contract = await OpenApiLoader.LoadAsync(FixturePath.Resolve("v1.json"));

        var parameters = contract.Operations.Single(o => o.Path == "/orders/{id}" && o.Method == "GET").Parameters;

        var id = parameters.Single(p => p.Name == "id");
        Assert.Equal("path", id.In);
        Assert.True(id.IsRequired);

        var verbose = parameters.Single(p => p.Name == "verbose");
        Assert.Equal("query", verbose.In);
        Assert.False(verbose.IsRequired);
    }

    [Fact]
    public async Task Mapea_los_status_codes_documentados()
    {
        var contract = await OpenApiLoader.LoadAsync(FixturePath.Resolve("v1.json"));

        var statuses = contract.Operations.Single(o => o.Path == "/orders" && o.Method == "POST").Responses.Keys.ToHashSet();

        Assert.Equal(new HashSet<string> { "200", "404" }, statuses);
    }

    [Fact]
    public async Task Falla_con_documento_invalido()
    {
        var path = Path.Combine(Path.GetTempPath(), $"contractwatch-invalid-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{ \"openapi\": \"3.0.3\" }");

        try
        {
            await Assert.ThrowsAsync<ContractLoadException>(() => OpenApiLoader.LoadAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
