using ContractWatch.Core;
using ContractWatch.Core.Comparison;
using ContractWatch.Core.Rules;

namespace ContractWatch.Core.Tests;

public class RulesOutputPolicyTests
{
    private static ApiSchema NullableString() => new(SchemaKind.String, true, null, null, null, null);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Propiedad_de_respuesta_que_pasa_a_requerida_emite_RequiredResponsePropertyAdded(bool existedBefore)
    {
        var previousResponse = existedBefore
            ? TestContracts.ObjectSchema(null, ("id", TestContracts.StringSchema()), ("note", TestContracts.StringSchema()))
            : TestContracts.ObjectSchema(null, ("id", TestContracts.StringSchema()));
        var currentResponse = TestContracts.ObjectSchema(
            ["note"],
            ("id", TestContracts.StringSchema()),
            ("note", TestContracts.StringSchema()));
        var previous = new ApiContract(
        [
            TestContracts.Operation("/orders/{id}", "GET",
                responses: new Dictionary<string, ApiResponse> { ["200"] = new("200", previousResponse) }),
        ]);
        var current = new ApiContract(
        [
            TestContracts.Operation("/orders/{id}", "GET",
                responses: new Dictionary<string, ApiResponse> { ["200"] = new("200", currentResponse) }),
        ]);

        var change = Assert.Single(new RequiredResponsePropertyAdded().Evaluate(previous, current));

        Assert.Equal("CW011", change.RuleId);
        Assert.Equal(ChangeSeverity.PotentiallyBreaking, change.Severity);
        Assert.Equal("/orders/{id}", change.Location.Path);
        Assert.Equal("GET", change.Location.Method);
        Assert.Equal("Required response property added: note", change.Message);
    }

    [Fact]
    public void Propiedad_de_respuesta_que_deja_de_ser_nullable_emite_NullableRemoved_con_mensaje_exacto()
    {
        var previousResponse = TestContracts.ObjectSchema(null, ("settledAt", NullableString()));
        var currentResponse = TestContracts.ObjectSchema(null, ("settledAt", TestContracts.StringSchema()));
        var previous = new ApiContract(
        [
            TestContracts.Operation("/payments", "GET",
                responses: new Dictionary<string, ApiResponse> { ["200"] = new("200", previousResponse) }),
        ]);
        var current = new ApiContract(
        [
            TestContracts.Operation("/payments", "GET",
                responses: new Dictionary<string, ApiResponse> { ["200"] = new("200", currentResponse) }),
        ]);

        var change = Assert.Single(new NullableRemoved().Evaluate(previous, current));

        Assert.Equal("CW012", change.RuleId);
        Assert.Equal(ChangeSeverity.PotentiallyBreaking, change.Severity);
        Assert.Equal("Response property settledAt changed: string|null → string", change.Message);
    }

    [Fact]
    public void Propiedad_que_cambia_de_kind_junto_con_la_nullabilidad_no_emite_NullableRemoved()
    {
        var previousResponse = TestContracts.ObjectSchema(null, ("amount", NullableString()));
        var currentResponse = TestContracts.ObjectSchema(null, ("amount", new ApiSchema(SchemaKind.Integer, false, null, null, null, null)));
        var previous = new ApiContract(
        [
            TestContracts.Operation("/payments", "GET",
                responses: new Dictionary<string, ApiResponse> { ["200"] = new("200", previousResponse) }),
        ]);
        var current = new ApiContract(
        [
            TestContracts.Operation("/payments", "GET",
                responses: new Dictionary<string, ApiResponse> { ["200"] = new("200", currentResponse) }),
        ]);

        var change = Assert.Single(new ResponsePropertyTypeChanged().Evaluate(previous, current));
        Assert.Empty(new NullableRemoved().Evaluate(previous, current));

        Assert.Equal("CW008", change.RuleId);
    }

