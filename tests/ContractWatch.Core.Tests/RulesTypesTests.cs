using ContractWatch.Core;
using ContractWatch.Core.Rules;

namespace ContractWatch.Core.Tests;

public class RulesTypesTests
{
    private static ApiParameter TypedParameter(string name, string @in, bool required, ApiSchema? schema) =>
        new(name, @in, required, schema);

    [Fact]
    public void Parametro_que_cambia_de_tipo_emite_ParameterTypeChanged_con_mensaje_exacto()
    {
        var previous = new ApiContract(
        [
            TestContracts.Operation("/orders", "POST",
                parameters: [TypedParameter("limit", "query", false, new ApiSchema(SchemaKind.Integer, false, null, null, null, null))]),
        ]);
        var current = new ApiContract(
        [
            TestContracts.Operation("/orders", "POST",
                parameters: [TypedParameter("limit", "query", false, TestContracts.StringSchema())]),
        ]);

        var change = Assert.Single(new ParameterTypeChanged().Evaluate(previous, current));

        Assert.Equal("CW005", change.RuleId);
        Assert.Equal(ChangeSeverity.Breaking, change.Severity);
        Assert.Equal("/orders", change.Location.Path);
        Assert.Equal("POST", change.Location.Method);
        Assert.Equal("Parameter limit changed: integer → string", change.Message);
    }

    [Fact]
    public void Propiedad_de_respuesta_que_cambia_de_tipo_emite_ResponsePropertyTypeChanged()
    {
        var previousResponse = TestContracts.ObjectSchema(null, ("amount", new ApiSchema(SchemaKind.Number, false, null, null, null, null)));
        var currentResponse = TestContracts.ObjectSchema(null, ("amount", TestContracts.StringSchema()));
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

        var change = Assert.Single(new ResponsePropertyTypeChanged().Evaluate(previous, current));

        Assert.Equal("CW008", change.RuleId);
        Assert.Equal(ChangeSeverity.Breaking, change.Severity);
        Assert.Equal("Response property amount changed: number → string", change.Message);
    }

    [Fact]
    public void Propiedad_de_respuesta_eliminada_emite_ResponsePropertyRemoved_incluso_si_era_opcional()
    {
        var previousResponse = TestContracts.ObjectSchema(
            null,
            ("amount", new ApiSchema(SchemaKind.Number, false, null, null, null, null)),
            ("createdAt", TestContracts.StringSchema()));
        var currentResponse = TestContracts.ObjectSchema(null, ("amount", new ApiSchema(SchemaKind.Number, false, null, null, null, null)));
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

        var change = Assert.Single(new ResponsePropertyRemoved().Evaluate(previous, current));

        Assert.Equal("CW009", change.RuleId);
        Assert.Equal(ChangeSeverity.Breaking, change.Severity);
        Assert.Equal("Response property removed: createdAt", change.Message);
    }

    [Fact]
    public void Sin_cambio_de_tipo_solo_nullabilidad_no_emite_las_reglas_de_tipo()
    {
        var previousParameter = TypedParameter("limit", "query", false, new ApiSchema(SchemaKind.Integer, false, null, null, null, null));
        var currentParameter = TypedParameter("limit", "query", false, new ApiSchema(SchemaKind.Integer, true, null, null, null, null));
        var previous = new ApiContract(
        [
            TestContracts.Operation("/orders", "POST", parameters: [previousParameter]),
        ]);
        var current = new ApiContract(
        [
            TestContracts.Operation("/orders", "POST", parameters: [currentParameter]),
        ]);

        Assert.Empty(new ParameterTypeChanged().Evaluate(previous, current));
        Assert.Empty(new ResponsePropertyTypeChanged().Evaluate(previous, current));
        Assert.Empty(new ResponsePropertyRemoved().Evaluate(previous, current));
        Assert.Empty(TestContracts.Compare(previous, current).Changes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Parametro_sin_schema_en_uno_de_los_lados_no_emite_ParameterTypeChanged(bool schemaEnPrevio)
    {
        var typedSchema = new ApiSchema(SchemaKind.Integer, false, null, null, null, null);
        var previous = new ApiContract(
        [
            TestContracts.Operation("/orders", "GET",
                parameters: [TypedParameter("limit", "query", false, schemaEnPrevio ? typedSchema : null)]),
        ]);
        var current = new ApiContract(
        [
            TestContracts.Operation("/orders", "GET",
                parameters: [TypedParameter("limit", "query", false, schemaEnPrevio ? null : typedSchema)]),
        ]);

        Assert.Empty(new ParameterTypeChanged().Evaluate(previous, current));
    }

    [Fact]
    public void Respuesta_sin_json_schema_en_uno_de_los_lados_no_emite_reglas_de_propiedades()
    {
        var previous = new ApiContract(
        [
            TestContracts.Operation("/orders/{id}", "GET",
                responses: new Dictionary<string, ApiResponse> { ["200"] = new("200", TestContracts.ObjectSchema(null, ("amount", TestContracts.StringSchema()))) }),
        ]);
        var current = new ApiContract(
        [
            TestContracts.Operation("/orders/{id}", "GET",
                responses: new Dictionary<string, ApiResponse> { ["200"] = TestContracts.Response("200") }),
        ]);

        Assert.Empty(new ResponsePropertyTypeChanged().Evaluate(previous, current));
        Assert.Empty(new ResponsePropertyRemoved().Evaluate(previous, current));
    }
}
