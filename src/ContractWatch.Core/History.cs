using System.Globalization;
using System.Text.Json;

namespace ContractWatch.Core;

public record SavedReportMeta(string SavedAt, string Command, IReadOnlyList<string> Inputs);

public record SavedReportSummary(int Breaking, int PotentiallyBreaking, int Compatible);

public record HistoryEntry(string FileName, SavedReportMeta? Meta, SavedReportSummary? Summary)
{
    public bool IsLegible => Meta is not null || Summary is not null;
}

public sealed class HistoryException : Exception
{
    public HistoryException(string message)
        : base(message)
    {
    }
}

public static class HistoryStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Save(string directory, string jsonContent, string kind, DateTime timestampUtc)
    {
        Directory.CreateDirectory(directory);

        var stem = $"{timestampUtc.ToUniversalTime():yyyyMMdd-HHmmss}-{kind}";
        var fileName = $"{stem}.json";
        var counter = 2;

        while (File.Exists(Path.Combine(directory, fileName)))
        {
            fileName = $"{stem}-{counter}.json";
            counter++;
        }

        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, jsonContent);
        return path;
    }

    public static IReadOnlyList<HistoryEntry> List(string directory, int limit)
    {
        if (!Directory.Exists(directory))
            throw new HistoryException($"no existe el directorio de historial {directory}");

        var entries = new List<HistoryEntry>();

        foreach (var file in Directory.GetFiles(directory, "*.json"))
        {
            HistoryEntry entry;

            try
            {
                entry = Parse(Path.GetFileName(file), File.ReadAllText(file));
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                entry = new HistoryEntry(Path.GetFileName(file), null, null);
            }

            entries.Add(entry);
        }

        return entries
            .OrderByDescending(e => ParseTimestamp(e.Meta?.SavedAt))
            .ThenByDescending(e => e.FileName, StringComparer.Ordinal)
            .Take(limit)
            .ToList();
    }

    public static string Read(string path)
    {
        if (!File.Exists(path))
            throw new HistoryException($"no existe el reporte {path}");

        return File.ReadAllText(path);
    }

    private static HistoryEntry Parse(string fileName, string content)
    {
        var dto = JsonSerializer.Deserialize<ReportDto>(content, Options)
                  ?? new ReportDto(null, null);

        var meta = dto.Meta is { SavedAt: not null, Command: not null }
            ? new SavedReportMeta(dto.Meta.SavedAt, dto.Meta.Command, dto.Meta.Inputs ?? [])
            : null;

        var summary = dto.Summary is { } summaryDto
            ? new SavedReportSummary(summaryDto.Breaking, summaryDto.PotentiallyBreaking, summaryDto.Compatible)
            : null;

        return new HistoryEntry(fileName, meta, summary);
    }

    private static DateTimeOffset ParseTimestamp(string? savedAt) =>
        DateTimeOffset.TryParse(savedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp)
            ? timestamp
            : DateTimeOffset.MinValue;

    private sealed record ReportDto(MetaDto? Meta, SummaryDto? Summary);

    private sealed record MetaDto(string? SavedAt, string? Command, List<string>? Inputs);

    private sealed record SummaryDto(int Breaking, int PotentiallyBreaking, int Compatible);
}
