using System.Text.Json;
using ContractWatch.Core;
using ContractWatch.Core.Comparison;
using ContractWatch.Core.Reporting;

namespace ContractWatch.Core.Tests;

public class HistoryTests : IDisposable
{
    private readonly string _directory;

    public HistoryTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"cw-history-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private string PathOf(string name) => Path.Combine(_directory, name);

    [Fact]
    public void Save_crea_el_directorio_y_ante_colision_genera_sufijo()
    {
        var timestamp = new DateTime(2026, 8, 22, 17, 3, 11, DateTimeKind.Utc);

        var first = HistoryStore.Save(_directory, "{}", "compare", timestamp);
        var second = HistoryStore.Save(_directory, "{}", "compare", timestamp);
        var third = HistoryStore.Save(_directory, "{}", "check", timestamp);

        Assert.True(Directory.Exists(_directory));
        Assert.Equal(PathOf("20260822-170311-compare.json"), first);
        Assert.Equal(PathOf("20260822-170311-compare-2.json"), second);
        Assert.Equal(PathOf("20260822-170311-check.json"), third);
    }

    [Fact]
    public void Lo_guardado_por_Save_se_lee_con_List_con_meta_y_summary_correctos()
    {
        var changes = new List<ContractChange>
        {
            new("CW004", "RequiredPropertyAdded", ChangeSeverity.Breaking,
                new ChangeLocation("/orders", "POST"), "Required request property added: currency"),
            new("CW010", "EnumWidened", ChangeSeverity.PotentiallyBreaking,
                new ChangeLocation("/payments"), "Response enum widened: + PENDING"),
            new("CW015", "OptionalPropertyAdded", ChangeSeverity.Compatible,
                new ChangeLocation("/orders", "POST"), "Optional property added: metadata"),
        };
        var result = new ComparisonResult(changes);
        var meta = new ReportMeta("2026-08-22T17:03:11.0000000Z", "compare", ["old.json", "new.json"]);
        var json = JsonReporter.Render(result, null, meta);

        var path = HistoryStore.Save(_directory, json, "compare", new DateTime(2026, 8, 22, 17, 3, 11, DateTimeKind.Utc));

        var entry = Assert.Single(HistoryStore.List(_directory, 10));

        Assert.Equal(Path.GetFileName(path), entry.FileName);
        Assert.True(entry.IsLegible);
        Assert.Equal("2026-08-22T17:03:11.0000000Z", entry.Meta!.SavedAt);
        Assert.Equal("compare", entry.Meta.Command);
        Assert.Equal(["old.json", "new.json"], entry.Meta.Inputs);
        Assert.Equal(1, entry.Summary!.Breaking);
        Assert.Equal(1, entry.Summary.PotentiallyBreaking);
        Assert.Equal(1, entry.Summary.Compatible);
    }

    [Fact]
    public void List_ordena_descendente_por_savedAt_y_respeta_limit()
    {
        Save("older", new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc));
        Save("newest", new DateTime(2026, 8, 22, 17, 3, 11, DateTimeKind.Utc));
        Save("middle", new DateTime(2026, 8, 21, 12, 30, 0, DateTimeKind.Utc));

        var all = HistoryStore.List(_directory, 10);
        var limited = HistoryStore.List(_directory, 2);

        Assert.Equal(
        [
            "20260822-170311-newest.json",
            "20260821-123000-middle.json",
            "20260820-100000-older.json",
        ], all.Select(e => e.FileName));
        Assert.Equal(
        [
            "20260822-170311-newest.json",
            "20260821-123000-middle.json",
        ], limited.Select(e => e.FileName));
    }

    [Fact]
    public void Archivo_corrupto_entre_validos_aparece_ilegible_sin_romper_la_lista()
    {
        Save("good", new DateTime(2026, 8, 22, 17, 3, 11, DateTimeKind.Utc));
        File.WriteAllText(PathOf("roto.json"), "{\"summary\": {\"breaking\": ");
        Save("other", new DateTime(2026, 8, 21, 12, 30, 0, DateTimeKind.Utc));

        var entries = HistoryStore.List(_directory, 10);

        Assert.Equal(3, entries.Count);

        var broken = entries.Single(e => e.FileName == "roto.json");
        Assert.False(broken.IsLegible);
        Assert.Null(broken.Meta);
        Assert.Null(broken.Summary);

        Assert.Equal(["20260822-170311-good.json", "20260821-123000-other.json", "roto.json"], entries.Select(e => e.FileName));
        Assert.All(entries.Where(e => e.FileName != "roto.json"), e => Assert.True(e.IsLegible));
    }

    [Fact]
    public void Archivo_valido_sin_meta_es_listable_con_meta_nula()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(PathOf("20260822-170311-check.json"), """
            {
              "tool": "contractwatch",
              "summary": { "breaking": 0, "potentiallyBreaking": 2, "compatible": 4 },
              "changes": []
            }
            """);

        var entry = Assert.Single(HistoryStore.List(_directory, 5));

        Assert.True(entry.IsLegible);
        Assert.Null(entry.Meta);
        Assert.Equal(0, entry.Summary!.Breaking);
        Assert.Equal(2, entry.Summary.PotentiallyBreaking);
        Assert.Equal(4, entry.Summary.Compatible);
    }

    [Fact]
    public void Read_de_un_reporte_inexistente_lanza_HistoryException()
    {
        Directory.CreateDirectory(_directory);

        Assert.Throws<HistoryException>(() => HistoryStore.Read(PathOf("no-existe.json")));
    }

    private void Save(string kind, DateTime timestampUtc)
    {
        var meta = new ReportMeta(timestampUtc.ToString("o"), "compare", ["old.json", "new.json"]);
        var json = JsonReporter.Render(new ComparisonResult([]), null, meta);
        HistoryStore.Save(_directory, json, kind, timestampUtc);
    }
}
