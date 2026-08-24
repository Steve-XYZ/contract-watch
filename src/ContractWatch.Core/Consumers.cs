using System.Text.Json;
using System.Text.Json.Serialization;
using ContractWatch.Core.Comparison;

namespace ContractWatch.Core;

public record ConsumerEntry(string Service, IReadOnlyList<string> Operations, string? Spec = null);

public sealed record ConsumerRegistry(IReadOnlyList<ConsumerEntry> Consumers, string? Service = null)
{
    public static ConsumerRegistry Empty { get; } = new([]);
}

public enum ConfidenceLevel
{
    High,
    Medium,
}

public record AffectedConsumer(string Service, ConfidenceLevel Confidence, int Changes);

public record ChainTrigger(string RuleId, string Target);

public record ImpactChain(IReadOnlyList<string> Services, ConfidenceLevel Confidence, IReadOnlyList<ChainTrigger> Triggers);

public sealed record ImpactAnalysis(IReadOnlyList<AffectedConsumer> Consumers, IReadOnlyList<ImpactChain> Chains);

public sealed record ImpactGraph(
    ConsumerRegistry Registry,
    IReadOnlyDictionary<string, ImpactGraph> Children,
    string? SourcePath = null);

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

    public static ImpactGraph LoadGraphOrDefault(string? explicitPath, string? directory = null)
    {
        var path = explicitPath ?? Path.Combine(directory ?? Directory.GetCurrentDirectory(), DefaultFileName);
        var root = File.Exists(path) ? Load(path) : ConsumerRegistry.Empty;
        return ResolveChildren(root, path, [], new Dictionary<string, ImpactGraph>(StringComparer.Ordinal));
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

        var service = dto.Service?.Trim();

        if (dto.Service is not null && string.IsNullOrEmpty(service))
            throw new ConsumerRegistryException(path, "el nombre del servicio es obligatorio");

        var consumers = new List<ConsumerEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var consumer in dto.Consumers ?? [])
        {
            var entryService = consumer.Service?.Trim();

            if (string.IsNullOrEmpty(entryService))
                throw new ConsumerRegistryException(path, "el nombre del servicio es obligatorio");

            if (!seen.Add(entryService))
                throw new ConsumerRegistryException(path, $"servicio duplicado '{entryService}'");

            if (consumer.Operations is not { Count: > 0 })
                throw new ConsumerRegistryException(path, $"el consumidor '{entryService}' no tiene operaciones");

            var spec = consumer.Spec?.Trim();

            if (consumer.Spec is not null && string.IsNullOrEmpty(spec))
                throw new ConsumerRegistryException(path, $"la ruta del spec está vacía en el consumidor '{entryService}'");

            consumers.Add(new ConsumerEntry(entryService, [.. consumer.Operations.Select(o => ParseOperation(path, entryService, o))], spec));
        }

        return new ConsumerRegistry(consumers, service);
    }

    private static ImpactGraph ResolveChildren(
        ConsumerRegistry registry,
        string filePath,
        List<(string Path, string? Name)> ancestors,
        Dictionary<string, ImpactGraph> cache)
    {
        var framePath = Path.GetFullPath(filePath);
        var stack = new List<(string Path, string? Name)>(ancestors) { (framePath, registry.Service) };
        var children = new Dictionary<string, ImpactGraph>(StringComparer.Ordinal);
        var directory = Path.GetDirectoryName(framePath) ?? Directory.GetCurrentDirectory();

        foreach (var entry in registry.Consumers)
        {
            if (entry.Spec is null)
                continue;

            var specPath = Path.GetFullPath(Path.Combine(directory, entry.Spec));

            if (!File.Exists(specPath))
                throw new ConsumerRegistryException(filePath, $"no existe el spec '{entry.Spec}' declarado para el consumidor '{entry.Service}'");

            var nestedPath = Path.Combine(Path.GetDirectoryName(specPath)!, DefaultFileName);

            if (!File.Exists(nestedPath))
                throw new ConsumerRegistryException(filePath, $"el consumidor '{entry.Service}' declara el spec '{entry.Spec}' pero no hay un '{DefaultFileName}' junto a él");

            var canonical = Path.GetFullPath(nestedPath);

            if (stack.Any(frame => frame.Path == canonical))
                throw new ConsumerRegistryException(canonical, $"ciclo de consumidores detectado: {RenderCycle(stack, canonical)}");

            if (!cache.TryGetValue(canonical, out var childNode))
            {
                childNode = ResolveChildren(Load(canonical), canonical, stack, cache);
                cache[canonical] = childNode;
            }

            children[entry.Service] = childNode;
        }

        return new ImpactGraph(registry, children, filePath);
    }

    private static string RenderCycle(List<(string Path, string? Name)> stack, string repeatedPath)
    {
        var names = stack.Select(frame => frame.Name).Where(name => !string.IsNullOrEmpty(name)).Cast<string>().ToList();
        var repeated = stack.FirstOrDefault(frame => frame.Path == repeatedPath).Name;
        var cycle = new List<string>(names);

        if (!string.IsNullOrEmpty(repeated))
            cycle.Add(repeated);

        return string.Join(" → ", cycle);
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

    private sealed record RegistryDto(string? Service, List<ConsumerDto>? Consumers);

    private sealed record ConsumerDto(string? Service, List<string>? Operations, string? Spec);
}

