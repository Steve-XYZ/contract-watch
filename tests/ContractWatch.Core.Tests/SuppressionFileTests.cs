using ContractWatch.Core;
using ContractWatch.Core.Comparison;

namespace ContractWatch.Core.Tests;

public class SuppressionFileTests : IDisposable
{
    private readonly string _directory;

    public SuppressionFileTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"cw-suppress-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private string Write(string content, string name = ".contractwatchignore")
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Parse_linea_valida_con_metodo_y_razon()
    {
        var suppressions = SuppressionFile.Load(Write("CW003 /orders POST :: acordado con consumidores en #42\n"));

        var suppression = Assert.Single(suppressions);
        Assert.Equal("CW003", suppression.RuleId);
        Assert.Equal("/orders", suppression.Path);
        Assert.Equal("POST", suppression.Method);
        Assert.Equal("acordado con consumidores en #42", suppression.Reason);
    }

    [Fact]
    public void Parse_linea_sin_metodo_e_ignora_comentarios_y_vacias()
    {
        var content = """
            # supresiones del equipo

            CW001 /legacy/orders :: retirada planificada Q4


            CW010 /players/{id} GET :: admin-web maneja PENDING desde v2
            """;
        var suppressions = SuppressionFile.Load(Write(content));

        Assert.Equal(2, suppressions.Count);
        Assert.Null(suppressions[0].Method);
        Assert.Equal("GET", suppressions[1].Method);
    }

    [Theory]
    [InlineData("CW003 /orders\n")]
    [InlineData("CW003 :: sin tokens suficientes\n")]
    [InlineData("CW003 /orders POST extra :: razon\n")]
    public void Linea_malformada_lanza_error_con_numero_de_linea(string line)
    {
        var path = Write("# ok\n" + line);

        var exception = Assert.Throws<SuppressionFileException>(() => SuppressionFile.Load(path));

        Assert.Contains(":2:", exception.Message);
    }

    [Fact]
    public void Razon_vacia_es_rechazada_porque_toda_supresion_debe_estar_justificada()
    {
        var path = Write("CW001 /x ::   \n");

        Assert.Throws<SuppressionFileException>(() => SuppressionFile.Load(path));
    }

    [Fact]
    public void LoadOrDefault_devuelve_vacio_cuando_no_hay_archivo()
    {
        Assert.Empty(SuppressionFile.LoadOrDefault(null, _directory));
    }

    [Fact]
    public void LoadOrDefault_detecta_el_archivo_por_defecto_en_el_directorio_dado()
    {
        Write("CW001 /x :: razón\n");

        var suppressions = SuppressionFile.LoadOrDefault(null, _directory);

        Assert.Single(suppressions);
    }

    [Fact]
    public void Filtra_cambios_que_coinciden_con_regla_y_path()
    {
        var original = new ComparisonResult(
        [
            new("CW001", "EndpointRemoved", ChangeSeverity.Breaking, new ChangeLocation("/legacy/orders"), "Endpoint removed"),
            new("CW002", "OperationRemoved", ChangeSeverity.Breaking, new ChangeLocation("/orders/{id}", "DELETE"), "Method DELETE removed"),
        ]);
        var suppressions = new[]
        {
            new Suppression("CW001", "/legacy/orders", null, "razón"),
        };

        var filtered = SuppressionFile.Apply(original, suppressions);

        Assert.Single(filtered.Changes);
        Assert.Equal("CW002", filtered.Changes[0].RuleId);
        Assert.Equal(1, SuppressionFile.CountSuppressed(original, filtered));
    }

    [Fact]
    public void Metodo_distinto_no_suprime()
    {
        var change = new ContractChange("CW003", "RequiredParameterAdded", ChangeSeverity.Breaking,
            new ChangeLocation("/orders", "POST"), "Required parameter added: page");
        var result = new ComparisonResult([change]);
        var suppressions = new[] { new Suppression("CW003", "/orders", "PUT", "otro método") };

        var filtered = SuppressionFile.Apply(result, suppressions);

        Assert.Single(filtered.Changes);
    }
}
