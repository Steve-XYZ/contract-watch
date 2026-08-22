using ContractWatch.Core;
using ContractWatch.Core.Rules;

namespace ContractWatch.Core.Tests;

public class RulesTests
{
    [Fact]
    public void Quitar_un_metodo_manteniendo_el_path_emite_OperationRemoved_pero_no_EndpointRemoved()
    {
        var previous = new ApiContract(
        [
            TestContracts.Operation("/orders", "GET"),
            TestContracts.Operation("/orders/{id}", "GET"),
            TestContracts.Operation("/orders/{id}", "DELETE"),
        ]);
        var current = new ApiContract(
        [
            TestContracts.Operation("/orders", "GET"),
            TestContracts.Operation("/orders/{id}", "GET"),
        ]);

        var changes = TestContracts.Compare(previous, current).Changes;

        var removed = Assert.Single(changes);
        Assert.Equal("CW002", removed.RuleId);
        Assert.Equal(ChangeSeverity.Breaking, removed.Severity);
        Assert.Equal("Method DELETE removed", removed.Message);
        Assert.Null(new EndpointRemoved().Evaluate(previous, current).FirstOrDefault());
    }

    [Fact]
    public void Quitar_el_path_completo_emite_EndpointRemoved_y_no_OperationRemoved()
    {
        var previous = new ApiContract([TestContracts.Operation("/legacy", "POST")]);
        var current = new ApiContract([]);

        var changes = TestContracts.Compare(previous, current);

        var change = Assert.Single(changes.Changes);
        Assert.Equal("CW001", change.RuleId);
        Assert.Null(change.Location.Method);
        Assert.Empty(new OperationRemoved().Evaluate(previous, current));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Parametro_nuevo_requerido_y_parametro_que_pasa_a_requerido_son_breaking(bool existedBefore)
    {
        var previous = new ApiContract(
        [
            TestContracts.Operation("/orders", "POST",
                parameters: existedBefore ? [TestContracts.Parameter("page", "query", false)] : []),
        ]);
        var current = new ApiContract(
        [
            TestContracts.Operation("/orders", "POST",
                parameters: [TestContracts.Parameter("page", "query", true)]),
        ]);

        var change = Assert.Single(TestContracts.Compare(previous, current).Changes);

        Assert.Equal("CW003", change.RuleId);
        Assert.Equal(existedBefore ? "Parameter became required: page" : "Required parameter added: page", change.Message);
    }

    [Fact]
    public void Parametro_opcional_nuevo_es_compatible()
    {
        var previous = new ApiContract([TestContracts.Operation("/orders", "GET")]);
        var current = new ApiContract(
        [
            TestContracts.Operation("/orders", "GET",
                parameters: [TestContracts.Parameter("locale", "query", false)]),
        ]);

        var change = Assert.Single(TestContracts.Compare(previous, current).Changes);

        Assert.Equal("CW014", change.RuleId);
        Assert.Equal(ChangeSeverity.Compatible, change.Severity);
    }

    [Fact]
    public void Propiedad_que_entra_a_required_en_request_es_breaking()
    {
        var previousSchema = TestContracts.ObjectSchema(
            ["customerId"],
            ("customerId", TestContracts.StringSchema()),
            ("currency", TestContracts.StringSchema("USD", "EUR")));
        var currentSchema = TestContracts.ObjectSchema(
            ["customerId", "currency"],
            ("customerId", TestContracts.StringSchema()),
            ("currency", TestContracts.StringSchema("USD", "EUR")));

        var previous = new ApiContract([TestContracts.Operation("/payments", "POST", requestSchema: previousSchema)]);
        var current = new ApiContract([TestContracts.Operation("/payments", "POST", requestSchema: currentSchema)]);

        var change = Assert.Single(TestContracts.Compare(previous, current).Changes);

        Assert.Equal("CW004", change.RuleId);
        Assert.Equal("Required request property added: currency", change.Message);
    }

    [Fact]
    public void Propiedad_opcional_nueva_en_request_es_compatible()
    {
        var previousSchema = TestContracts.ObjectSchema(["customerId"], ("customerId", TestContracts.StringSchema()));
        var currentSchema = TestContracts.ObjectSchema(
            ["customerId"],
            ("customerId", TestContracts.StringSchema()),
            ("note", TestContracts.StringSchema()));

        var previous = new ApiContract([TestContracts.Operation("/orders", "POST", requestSchema: previousSchema)]);
        var current = new ApiContract([TestContracts.Operation("/orders", "POST", requestSchema: currentSchema)]);

        var change = Assert.Single(TestContracts.Compare(previous, current).Changes);

        Assert.Equal("CW015", change.RuleId);
        Assert.Equal(ChangeSeverity.Compatible, change.Severity);
    }

    [Fact]
    public void Status_de_respuesta_eliminado_es_breaking_y_agregado_es_compatible()
    {
        var previous = new ApiContract(
        [
            TestContracts.Operation("/bets", "POST", responses: new Dictionary<string, ApiResponse>
            {
                ["200"] = TestContracts.Response("200"),
                ["404"] = TestContracts.Response("404"),
            }),
        ]);
        var current = new ApiContract(
        [
            TestContracts.Operation("/bets", "POST", responses: new Dictionary<string, ApiResponse>
            {
                ["200"] = TestContracts.Response("200"),
                ["422"] = TestContracts.Response("422"),
            }),
        ]);

        var changes = TestContracts.Compare(previous, current).Changes;

        Assert.Equal(2, changes.Count);
        var removed = changes.First(c => c.RuleId == "CW007");
        Assert.Equal(ChangeSeverity.Breaking, removed.Severity);
        Assert.Equal("Response status removed: 404", removed.Message);
        var added = changes.First(c => c.RuleId == "CW017");
        Assert.Equal(ChangeSeverity.Compatible, added.Severity);
        Assert.Equal("Response status added: 422", added.Message);
    }

    [Fact]
    public void Contratos_identicos_no_producen_cambios()
    {
        var contract = new ApiContract(
        [
            TestContracts.Operation("/orders", "POST",
                parameters: [TestContracts.Parameter("idempotency-key", "header", false)],
                requestSchema: TestContracts.ObjectSchema(["customerId"], ("customerId", TestContracts.StringSchema())),
                responses: new Dictionary<string, ApiResponse> { ["200"] = TestContracts.Response("200") }),
        ]);

        Assert.Empty(TestContracts.Compare(contract, contract).Changes);
    }
}
