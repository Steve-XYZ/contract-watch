using System.Net;
using System.Text.Json;
using ContractWatch.Core.Comparison;
using ContractWatch.Core.Explanations;

namespace ContractWatch.Core.Tests;

public class OpenAiExplanationProviderTests
{
    private static readonly ContractChange Change = new(
        "CW004", "RequiredPropertyAdded", ChangeSeverity.Breaking,
        new ChangeLocation("/orders", "POST"), "Required request property added: customerId",
        Suggestion: "Introduce the property as optional with a default value.");

    private const string ChatCompletion = """
        {
          "choices": [
            { "message": { "role": "assistant", "content": "Consumers that omit customerId will be rejected." } }
          ]
        }
        """;

    [Fact]
    public async Task Envia_chat_completions_con_modelo_prompt_y_bearer_key()
    {
        var handler = new MockHttpHandler(_ => MockHttpHandler.Json(ChatCompletion));

        var explanation = await new OpenAiExplanationProvider(handler, "sk-test-key", "gpt-x", "https://ai.example/v1")
            .ExplainAsync(Change);

        Assert.Equal("Consumers that omit customerId will be rejected.", explanation);

        Assert.EndsWith("/chat/completions", handler.LastRequest!.RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("sk-test-key", handler.LastRequest.Headers.Authorization.Parameter);

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("gpt-x", body.RootElement.GetProperty("model").GetString());
        Assert.Equal(0.0, body.RootElement.GetProperty("temperature").GetDouble());

        var messages = body.RootElement.GetProperty("messages");
        Assert.True(messages.GetArrayLength() >= 2);
        Assert.Contains("CW004", messages[1].GetProperty("content").GetString());
        Assert.Contains("POST /orders", messages[1].GetProperty("content").GetString());
        Assert.Contains("customerId", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task Sin_modelo_usa_el_default_y_sin_base_url_el_endpoint_publico()
    {
        var handler = new MockHttpHandler(_ => MockHttpHandler.Json(ChatCompletion));

        await new OpenAiExplanationProvider(handler, "sk-test-key").ExplainAsync(Change);

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal(OpenAiExplanationProvider.DefaultModel, body.RootElement.GetProperty("model").GetString());
        Assert.StartsWith("https://api.openai.com/v1/chat/completions", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task La_sugerencia_determinista_viaja_en_el_prompt_para_expandirla()
    {
        var handler = new MockHttpHandler(_ => MockHttpHandler.Json(ChatCompletion));

        await new OpenAiExplanationProvider(handler, "sk-test-key").ExplainAsync(Change);

        Assert.Contains("Introduce the property as optional", handler.LastRequestBody);
    }

    [Fact]
    public async Task Status_no_exitoso_lanza_excepcion_con_el_codigo_y_sin_filtrar_el_cuerpo()
    {
        var handler = new MockHttpHandler(_ => MockHttpHandler.Json("""{ "error": "boom" }""", HttpStatusCode.Unauthorized));
        var provider = new OpenAiExplanationProvider(handler, "sk-test-key");

        var exception = await Assert.ThrowsAsync<ExplanationProviderException>(
            () => provider.ExplainAsync(Change, CancellationToken.None));

        Assert.Contains("401", exception.Message);
        Assert.DoesNotContain("boom", exception.Message);
    }

    [Fact]
    public async Task Respuesta_sin_choices_lanza_excepcion()
    {
        var handler = new MockHttpHandler(_ => MockHttpHandler.Json("""{ "choices": [] }"""));

        await Assert.ThrowsAsync<ExplanationProviderException>(
            () => new OpenAiExplanationProvider(handler, "sk-test-key").ExplainAsync(Change, CancellationToken.None));
    }

    [Fact]
    public async Task Cancelacion_se_propaga()
    {
        var handler = new MockHttpHandler(_ => MockHttpHandler.Json(ChatCompletion));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new OpenAiExplanationProvider(handler, "sk-test-key").ExplainAsync(Change, cts.Token));
    }
}

public class ExplanationProviderFactoryTests
{
    [Fact]
    public void Fake_no_requiere_ninguna_variable_de_entorno()
    {
        var provider = ExplanationProviderFactory.Create(new ExplanationSettings(ExplanationProviders.Fake, null), _ => null);

        Assert.Equal(ExplanationProviders.Fake, provider.Name);
    }

    [Fact]
    public void OpenAi_sin_key_en_el_entorno_lanza_error_configuracion_nombrando_la_variable()
    {
        var exception = Assert.Throws<ExplanationConfigurationException>(
            () => ExplanationProviderFactory.Create(new ExplanationSettings(ExplanationProviders.OpenAi, null), _ => null));

        Assert.Contains(ExplanationOptions.KeyEnvironmentVariable, exception.Message);
    }

    [Fact]
    public void OpenAi_con_key_en_el_entorno_crea_el_proveedor()
    {
        var provider = ExplanationProviderFactory.Create(
            new ExplanationSettings(ExplanationProviders.OpenAi, null),
            name => name == ExplanationOptions.KeyEnvironmentVariable ? "sk-from-env" : null);

        Assert.Equal(ExplanationProviders.OpenAi, provider.Name);
    }

    [Fact]
    public void Base_url_invalida_en_el_entorno_lanza_error_configuracion()
    {
        var exception = Assert.Throws<ExplanationConfigurationException>(
            () => ExplanationProviderFactory.Create(
                new ExplanationSettings(ExplanationProviders.OpenAi, null),
                name => name == ExplanationOptions.KeyEnvironmentVariable ? "sk" : "::no-es-url::"));

        Assert.Contains(ExplanationOptions.BaseUrlEnvironmentVariable, exception.Message);
    }

    [Fact]
    public void Proveedor_desconocido_lanza_error_configuracion()
    {
        Assert.Throws<ExplanationConfigurationException>(
            () => ExplanationProviderFactory.Create(new ExplanationSettings("claude", null), _ => null));
    }
}
