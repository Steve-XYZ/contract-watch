using ContractWatch.Core.Parsing;

namespace ContractWatch.Core.Tests;

public class GitSpecSourceTests
{
    [Fact]
    public async Task Resuelve_el_spec_de_un_ref_git_y_lo_carga_como_contrato()
    {
        var contract = await GitSpecSource.LoadAsync("HEAD", "examples/v1.json");

        Assert.Equal(5, contract.Operations.Count);
        Assert.Contains(contract.Operations, o => o.Path == "/orders" && o.Method == "POST");
    }

    [Fact]
    public async Task Ref_inexistente_lanza_error_claro()
    {
        var exception = await Assert.ThrowsAsync<GitSpecException>(
            () => GitSpecSource.LoadAsync("ref-inexistente-xyz", "examples/v1.json"));

        Assert.Contains("examples/v1.json", exception.Message);
        Assert.Contains("ref-inexistente-xyz", exception.Message);
    }

    [Fact]
    public async Task Path_inexistente_en_el_ref_lanza_error()
    {
        await Assert.ThrowsAsync<GitSpecException>(
            () => GitSpecSource.LoadAsync("HEAD", "examples/no-existe.json"));
    }
}
