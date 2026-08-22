using ContractWatch.Core.Rules;

namespace ContractWatch.Core.Tests;

public class RemediationCatalogTests
{
    [Fact]
    public void Cada_regla_del_catalogo_tiene_una_sugerencia_no_vacia()
    {
        var ruleCount = RuleCatalog.Default.Count;

        foreach (var index in Enumerable.Range(1, ruleCount))
        {
            var ruleId = $"CW{index:000}";

            Assert.False(string.IsNullOrWhiteSpace(RemediationCatalog.For(ruleId)), $"Falta remediation para {ruleId}");
        }

        Assert.Null(RemediationCatalog.For($"CW{ruleCount + 1:000}"));
    }

    [Fact]
    public void Un_id_desconocido_devuelve_null()
    {
        Assert.Null(RemediationCatalog.For("CW999"));
        Assert.Null(RemediationCatalog.For(string.Empty));
    }
}
