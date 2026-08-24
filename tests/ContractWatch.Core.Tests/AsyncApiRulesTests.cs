using ContractWatch.Core;
using ContractWatch.Core.Comparison;
using ContractWatch.Core.Rules;
using ContractWatch.Core.Parsing;

namespace ContractWatch.Core.Tests;

public class AsyncApiRulesTests
{
    private static ApiContract With(params ApiMessageOperation[] operations) => new([], operations);

    [Fact]
    public void Quitar_un_canal_emite_ChannelRemoved_y_nada_mas()
    {
        var previous = With(
            TestContracts.MessageOperation("legacy/audit", "PUBLISH", MessageDirection.Outbound, TestContracts.ObjectSchema(["event"], ("event", TestContracts.StringSchema()))),
            TestContracts.MessageOperation("orders/events", "PUBLISH", MessageDirection.Outbound));
        var current = With(
            TestContracts.MessageOperation("orders/events", "PUBLISH", MessageDirection.Outbound));

        var changes = TestContracts.Compare(previous, current).Changes;

        var change = Assert.Single(changes);
        Assert.Equal("CW019", change.RuleId);
        Assert.Equal(ChangeSeverity.Breaking, change.Severity);
        Assert.Equal("Channel removed", change.Message);
        Assert.Null(change.Location.Method);
        Assert.NotEmpty(change.Suggestion!);
    }

    [Fact]
    public void Quitar_publish_manteniendo_el_canal_emite_ChannelOperationRemoved_pero_no_ChannelRemoved()
    {
        var previous = With(
            TestContracts.MessageOperation("orders/events", "PUBLISH", MessageDirection.Outbound),
            TestContracts.MessageOperation("orders/events", "SUBSCRIBE", MessageDirection.Inbound));
        var current = With(
            TestContracts.MessageOperation("orders/events", "SUBSCRIBE", MessageDirection.Inbound));

        var changes = TestContracts.Compare(previous, current).Changes;

        var change = Assert.Single(changes);
        Assert.Equal("CW020", change.RuleId);
        Assert.Equal(ChangeSeverity.Breaking, change.Severity);
        Assert.Equal("Action PUBLISH removed", change.Message);
        Assert.Equal("PUBLISH", change.Location.Method);
        Assert.Empty(new ChannelRemoved().Evaluate(previous, current));
    }

    [Fact]
    public void Required_nuevo_en_mensaje_inbound_es_breaking_y_en_outbound_no_emite()
    {
        var previousPayload = TestContracts.ObjectSchema(["paymentId"], ("paymentId", TestContracts.StringSchema()));
        var currentPayload = TestContracts.ObjectSchema(
            ["paymentId", "referenceId"],
            ("paymentId", TestContracts.StringSchema()),
            ("referenceId", TestContracts.StringSchema()));

        var inboundPrevious = With(TestContracts.MessageOperation("payments/instructions", "SUBSCRIBE", MessageDirection.Inbound, previousPayload));
        var inboundCurrent = With(TestContracts.MessageOperation("payments/instructions", "SUBSCRIBE", MessageDirection.Inbound, currentPayload));

        var change = Assert.Single(TestContracts.Compare(inboundPrevious, inboundCurrent).Changes);

        Assert.Equal("CW021", change.RuleId);
        Assert.Equal(ChangeSeverity.Breaking, change.Severity);
        Assert.Equal("Required message property added: referenceId", change.Message);

        var outboundPrevious = inboundPrevious with { MessageOperations = [TestContracts.MessageOperation("payments/instructions", "PUBLISH", MessageDirection.Outbound, previousPayload)] };
        var outboundCurrent = inboundCurrent with { MessageOperations = [TestContracts.MessageOperation("payments/instructions", "PUBLISH", MessageDirection.Outbound, currentPayload)] };

        Assert.Empty(new MessageRequiredPropertyAdded().Evaluate(outboundPrevious, outboundCurrent));
    }

    [Fact]
    public void Cambio_de_tipo_en_payload_emite_MessagePropertyTypeChanged_en_cualquier_direccion()
    {
        foreach (var direction in (MessageDirection[])Enum.GetValues(typeof(MessageDirection)))
        {
            var previous = With(TestContracts.MessageOperation("shipments/status", "PUBLISH", direction,
                TestContracts.ObjectSchema(null, ("eta", TestContracts.StringSchema()))));
            var current = With(TestContracts.MessageOperation("shipments/status", "PUBLISH", direction,
                TestContracts.ObjectSchema(null, ("eta", new ApiSchema(SchemaKind.Number, false, null, null, null, null)))));

            var change = Assert.Single(new MessagePropertyTypeChanged().Evaluate(previous, current));

            Assert.Equal("CW022", change.RuleId);
            Assert.Equal(ChangeSeverity.Breaking, change.Severity);
            Assert.Equal("Message property eta changed: string → number", change.Message);
        }
    }

