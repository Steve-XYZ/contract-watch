using ContractWatch.Core;
using ContractWatch.Core.Parsing;

namespace ContractWatch.Core.Tests;

public class AsyncApiLoaderTests
{
    [Fact]
    public async Task Carga_los_canales_v2_con_acciones_en_mayusculas_y_direccion()
    {
        var contract = await AsyncContract();

        var expected = new HashSet<(string Channel, string Action)>
        {
            ("legacy/audit", "PUBLISH"),
            ("orders/events", "PUBLISH"),
            ("orders/events", "SUBSCRIBE"),
            ("payments/instructions", "SUBSCRIBE"),
            ("shipments/status", "PUBLISH"),
            ("users/signedup", "PUBLISH"),
        };

        Assert.Equal(expected, contract.MessageOperations!.Select(o => (o.Channel, o.Action)).ToHashSet());
        Assert.All(contract.MessageOperations!.Where(o => o.Action == "SUBSCRIBE"), o => Assert.Equal(MessageDirection.Inbound, o.Direction));
        Assert.All(contract.MessageOperations!.Where(o => o.Action == "PUBLISH"), o => Assert.Equal(MessageDirection.Outbound, o.Direction));
        Assert.Empty(contract.Operations);
    }

    [Fact]
    public async Task Resuelve_refs_del_payload_y_mapea_el_schema()
    {
        var contract = await AsyncContract();

        var payload = contract.MessageOperations!.Single(o => o.Channel == "payments/instructions").PayloadSchema;

        Assert.NotNull(payload);
        Assert.Equal(SchemaKind.Object, payload!.Kind);
        Assert.Equal(new[] { "paymentId", "method" }, payload.RequiredProperties);
        Assert.Equal(["card", "transfer"], payload.Properties!["method"].EnumValues);
        Assert.Equal(SchemaKind.String, payload.Properties!["paymentId"].Kind);
    }

    [Fact]
    public async Task Mapea_documentos_v3_resolviendo_operaciones_y_mensajes_referenciados()
    {
        var contract = await LoadAsync(FixturePath.Resolve("asyncapi-v3.json"));

        var send = contract.MessageOperations!.Single(o => o.Action == "SEND");
        Assert.Equal("orders/events", send.Channel);
        Assert.Equal(MessageDirection.Outbound, send.Direction);
        Assert.Equal(SchemaKind.Object, send.PayloadSchema!.Kind);
        Assert.Equal(["orderId"], send.PayloadSchema.RequiredProperties);

        var receive = contract.MessageOperations!.Single(o => o.Action == "RECEIVE");
        Assert.Equal("billing/commands", receive.Channel);
        Assert.Equal(MessageDirection.Inbound, receive.Direction);
        Assert.Equal(["card", "transfer"], receive.PayloadSchema!.Properties!["method"].EnumValues);
    }

    [Fact]
    public async Task Un_mensaje_oneOf_toma_el_primer_miembro()
    {
        var path = await TempSpecAsync("""
            {
              "asyncapi": "2.6.0",
              "channels": {
                "orders/created": {
                  "publish": {
                    "message": {
                      "oneOf": [
                        { "$ref": "#/components/messages/OrderCreated" },
                        { "payload": { "type": "object" } }
                      ]
                    }
                  }
                }
              },
              "components": {
                "messages": {
                  "OrderCreated": { "payload": { "$ref": "#/components/schemas/OrderCreated" } }
                },
                "schemas": {
                  "OrderCreated": { "type": "object", "required": ["orderId"], "properties": { "orderId": { "type": "string" } } }
                }
              }
            }
            """);

        try
        {
            var contract = await LoadAsync(path);
            var payload = contract.MessageOperations!.Single().PayloadSchema;

            Assert.NotNull(payload);
            Assert.Equal(new[] { "orderId" }, payload!.RequiredProperties);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Version_no_soportada_lanza_error_claro()
    {
        var path = await TempSpecAsync("""{ "asyncapi": "4.0.0", "channels": {} }""");

        try
        {
            var exception = await Assert.ThrowsAsync<UnsupportedSpecException>(() => SpecLoader.LoadAsync(path));

            Assert.Contains("4.0.0", exception.Message);
            Assert.Contains(path, exception.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Ref_externo_lanza_error_claro()
    {
        var path = await TempSpecAsync("""
            {
              "asyncapi": "2.6.0",
              "channels": {
                "orders/created": {
                  "publish": {
                    "message": { "$ref": "./otros-mensajes.json#/messages/OrderCreated" }
                  }
                }
              }
            }
            """);

        try
        {
            var exception = await Assert.ThrowsAsync<UnsupportedSpecException>(() => SpecLoader.LoadAsync(path));

            Assert.Contains("$ref locales", exception.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task<ApiContract> AsyncContract() =>
        await LoadAsync(FixturePath.Resolve("asyncapi-v1.json"));

    private static async Task<ApiContract> LoadAsync(string path) =>
        (await SpecLoader.LoadAsync(path)).Contract;

    private static async Task<string> TempSpecAsync(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"contractwatch-async-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, content);
        return path;
    }
}
