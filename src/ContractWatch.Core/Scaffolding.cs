namespace ContractWatch.Core;

public sealed record ScaffoldFileResult(string FileName, string Status);

public static class ContractWatchInit
{
    public static IReadOnlyList<ScaffoldFileResult> Init(string directory)
    {
        var results = new List<ScaffoldFileResult>();

        Scaffold(directory, PolicyFile.DefaultFileName, """
            {
              // "failOn": "breaking" | "potentially" | "never"   ← umbral por defecto del gate
              // "severityOverrides": { "CW010": "compatible" }     ← re-map por regla (CW001..CW018)
              // "explain": "fake" | "openai"                       ← explicaciones con IA (off por defecto)
              // "explainModel": "gpt-4o-mini"                      ← modelo del proveedor
            }
            """, results);

        Scaffold(directory, SuppressionFile.DefaultFileName, """
            # Supresiones de ContractWatch — una por línea:
            # <ruleId> <path> [<method>] :: <razón obligatoria>
            # Ejemplo: CW001 /legacy/orders :: retirada planificada Q4
            """, results);

        Scaffold(directory, ConsumerRegistryFile.DefaultFileName, """
            {
              "consumers": []
            }
            """, results);

        return results;
    }

    private static void Scaffold(string directory, string fileName, string template, List<ScaffoldFileResult> results)
    {
        var path = Path.Combine(directory, fileName);
        var status = File.Exists(path) ? "exists" : WriteTemplate(path, template);
        results.Add(new ScaffoldFileResult(fileName, status));
    }

    private static string WriteTemplate(string path, string template)
    {
        File.WriteAllText(path, template + Environment.NewLine);
        return "created";
    }
}