    [Fact]
    public void Quitar_propiedad_de_mensaje_outbound_es_breaking_y_en_inbound_no_emite()
    {
        var previousPayload = TestContracts.ObjectSchema(
            ["userId"],
            ("userId", TestContracts.StringSchema()),
            ("email", TestContracts.StringSchema()));
        var currentPayload = TestContracts.ObjectSchema(["userId"], ("userId", TestContracts.StringSchema()));

        var outboundPrevious = With(TestContracts.MessageOperation("users/signedup", "PUBLISH", MessageDirection.Outbound, previousPayload));
        var outboundCurrent = With(TestContracts.MessageOperation("users/signedup", "PUBLISH", MessageDirection.Outbound, currentPayload));

        var change = Assert.Single(new MessagePropertyRemoved().Evaluate(outboundPrevious, outboundCurrent));

        Assert.Equal("CW023", change.RuleId);
        Assert.Equal(ChangeSeverity.Breaking, change.Severity);
        Assert.Equal("Message property removed: email", change.Message);

        var inboundPrevious = outboundPrevious with { MessageOperations = [TestContracts.MessageOperation("users/signedup", "SUBSCRIBE", MessageDirection.Inbound, previousPayload)] };
        var inboundCurrent = outboundCurrent with { MessageOperations = [TestContracts.MessageOperation("users/signedup", "SUBSCRIBE", MessageDirection.Inbound, currentPayload)] };

        Assert.Empty(new MessagePropertyRemoved().Evaluate(inboundPrevious, inboundCurrent));
    }

    [Fact]
    public void Enum_ampliado_outbound_es_potential_y_enum_reducido_inbound_es_breaking()
    {
        var outboundPrevious = With(TestContracts.MessageOperation("shipments/status", "PUBLISH", MessageDirection.Outbound,
            TestContracts.ObjectSchema(null, ("state", TestContracts.StringSchema("pending", "shipped")))));
        var outboundCurrent = With(TestContracts.MessageOperation("shipments/status", "PUBLISH", MessageDirection.Outbound,
            TestContracts.ObjectSchema(null, ("state", TestContracts.StringSchema("pending", "shipped", "delivered")))));

        var widened = Assert.Single(new MessageEnumWidened().Evaluate(outboundPrevious, outboundCurrent));

        Assert.Equal("CW024", widened.RuleId);
        Assert.Equal(ChangeSeverity.PotentiallyBreaking, widened.Severity);
        Assert.Equal("Message enum widened: state: pending, shipped → pending, shipped, delivered", widened.Message);
        Assert.Empty(new MessageEnumNarrowed().Evaluate(outboundPrevious, outboundCurrent));

        var inboundPrevious = With(TestContracts.MessageOperation("payments/instructions", "SUBSCRIBE", MessageDirection.Inbound,
            TestContracts.ObjectSchema(null, ("method", TestContracts.StringSchema("card", "transfer")))));
        var inboundCurrent = With(TestContracts.MessageOperation("payments/instructions", "SUBSCRIBE", MessageDirection.Inbound,
            TestContracts.ObjectSchema(null, ("method", TestContracts.StringSchema("card")))));

        var narrowed = Assert.Single(new MessageEnumNarrowed().Evaluate(inboundPrevious, inboundCurrent));

        Assert.Equal("CW025", narrowed.RuleId);
        Assert.Equal(ChangeSeverity.Breaking, narrowed.Severity);
        Assert.Equal("Message enum narrowed: method: card, transfer → card", narrowed.Message);
        Assert.Empty(new MessageEnumWidened().Evaluate(inboundPrevious, inboundCurrent));
    }

    [Fact]
    public void Canal_nuevo_y_propiedad_opcional_nueva_son_compatibles()
    {
        var previous = With(TestContracts.MessageOperation("users/signedup", "PUBLISH", MessageDirection.Outbound,
            TestContracts.ObjectSchema(["userId"], ("userId", TestContracts.StringSchema()))));
        var current = With(
            TestContracts.MessageOperation("users/signedup", "PUBLISH", MessageDirection.Outbound,
                TestContracts.ObjectSchema(
                    ["userId"],
                    ("userId", TestContracts.StringSchema()),
                    ("marketingOptIn", new ApiSchema(SchemaKind.Boolean, false, null, null, null, null)))),
            TestContracts.MessageOperation("refunds/issued", "PUBLISH", MessageDirection.Outbound,
                TestContracts.ObjectSchema(["refundId"], ("refundId", TestContracts.StringSchema()))));

        var changes = TestContracts.Compare(previous, current).Changes;

        Assert.Equal(2, changes.Count);
        var channel = changes.Single(c => c.RuleId == "CW026");
        Assert.Equal(ChangeSeverity.Compatible, channel.Severity);
        Assert.Equal("Channel added", channel.Message);
        Assert.Equal("refunds/issued", channel.Location.Path);

        var optional = changes.Single(c => c.RuleId == "CW027");
        Assert.Equal(ChangeSeverity.Compatible, optional.Severity);
        Assert.Equal("Optional message property added: marketingOptIn", optional.Message);
    }

    [Fact]
    public void Contratos_async_identicos_no_producen_cambios()
    {
        var contract = With(
            TestContracts.MessageOperation("orders/events", "PUBLISH", MessageDirection.Outbound,
                TestContracts.ObjectSchema(
                    ["orderId", "currency"],
                    ("orderId", TestContracts.StringSchema()),
                    ("currency", TestContracts.StringSchema("USD", "EUR")))),
            TestContracts.MessageOperation("billing/commands", "SUBSCRIBE", MessageDirection.Inbound,
                TestContracts.ObjectSchema(["id"], ("id", TestContracts.StringSchema()))));

        Assert.Empty(TestContracts.Compare(contract, contract).Changes);
    }

    [Fact]
    public void Las_reglas_http_no_se_aplican_a_contratos_async()
    {
        var asyncContract = With(TestContracts.MessageOperation("legacy/audit", "PUBLISH", MessageDirection.Outbound));

        Assert.Empty(RuleCatalog.Default.Take(18).SelectMany(rule => rule.Evaluate(asyncContract, new ApiContract([], []))));
    }
}