public static class ImpactAnalyzer
{
    public static IReadOnlyList<AffectedConsumer> Analyze(ComparisonResult result, ConsumerRegistry registry)
    {
        var graph = new ImpactGraph(registry, new Dictionary<string, ImpactGraph>(StringComparer.Ordinal));
        return Analyze(result, graph).Consumers;
    }

    public static ImpactAnalysis Analyze(ComparisonResult result, ImpactGraph root)
    {
        var relevant = result.Changes.Where(c => c.Severity != ChangeSeverity.Compatible).ToArray();

        if (relevant.Length == 0)
            return new ImpactAnalysis([], []);

        var affected = new Dictionary<string, (ConfidenceLevel Confidence, HashSet<ContractChange> Changes)>(StringComparer.Ordinal);
        var chains = new List<ImpactChain>();
        List<string> head = root.Registry.Service is { } name ? [name] : [];

        foreach (var entry in root.Registry.Consumers)
        {
            var triggers = Matches(entry, relevant);

            if (triggers.Count == 0)
                continue;

            var confidence = Weaker(ConfidenceLevel.High, HopConfidence(entry));
            Record(affected, entry.Service, confidence, triggers);

            if (root.Children.TryGetValue(entry.Service, out var child))
                Propagate(child, [.. head, entry.Service], confidence, triggers, affected, chains);
        }

        return new ImpactAnalysis(Order(affected), Order(chains));

        static void Record(
            Dictionary<string, (ConfidenceLevel Confidence, HashSet<ContractChange> Changes)> affected,
            string service,
            ConfidenceLevel confidence,
            IReadOnlyList<ContractChange> changes)
        {
            if (!affected.TryGetValue(service, out var entry))
            {
                affected[service] = (confidence, [.. changes]);
                return;
            }

            if (confidence == ConfidenceLevel.High && entry.Confidence == ConfidenceLevel.Medium)
                affected[service] = entry with { Confidence = ConfidenceLevel.High };

            entry.Changes.UnionWith(changes);
        }

        void Propagate(
            ImpactGraph node,
            IReadOnlyList<string> chainServices,
            ConfidenceLevel inherited,
            IReadOnlyList<ContractChange> triggers,
            Dictionary<string, (ConfidenceLevel Confidence, HashSet<ContractChange> Changes)> affected,
            List<ImpactChain> chains)
        {
            foreach (var entry in node.Registry.Consumers)
            {
                if (chainServices.Contains(entry.Service))
                    throw new ConsumerRegistryException(node.SourcePath ?? ConsumerRegistryFile.DefaultFileName, $"ciclo de consumidores detectado: {string.Join(" → ", [.. chainServices, entry.Service])}");

                var confidence = Weaker(inherited, HopConfidence(entry));

                Record(affected, entry.Service, confidence, triggers);
                chains.Add(new ImpactChain([.. chainServices, entry.Service], confidence, [.. triggers.Select(ToTrigger)]));

                if (node.Children.TryGetValue(entry.Service, out var child))
                    Propagate(child, [.. chainServices, entry.Service], confidence, triggers, affected, chains);
            }
        }
    }

    private static IReadOnlyList<AffectedConsumer> Order(Dictionary<string, (ConfidenceLevel Confidence, HashSet<ContractChange> Changes)> affected) =>
    [
        .. affected.Select(kv => new AffectedConsumer(kv.Key, kv.Value.Confidence, kv.Value.Changes.Count))
            .OrderBy(a => a.Confidence)
            .ThenBy(a => a.Service, StringComparer.Ordinal)
    ];

    private static IReadOnlyList<ImpactChain> Order(List<ImpactChain> chains) =>
    [
        .. chains.OrderBy(c => c.Confidence)
            .ThenBy(c => string.Join(" → ", c.Services), StringComparer.Ordinal)
    ];

    private static ConfidenceLevel HopConfidence(ConsumerEntry entry) =>
        entry.Operations.Any(HasExplicitMethod) ? ConfidenceLevel.High : ConfidenceLevel.Medium;

    private static ConfidenceLevel Weaker(ConfidenceLevel left, ConfidenceLevel right) =>
        left == ConfidenceLevel.Medium || right == ConfidenceLevel.Medium ? ConfidenceLevel.Medium : ConfidenceLevel.High;

    private static IReadOnlyList<ContractChange> Matches(ConsumerEntry entry, ContractChange[] changes) =>
    [
        .. changes.Where(change => entry.Operations.Any(operation => Matches(operation, change.Location))).Distinct()
    ];

    private static ChainTrigger ToTrigger(ContractChange change) => new(change.RuleId, RenderTarget(change.Location));

    private static string RenderTarget(ChangeLocation location) =>
        location.Method is null ? location.Path : $"{location.Method} {location.Path}";

    private static bool Matches(string operation, ChangeLocation location)
    {
        var separator = operation.IndexOf(' ');
        var hasMethod = separator >= 0;
        var method = hasMethod ? operation[..separator] : null;
        var path = hasMethod ? operation[(separator + 1)..] : operation;

        if (!string.Equals(path, location.Path, StringComparison.Ordinal))
            return false;

        if (location.Method is null)
            return true;

        return method is null or "*" || string.Equals(method, location.Method, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasExplicitMethod(string operation) =>
        !operation.StartsWith('/') && !operation.StartsWith("* ");
}
