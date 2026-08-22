using System.Text.Json;
using System.Text.Json.Serialization;
using ContractWatch.Core.Comparison;

namespace ContractWatch.Core;

public record ConsumerEntry(string Service, IReadOnlyList<string> Operations);

public sealed record ConsumerRegistry(IReadOnlyList<ConsumerEntry> Consumers)
{
    public static ConsumerRegistry Empty { get; } = new([]);
}

public enum ConfidenceLevel
{
    High,
    Medium,
}

public record AffectedConsumer(string Service, ConfidenceLevel Confidence, int Changes);

public sealed class ConsumerRegistryException : Exception
{
    public ConsumerRegistryException(string path, string problem)
        : base($"Registro de consumidores inválido en {path}: {problem}")
    {
    }
}

public static class ConsumerRegistryFile
{
    public const string DefaultFileName = "consumers.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static ConsumerRegistry LoadOrDefault(string? explicitPath, string? directory = null)
    {
        var path = explicitPath ?? Path.Combine(directory ?? Directory.GetCurrentDirectory(), DefaultFileName);
        return File.Exists(path) ? Load(path) : ConsumerRegistry.Empty;
    }

    public static ConsumerRegistry Load(string path)
    {
        RegistryDto dto;

        try
        {
            dto = JsonSerializer.Deserialize<RegistryDto>(File.ReadAllText(path), Options)
                  ?? throw new JsonException("el documento está vacío");
        }
        catch (JsonException ex)
        {
            throw new ConsumerRegistryException(path, $"JSON malformado ({ex.Message})");
        }

        var consumers = new List<ConsumerEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var consumer in dto.Consumers ?? [])
        {
            var service = consumer.Service?.Trim();

            if (string.IsNullOrEmpty(service))
                throw new ConsumerRegistryException(path, "el nombre del servicio es obligatorio");

            if (!seen.Add(service))
                throw new ConsumerRegistryException(path, $"servicio duplicado '{service}'");

            if (consumer.Operations is not { Count: > 0 })
                throw new ConsumerRegistryException(path, $"el consumidor '{service}' no tiene operaciones");

            consumers.Add(new ConsumerEntry(service, [.. consumer.Operations.Select(o => ParseOperation(path, service, o))]));
        }

        return new ConsumerRegistry(consumers);
    }

    private static string ParseOperation(string path, string service, string operation)
    {
        if (string.IsNullOrWhiteSpace(operation))
            throw new ConsumerRegistryException(path, $"operación vacía en el consumidor '{service}' (se espera 'METHOD /path' o '/path')");

        var tokens = operation.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length == 1)
        {
            if (!tokens[0].StartsWith('/'))
                throw new ConsumerRegistryException(path, $"operación inválida '{operation}' en el consumidor '{service}' (se espera 'METHOD /path' o '/path')");

            return tokens[0];
        }

        if (tokens.Length != 2 || !tokens[1].StartsWith('/'))
            throw new ConsumerRegistryException(path, $"operación inválida '{operation}' en el consumidor '{service}' (se espera 'METHOD /path' o '/path')");

        return $"{tokens[0].ToUpperInvariant()} {tokens[1]}";
    }

    private sealed record RegistryDto(List<ConsumerDto>? Consumers);

    private sealed record ConsumerDto(string? Service, List<string>? Operations);
}

public static class ImpactAnalyzer
{
    public static IReadOnlyList<AffectedConsumer> Analyze(ComparisonResult result, ConsumerRegistry registry)
    {
        if (registry.Consumers.Count == 0)
            return [];

        var affected = new Dictionary<string, (ConfidenceLevel Confidence, HashSet<ContractChange> Changes)>(StringComparer.Ordinal);

        foreach (var consumer in registry.Consumers)
        {
            foreach (var operation in consumer.Operations)
            {
                foreach (var change in result.Changes)
                {
                    if (change.Severity == ChangeSeverity.Compatible || !Matches(operation, change.Location))
                        continue;

                    var confidence = HasExplicitMethod(operation) ? ConfidenceLevel.High : ConfidenceLevel.Medium;

                    if (!affected.TryGetValue(consumer.Service, out var entry))
                    {
                        entry = (confidence, []);
                        affected[consumer.Service] = entry;
                    }
                    else if (confidence == ConfidenceLevel.High && entry.Confidence == ConfidenceLevel.Medium)
                    {
                        affected[consumer.Service] = entry with { Confidence = ConfidenceLevel.High };
                    }

                    entry.Changes.Add(change);
                }
            }
        }

        return [.. affected
            .Select(kv => new AffectedConsumer(kv.Key, kv.Value.Confidence, kv.Value.Changes.Count))
            .OrderByDescending(a => a.Confidence)
            .ThenBy(a => a.Service, StringComparer.Ordinal)];
    }

    private static bool Matches(string operation, ChangeLocation location)
    {
        var separator = operation.IndexOf(' ');
        var hasMethod = separator >= 0;
        var method = hasMethod ? operation[..separator] : null;
        var path = hasMethod ? operation[(separator + 1)..] : operation;

        if (!string.Equals(path, location.Path, StringComparison.Ordinal))
            return false;

        return method is null or "*" || string.Equals(method, location.Method, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasExplicitMethod(string operation) =>
        !operation.StartsWith('/') && !operation.StartsWith("* ");
}
