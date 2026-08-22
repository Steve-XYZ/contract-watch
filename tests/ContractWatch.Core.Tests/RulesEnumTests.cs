using ContractWatch.Core;
using ContractWatch.Core.Rules;

namespace ContractWatch.Core.Tests;

public class RulesEnumTests
{
    private static ApiParameter EnumParameter(string name, string @in, bool required, ApiSchema? schema) =>
        new(name, @in, required, schema);

    private static ApiContract WithParameter(string path, string method, ApiParameter parameter) =>
        new([TestContracts.Operation(path, method, parameters: [parameter])]);

    private static ApiContract WithRequestProperty(string path, string method, string name, ApiSchema schema) =>
        new([TestContracts.Operation(path, method, requestSchema: TestContracts.ObjectSchema(null, (name, schema)))]);

    private static ApiContract WithResponseProperty(string path, string method, string name, ApiSchema schema) =>
        new(
        [
            TestContracts.Operation(path, method,
                responses: new Dictionary<string, ApiResponse> { ["200"] = new("200", TestContracts.ObjectSchema(null, (name, schema))) }),
        ]);

    [Fact]
    public void Parametro_de_query_con_enum_restringido_emite_RequestEnumNarrowed_breaking()
    {
        var previous = WithParameter("/payments", "POST", EnumParameter("currency", "query", false, TestContracts.StringSchema("USD", "EUR")));
        var current = WithParameter("/payments", "POST", EnumParameter("currency", "query", false, TestContracts.StringSchema("USD")));

        var change = Assert.Single(new RequestEnumNarrowed().Evaluate(previous, current));

        Assert.Equal("CW006", change.RuleId);
        Assert.Equal("RequestEnumNarrowed", change.RuleName);
        Assert.Equal(ChangeSeverity.Breaking, change.Severity);
        Assert.Equal("/payments", change.Location.Path);
        Assert.Equal("POST", change.Location.Method);
        Assert.Equal("Request enum narrowed: currency: USD, EUR → USD", change.Message);
    }

    [Fact]
    public void Propiedad_de_request_con_enum_ampliado_emite_RequestEnumWidened_compatible()
    {
        var previous = WithRequestProperty("/orders", "POST", "currency", TestContracts.StringSchema("USD"));
        var current = WithRequestProperty("/orders", "POST", "currency", TestContracts.StringSchema("USD", "EUR"));

        var change = Assert.Single(new RequestEnumWidened().Evaluate(previous, current));

        Assert.Equal("CW016", change.RuleId);
        Assert.Equal("RequestEnumWidened", change.RuleName);
        Assert.Equal(ChangeSeverity.Compatible, change.Severity);
        Assert.Equal("Request enum widened: currency: USD → USD, EUR", change.Message);
    }

    [Fact]
    public void Propiedad_de_respuesta_con_enum_ampliado_emite_ResponseEnumWidened_potentially_breaking()
    {
        var previous = WithResponseProperty("/bets/{id}", "GET", "status", TestContracts.StringSchema("PAID", "FAILED"));
        var current = WithResponseProperty("/bets/{id}", "GET", "status", TestContracts.StringSchema("PAID", "FAILED", "PENDING"));

        var change = Assert.Single(new ResponseEnumWidened().Evaluate(previous, current));

        Assert.Equal("CW010", change.RuleId);
        Assert.Equal("ResponseEnumWidened", change.RuleName);
        Assert.Equal(ChangeSeverity.PotentiallyBreaking, change.Severity);
        Assert.Equal("Response enum widened: status: PAID, FAILED → PAID, FAILED, PENDING", change.Message);
    }

    [Fact]
    public void Enums_iguales_no_emiten_ninguna_regla_de_enum()
    {
        var previous = WithParameter("/payments", "POST", EnumParameter("currency", "query", false, TestContracts.StringSchema("USD", "EUR")));
        var current = WithParameter("/payments", "POST", EnumParameter("currency", "query", false, TestContracts.StringSchema("USD", "EUR")));

        Assert.Empty(new RequestEnumNarrowed().Evaluate(previous, current));
        Assert.Empty(new RequestEnumWidened().Evaluate(previous, current));
        Assert.Empty(new ResponseEnumWidened().Evaluate(previous, current));
        Assert.Empty(TestContracts.Compare(previous, current).Changes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Enum_presente_en_un_solo_lado_no_emite_ninguna_regla_de_enum(bool enumEnPrevio)
    {
        var previous = WithParameter("/payments", "POST", EnumParameter("currency", "query", false, enumEnPrevio ? TestContracts.StringSchema("USD") : TestContracts.StringSchema()));
        var current = WithParameter("/payments", "POST", EnumParameter("currency", "query", false, enumEnPrevio ? TestContracts.StringSchema() : TestContracts.StringSchema("USD")));

        Assert.Empty(new RequestEnumNarrowed().Evaluate(previous, current));
        Assert.Empty(new RequestEnumWidened().Evaluate(previous, current));
        Assert.Empty(new ResponseEnumWidened().Evaluate(previous, current));
    }

    [Fact]
    public void Mismo_conjunto_con_distinto_orden_y_duplicados_no_emite_nada_por_semantica_de_conjuntos()
    {
        var previous = WithParameter("/payments", "POST", EnumParameter("currency", "query", false, TestContracts.StringSchema("EUR", "USD", "EUR")));
        var current = WithParameter("/payments", "POST", EnumParameter("currency", "query", false, TestContracts.StringSchema("USD", "EUR")));

        Assert.Empty(new RequestEnumNarrowed().Evaluate(previous, current));
        Assert.Empty(new RequestEnumWidened().Evaluate(previous, current));
        Assert.Empty(new ResponseEnumWidened().Evaluate(previous, current));
        Assert.Empty(TestContracts.Compare(previous, current).Changes);
    }
}
