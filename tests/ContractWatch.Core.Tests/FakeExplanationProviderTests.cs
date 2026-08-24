using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ContractWatch.Core.Comparison;
using ContractWatch.Core.Explanations;

namespace ContractWatch.Core.Tests;

public class FakeExplanationProviderTests
{
    private static readonly ContractChange Change = new(
        "CW003", "RequiredParameterAdded", ChangeSeverity.Breaking,
        new ChangeLocation("/orders", "POST"), "Required parameter added: page");

    [Fact]
    public async Task Produce_texto_determinista_con_regla_operacion_y_mensaje()
    {
        var explanation = await new FakeExplanationProvider().ExplainAsync(Change);

        Assert.NotNull(explanation);
        Assert.Equal(
            "[fake] CW003 (RequiredParameterAdded) at POST /orders: Required parameter added: page.",
            explanation);
    }

    [Fact]
    public async Task Dos_llamadas_con_el_mismo_cambio_producen_el_mismo_texto()
    {
        var provider = new FakeExplanationProvider();

        Assert.Equal(await provider.ExplainAsync(Change), await provider.ExplainAsync(Change));
    }

    [Fact]
    public async Task Cambios_sin_metodo_se_referencian_solo_por_path()
    {
        var change = new ContractChange(
            "CW001", "EndpointRemoved", ChangeSeverity.Breaking,
            new ChangeLocation("/legacy/orders"), "Endpoint removed");

        var explanation = await new FakeExplanationProvider().ExplainAsync(change);

        Assert.Contains("at /legacy/orders:", explanation);
    }
}

public sealed class MockHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastRequest = request;
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return responder(request);
    }

    public static HttpResponseMessage Json(string content, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = new StringContent(content, Encoding.UTF8, new MediaTypeHeaderValue("application/json")),
    };
}
