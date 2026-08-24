using ContractWatch.Core;
using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Tests;

public class PolicyFileTests : IDisposable
{
    private readonly string _directory;

    public PolicyFileTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"cw-policy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private string Write(string content, string name = ".contractwatch.json")
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Parse_failOn_solo()
    {
        var policy = PolicyFile.Load(Write("""{ "failOn": "potentially" }"""));

        Assert.Equal("potentially", policy.FailOn);
        Assert.Empty(policy.SeverityOverrides);
    }

    [Fact]
    public void Parse_overrides_solo()
    {
        var policy = PolicyFile.Load(Write("""{ "severityOverrides": { "CW010": "compatible" } }"""));

        Assert.Null(policy.FailOn);
        Assert.Equal(ChangeSeverity.Compatible, policy.SeverityOverrides["CW010"]);
    }

    [Fact]
    public void Parse_failOn_y_overrides()
    {
        var policy = PolicyFile.Load(Write("""
            {
              "failOn": "never",
              "severityOverrides": {
                "CW010": "compatible",
                "CW011": "breaking"
              }
            }
            """));

        Assert.Equal("never", policy.FailOn);
        Assert.Equal(2, policy.SeverityOverrides.Count);
        Assert.Equal(ChangeSeverity.Compatible, policy.SeverityOverrides["CW010"]);
        Assert.Equal(ChangeSeverity.Breaking, policy.SeverityOverrides["CW011"]);
    }

    [Fact]
    public void failOn_invalido_lanza_error_con_la_ruta()
    {
        var path = Write("""{ "failOn": "a veces" }""");

        var exception = Assert.Throws<PolicyFileException>(() => PolicyFile.Load(path));

        Assert.Contains(path, exception.Message);
        Assert.Contains("failOn", exception.Message);
    }

    [Fact]
    public void Regla_desconocida_en_overrides_lanza_error_para_proteger_typos()
    {
        var path = Write("""{ "severityOverrides": { "CW003": "compatible", "CW099": "breaking" } }""");

        var exception = Assert.Throws<PolicyFileException>(() => PolicyFile.Load(path));

        Assert.Contains("CW099", exception.Message);
    }

    [Fact]
    public void Las_reglas_de_AsyncAPI_son_validas_en_overrides()
    {
        var policy = PolicyFile.Load(Write("""{ "severityOverrides": { "CW019": "compatible", "CW027": "breaking" } }"""));

        Assert.Equal(ChangeSeverity.Compatible, policy.SeverityOverrides["CW019"]);
        Assert.Equal(ChangeSeverity.Breaking, policy.SeverityOverrides["CW027"]);
    }

    [Fact]
    public void Severidad_invalida_en_overrides_lanza_error()
    {
        var path = Write("""{ "severityOverrides": { "CW010": "optional" } }""");

        Assert.Throws<PolicyFileException>(() => PolicyFile.Load(path));
    }

    [Fact]
    public void Json_malformado_lanza_error_con_la_ruta()
    {
        var path = Write("{ failOn: ");

        var exception = Assert.Throws<PolicyFileException>(() => PolicyFile.Load(path));

        Assert.Contains(path, exception.Message);
        Assert.Contains("JSON malformado", exception.Message);
    }

    [Fact]
    public void Apply_remapea_severidades_de_los_cambios_de_la_regla()
    {
        var result = new ComparisonResult(
        [
            new("CW003", "RequiredParameterAdded", ChangeSeverity.Breaking, new ChangeLocation("/orders", "POST"), "Required parameter added: page"),
            new("CW010", "EnumWidened", ChangeSeverity.PotentiallyBreaking, new ChangeLocation("/payments", "GET"), "Response enum widened: + PENDING"),
            new("CW015", "OptionalPropertyAdded", ChangeSeverity.Compatible, new ChangeLocation("/orders", "POST"), "Optional property added: metadata"),
        ]);
        var policy = new ContractPolicy(null, new Dictionary<string, ChangeSeverity>
        {
            ["CW010"] = ChangeSeverity.Compatible,
            ["CW015"] = ChangeSeverity.Breaking,
        });

        var remapped = PolicyFile.Apply(result, policy);

        Assert.Equal(ChangeSeverity.Breaking, remapped.Changes.Single(c => c.RuleId == "CW003").Severity);
        Assert.Equal(ChangeSeverity.Compatible, remapped.Changes.Single(c => c.RuleId == "CW010").Severity);
        Assert.Equal(ChangeSeverity.Breaking, remapped.Changes.Single(c => c.RuleId == "CW015").Severity);
    }

    [Fact]
    public void Apply_sin_overrides_devuelve_el_resultado_original_y_mantiene_el_orden_por_severidad()
    {
        var result = new ComparisonResult(
        [
            new("CW003", "RequiredParameterAdded", ChangeSeverity.Breaking, new ChangeLocation("/orders", "POST"), "Required parameter added: page"),
            new("CW010", "EnumWidened", ChangeSeverity.PotentiallyBreaking, new ChangeLocation("/payments", "GET"), "Response enum widened: + PENDING"),
        ]);

        var applied = PolicyFile.Apply(result, PolicyFile.LoadOrDefault(null, _directory));

        Assert.Equal(result.Changes[0].RuleId, applied.Changes[0].RuleId);
        Assert.Equal(result.Changes[1].RuleId, applied.Changes[1].RuleId);
    }

    [Fact]
    public void LoadOrDefault_sin_archivo_devuelve_politica_vacia()
    {
        var policy = PolicyFile.LoadOrDefault(null, _directory);

        Assert.Null(policy.FailOn);
        Assert.Empty(policy.SeverityOverrides);
    }

    [Fact]
    public void LoadOrDefault_detecta_el_archivo_por_defecto_en_el_directorio_dado()
    {
        Write("""{ "failOn": "potentially" }""");

        var policy = PolicyFile.LoadOrDefault(null, _directory);

        Assert.Equal("potentially", policy.FailOn);
    }

    [Theory]
    [InlineData("breaking", null, ChangeSeverity.Breaking)]
    [InlineData(null, "potentially", ChangeSeverity.PotentiallyBreaking)]
    [InlineData(null, null, ChangeSeverity.Breaking)]
    [InlineData(null, "never", null)]
    public void ResolveThreshold_aplica_la_precedencia_flag_policy_default(string? flag, string? policyFailOn, ChangeSeverity? expected)
    {
        Assert.Equal(expected, PolicyFile.ResolveThreshold(flag, policyFailOn));
    }

    [Fact]
    public void ResolveThreshold_el_flag_explicito_pisa_la_policy()
    {
        Assert.Equal(ChangeSeverity.Breaking, PolicyFile.ResolveThreshold("breaking", "never"));
        Assert.Null(PolicyFile.ResolveThreshold("never", "breaking"));
    }
}
