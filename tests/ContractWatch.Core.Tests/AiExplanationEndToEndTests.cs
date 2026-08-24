using System.Text.Json;
using ContractWatch.Core.Comparison;
using ContractWatch.Core.Explanations;
using ContractWatch.Core.Parsing;
using ContractWatch.Core.Reporting;

namespace ContractWatch.Core.Tests;

public class AiExplanationEndToEndTests : IDisposable
{
    private readonly string _historyDirectory = Path.Combine(Path.GetTempPath(), $"cw-explain-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_historyDirectory))
            Directory.Delete(_historyDirectory, recursive: true);
    }

    [Fact]
    public async Task El_proveedor_fake_llega_a_los_cuatro_reportes_por_todo_el_pipeline()
    {
        var previous = await OpenApiLoader.LoadAsync(FixturePath.Resolve("v1.json"));
        var current = await OpenApiLoader.LoadAsync(FixturePath.Resolve("v2.json"));

        var result = SuppressionFile.Apply(
            PolicyFile.Apply(TestContracts.Compare(previous, current), new ContractPolicy(null, new Dictionary<string, ChangeSeverity>())),
            []);
        var outcome = await ExplanationEnricher.EnrichAsync(result, ExplanationProviderFactory.Create(new ExplanationSettings(ExplanationProviders.Fake, null)));
        var explained = outcome.Result;

        Assert.Equal(0, outcome.Failures);
        Assert.NotEmpty(explained.Changes);
        Assert.All(explained.Changes, c => Assert.NotNull(c.Explanation));

        var console = ConsoleReporter.Render(explained.Changes);
        Assert.Contains("↳ IA: [fake] CW", console);

        var markdown = MarkdownReporter.Render(explained);
        Assert.Contains("| Severity | Operation | Change | Rule | Suggestion | AI |", markdown);
        Assert.Contains("[fake] CW", markdown);

        var json = JsonReporter.Render(explained);
        using var jsonDocument = JsonDocument.Parse(json);
        Assert.All(jsonDocument.RootElement.GetProperty("changes").EnumerateArray(),
            entry => Assert.Equal(JsonValueKind.String, entry.GetProperty("explanation").ValueKind));

        var sarif = SarifReporter.Render(explained, "openapi.json");
        using var sarifDocument = JsonDocument.Parse(sarif);
        Assert.All(sarifDocument.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray(),
            entry => Assert.Equal(JsonValueKind.String, entry.GetProperty("properties").GetProperty("explanation").ValueKind));
    }

    [Fact]
    public async Task Sin_explain_la_salida_queda_identica_y_explanation_es_null()
    {
        var previous = await OpenApiLoader.LoadAsync(FixturePath.Resolve("v1.json"));
        var current = await OpenApiLoader.LoadAsync(FixturePath.Resolve("v2.json"));

        var result = TestContracts.Compare(previous, current);

        Assert.DoesNotContain("↳ IA:", ConsoleReporter.Render(result.Changes));
        Assert.DoesNotContain("| AI |", MarkdownReporter.Render(result));
        Assert.DoesNotContain("\"explanation\": \"", JsonReporter.Render(result));
    }

    [Fact]
    public async Task El_reporte_guardado_en_el_historial_embeda_la_explicacion_pero_nunca_la_key()
    {
        const string secretKey = "sk-secret-never-persisted";
        var previous = await OpenApiLoader.LoadAsync(FixturePath.Resolve("v1.json"));
        var current = await OpenApiLoader.LoadAsync(FixturePath.Resolve("v2.json"));

        var result = TestContracts.Compare(previous, current);
        var outcome = await ExplanationEnricher.EnrichAsync(
            result,
            ExplanationProviderFactory.Create(
                new ExplanationSettings(ExplanationProviders.OpenAi, null),
                name => name == ExplanationOptions.KeyEnvironmentVariable ? secretKey : null,
                new MockHttpHandler(_ => MockHttpHandler.Json("""{ "choices": [ { "message": { "content": "[openai] minimal migration." } } ] }"""))));
        Assert.Equal(0, outcome.Failures);

        Directory.CreateDirectory(_historyDirectory);
        var savedAt = DateTime.UtcNow;
        var json = JsonReporter.Render(outcome.Result, null, new ReportMeta(savedAt.ToString("o"), "compare", ["old.json", "new.json"]));
        var path = HistoryStore.Save(_historyDirectory, json, "compare", savedAt);

        var persisted = File.ReadAllText(path);
        Assert.Contains("[openai]", persisted);
        Assert.DoesNotContain(secretKey, persisted);

        using var document = JsonDocument.Parse(persisted);
        Assert.All(document.RootElement.GetProperty("changes").EnumerateArray(),
            entry => Assert.Equal(JsonValueKind.String, entry.GetProperty("explanation").ValueKind));
    }
}
