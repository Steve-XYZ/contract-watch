using ContractWatch.Core;
using ContractWatch.Core.Reporting;

namespace ContractWatch.Core.Tests;

public class ConsoleReporterTests
{
    private static readonly ChangeLocation Orders = new("/orders", "POST");

    [Fact]
    public void Ordena_por_severidad_y_ubica_cada_cambio()
    {
        var changes = new[]
        {
            new ContractChange("CW015", "OptionalPropertyAdded", ChangeSeverity.Compatible,
                Orders, "Optional property added: metadata"),
            new ContractChange("CW008", "ResponsePropertyTypeChanged", ChangeSeverity.Breaking,
                new ChangeLocation("/orders/{id}", "GET"), "Response property changed: amount: number → string",
                "number", "string"),
            new ContractChange("CW010", "EnumWidened", ChangeSeverity.PotentiallyBreaking,
                new ChangeLocation("/payments", "GET"), "Response enum widened: + PENDING",
                "[\"PAID\",\"FAILED\"]", "[\"PAID\",\"FAILED\",\"PENDING\"]"),
        };

        var output = ConsoleReporter.Render(changes);
        var lines = output.Split(Environment.NewLine);

        Assert.StartsWith("✗ BREAKING GET /orders/{id}", lines[0]);
        Assert.StartsWith("⚠ POTENTIAL  GET /payments", lines[2]);
        Assert.StartsWith("✓ COMPATIBLE POST /orders", lines[4]);
        Assert.EndsWith("1 breaking · 1 potentially breaking · 1 compatible", lines[7]);
    }

    [Fact]
    public void Sin_cambios_reporta_lista_vacia()
    {
        var output = ConsoleReporter.Render(Array.Empty<ContractChange>());

        Assert.Contains("No contract changes detected.", output);
        Assert.EndsWith("0 breaking · 0 potentially breaking · 0 compatible", output);
    }

    [Fact]
    public void Muestra_la_sugerencia_indentada_bajo_el_detalle()
    {
        var changes = new[]
        {
            new ContractChange("CW003", "RequiredParameterAdded", ChangeSeverity.Breaking,
                Orders, "Required parameter added: x-trace-id",
                Suggestion: "Introduce the parameter as optional with a default first."),
        };

        var output = ConsoleReporter.Render(changes);
        var lines = output.Split(Environment.NewLine);

        Assert.Equal("    ↳ Introduce the parameter as optional with a default first.", lines[2]);
        Assert.DoesNotContain("↳", lines[1]);
    }

    [Fact]
    public void Muestra_la_explicacion_ia_indentada_tras_la_sugerencia()
    {
        var changes = new[]
        {
            new ContractChange("CW003", "RequiredParameterAdded", ChangeSeverity.Breaking,
                Orders, "Required parameter added: x-trace-id",
                Suggestion: "Introduce the parameter as optional with a default first.",
                Explanation: "[fake] explains the change."),
        };

        var output = ConsoleReporter.Render(changes);
        var lines = output.Split(Environment.NewLine);

        Assert.Equal("    ↳ Introduce the parameter as optional with a default first.", lines[2]);
        Assert.Equal("    ↳ IA: [fake] explains the change.", lines[3]);
    }

    [Fact]
    public void Sin_explicacion_no_aparece_linea_ia()
    {
        var changes = new[]
        {
            new ContractChange("CW003", "RequiredParameterAdded", ChangeSeverity.Breaking,
                Orders, "Required parameter added: x-trace-id",
                Suggestion: "Introduce the parameter as optional with a default first."),
        };

        var output = ConsoleReporter.Render(changes);

        Assert.DoesNotContain("↳ IA:", output);
    }

    [Fact]
    public void La_explicacion_multilinea_se_aplasta_en_una_linea()
    {
        var changes = new[]
        {
            new ContractChange("CW003", "RequiredParameterAdded", ChangeSeverity.Breaking,
                Orders, "Required parameter added: x-trace-id",
                Explanation: "linea 1\nlinea 2"),
        };

        var output = ConsoleReporter.Render(changes);
        var lines = output.Split(Environment.NewLine);

        Assert.Equal("    ↳ IA: linea 1 linea 2", lines[2]);
    }
}
