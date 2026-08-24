using ContractWatch.Core.Comparison;
using ContractWatch.Core.Explanations;

namespace ContractWatch.Core.Tests;

public class ExplanationOptionsTests
{
    [Fact]
    public void Sin_flag_ni_policy_la_explicacion_queda_desactivada()
    {
        Assert.Null(ExplanationOptions.Resolve(null, null, null, null));
    }

    [Fact]
    public void El_flag_tiene_precedencia_sobre_la_policy()
    {
        var settings = ExplanationOptions.Resolve("fake", "openai", null, "gpt-policy");

        Assert.Equal("fake", settings!.Provider);
        Assert.Equal("gpt-policy", settings.Model);
    }

    [Fact]
    public void La_policy_activa_cuando_no_hay_flag()
    {
        var settings = ExplanationOptions.Resolve(null, "openai", null, null);

        Assert.Equal(ExplanationProviders.OpenAi, settings!.Provider);
    }

    [Fact]
    public void El_modelo_del_flag_pisa_el_de_la_policy()
    {
        var settings = ExplanationOptions.Resolve("openai", "openai", "gpt-flag", "gpt-policy");

        Assert.Equal("gpt-flag", settings!.Model);
    }
}

public class ExplanationEnricherTests
{
    private static readonly ComparisonResult Result = new(
    [
        new ContractChange("CW003", "RequiredParameterAdded", ChangeSeverity.Breaking,
            new ChangeLocation("/orders", "POST"), "Required parameter added: page",
            Suggestion: "Introduce the parameter as optional."),
        new ContractChange("CW010", "ResponseEnumWidened", ChangeSeverity.PotentiallyBreaking,
            new ChangeLocation("/payments", "GET"), "Response enum widened: + PENDING",
            Suggestion: "Announce the new case in the changelog."),
    ]);

    private sealed class ThrowingProvider : IExplanationProvider
    {
        public string Name => "throwing";

        public Task<string?> ExplainAsync(ContractChange change, CancellationToken cancellationToken = default) =>
            throw new ExplanationProviderException("el endpoint de chat-completions respondió 500");
    }

    private sealed class ConditionalProvider : IExplanationProvider
    {
        public string Name => "conditional";

        public Task<string?> ExplainAsync(ContractChange change, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(change.RuleId == "CW003" ? $"explicación para {change.RuleId}" : throw new InvalidOperationException("fallo puntual"));
    }

    [Fact]
    public async Task Enriquece_cada_cambio_manteniendo_el_orden_y_la_sugerencia()
    {
        var outcome = await ExplanationEnricher.EnrichAsync(Result, new FakeExplanationProvider());

        Assert.Equal(0, outcome.Failures);
        Assert.Null(outcome.FirstFailureReason);
        Assert.Equal(2, outcome.Result.Changes.Count);
        Assert.Equal("CW003", outcome.Result.Changes[0].RuleId);
        Assert.Equal("[fake] CW003 (RequiredParameterAdded) at POST /orders: Required parameter added: page.", outcome.Result.Changes[0].Explanation);
        Assert.Contains("page", outcome.Result.Changes[0].Message);

        var second = outcome.Result.Changes[1];
        Assert.Contains("[fake] CW010", second.Explanation);
    }

    [Fact]
    public async Task Fallo_del_proveedor_degrada_al_sugerencia_determinista_y_cuenta_una_vez()
    {
        var outcome = await ExplanationEnricher.EnrichAsync(Result, new ThrowingProvider());

        Assert.Equal(2, outcome.Failures);
        Assert.Contains("500", outcome.FirstFailureReason);

        foreach (var change in outcome.Result.Changes)
        {
            Assert.Null(change.Explanation);
            Assert.NotNull(change.Suggestion);
        }
    }

    [Fact]
    public async Task Fallo_puntual_degrada_solo_ese_cambio()
    {
        var outcome = await ExplanationEnricher.EnrichAsync(Result, new ConditionalProvider());

        Assert.Equal(1, outcome.Failures);
        Assert.Equal("explicación para CW003", outcome.Result.Changes[0].Explanation);
        Assert.Null(outcome.Result.Changes[1].Explanation);
        Assert.Contains("fallo puntual", outcome.FirstFailureReason);
    }
}
