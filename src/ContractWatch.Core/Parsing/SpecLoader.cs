using System.Text.Json;
using System.Text.Json.Nodes;

namespace ContractWatch.Core.Parsing;

public enum SpecKind
{
    OpenApi,
    AsyncApi,
}

public sealed record LoadedSpec(ApiContract Contract, SpecKind Kind);

public sealed class MixedSpecKindsException : Exception
{
    public MixedSpecKindsException(string previousPath, SpecKind previousKind, string currentPath, SpecKind currentKind)
        : base($"No se pueden mezclar tipos de spec en una comparación: '{previousPath}' es {Render(previousKind)} y '{currentPath}' es {Render(currentKind)}. Ambos contratos deben ser del mismo tipo (openapi o asyncapi).")
    {
    }

    private static string Render(SpecKind kind) => kind switch
    {
        SpecKind.AsyncApi => "AsyncAPI",
        _ => "OpenAPI",
    };
}

public static class SpecLoader
{
    public static async Task<LoadedSpec> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);

        JsonObject? root = null;

        try
        {
            root = JsonNode.Parse(content) as JsonObject;
        }
        catch (JsonException)
        {
        }

        if (root is not null && root.ContainsKey("asyncapi"))
            return new LoadedSpec(AsyncApiLoader.Parse(root, filePath), SpecKind.AsyncApi);

        if (root is null && LooksLikeAsyncApiYaml(content))
            throw new UnsupportedSpecException(filePath, "el documento parece AsyncAPI en formato YAML; solo se soporta JSON: exporta el documento a JSON y vuelve a intentarlo");

        return new LoadedSpec(await OpenApiLoader.LoadAsync(filePath, cancellationToken), SpecKind.OpenApi);
    }

    public static void EnsureSameKind(LoadedSpec previous, LoadedSpec current, string previousPath, string currentPath)
    {
        if (previous.Kind != current.Kind)
            throw new MixedSpecKindsException(previousPath, previous.Kind, currentPath, current.Kind);
    }

    private static bool LooksLikeAsyncApiYaml(string content)
    {
        foreach (var line in content.Split('\n'))
        {
            if (line.StartsWith("asyncapi:", StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