    [Fact]
    public void Operacion_que_solo_cambia_la_description_emite_MetadataOnlyChanged()
    {
        var previous = new ApiContract([TestContracts.Operation("/refunds", "GET")]);
        var current = new ApiContract([TestContracts.Operation("/refunds", "GET") with { Description = "Devuelve los reembolsos." }]);

        var change = Assert.Single(new MetadataOnlyChanged().Evaluate(previous, current));

        Assert.Equal("CW018", change.RuleId);
        Assert.Equal(ChangeSeverity.Compatible, change.Severity);
        Assert.Equal("/refunds", change.Location.Path);
        Assert.Equal("Operation metadata updated", change.Message);
    }

    [Fact]
    public void Cambio_estructural_aun_con_description_distinta_no_emite_MetadataOnlyChanged()
    {
        var previousResponse = TestContracts.ObjectSchema(null, ("total", new ApiSchema(SchemaKind.Number, false, null, null, null, null)));
        var currentResponse = TestContracts.ObjectSchema(null, ("total", TestContracts.StringSchema()));
        var previous = new ApiContract(
        [
            TestContracts.Operation("/payments", "GET",
                responses: new Dictionary<string, ApiResponse> { ["200"] = new("200", previousResponse) },
                requestSchema: TestContracts.StringSchema()),
        ]);
        var current = new ApiContract(
        [
            TestContracts.Operation("/payments", "GET",
                responses: new Dictionary<string, ApiResponse> { ["200"] = new("200", currentResponse) },
                requestSchema: TestContracts.StringSchema()) with { Description = "Ahora con detalle." },
        ]);

        Assert.False(OperationStructure.Equal(previous.Operations[0], current.Operations[0]));
        Assert.Empty(new MetadataOnlyChanged().Evaluate(previous, current));
    }

    [Fact]
    public void OperationStructure_Equal_ignora_el_orden_de_parametros_y_valores_de_enum()
    {
        var previousParameter = new ApiParameter("limit", "query", false,
            new ApiSchema(SchemaKind.String, false, "date-time", ["USD", "EUR"], null, null));
        var previous = new ApiContract(
        [
            TestContracts.Operation("/orders", "POST",
                parameters:
                [
                    previousParameter,
                    new ApiParameter("locale", "header", false, null),
                ],
                requestSchema: TestContracts.ObjectSchema(["customerId"], ("currency", TestContracts.StringSchema("USD", "EUR")))),
        ]);
        var current = new ApiContract(
        [
            TestContracts.Operation("/orders", "POST",
                parameters:
                [
                    new ApiParameter("locale", "header", false, null),
                    new ApiParameter("limit", "query", false,
                        new ApiSchema(SchemaKind.String, false, "date-time", ["EUR", "USD", "USD"], null, null)),
                ],
                requestSchema: TestContracts.ObjectSchema(["customerId"], ("currency", TestContracts.StringSchema("EUR", "USD")))),
        ]);

        Assert.True(OperationStructure.Equal(previous.Operations[0], current.Operations[0]));
    }

    [Fact]
    public void OperationStructure_Equal_detecta_diferencias_reales()
    {
        var baseParameter = new ApiParameter("limit", "query", false, TestContracts.StringSchema("USD"));
        var previous = new ApiContract(
        [
            TestContracts.Operation("/orders", "POST", parameters: [baseParameter]),
        ]);
        var widenedEnum = new ApiContract(
        [
            TestContracts.Operation("/orders", "POST",
                parameters: [new ApiParameter("limit", "query", false, TestContracts.StringSchema("USD", "EUR"))]),
        ]);
        var extraRequired = new ApiContract(
        [
            TestContracts.Operation("/orders", "POST", parameters: [new ApiParameter("limit", "query", true, TestContracts.StringSchema("USD"))]),
        ]);

        Assert.False(OperationStructure.Equal(previous.Operations[0], widenedEnum.Operations[0]));
        Assert.False(OperationStructure.Equal(previous.Operations[0], extraRequired.Operations[0]));
    }
}
