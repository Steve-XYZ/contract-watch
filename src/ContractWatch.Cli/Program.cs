using System.CommandLine;
using System.Globalization;
using ContractWatch.Core;
using ContractWatch.Core.Comparison;
using ContractWatch.Core.Explanations;
using ContractWatch.Core.Parsing;
using ContractWatch.Core.Reporting;

var formatOption = new Option<string>("--format") { DefaultValueFactory = _ => "console" };
var failOnOption = new Option<string?>("--fail-on") { DefaultValueFactory = _ => null };
var suppressFileOption = new Option<string?>("--suppress-file");
var consumersOption = new Option<string?>("--consumers");
var saveOption = new Option<string?>("--save");
var explainOption = new Option<string?>("--explain");
var explainModelOption = new Option<string?>("--explain-model");

var oldArgument = new Argument<FileInfo>("old");
var newArgument = new Argument<FileInfo>("new");
var baselineOption = new Option<string>("--baseline") { Required = true };
var specArgument = new Argument<string>("spec");

var compareCommand = new Command("compare", "Compara dos contratos OpenAPI y clasifica los cambios por compatibilidad");
compareCommand.Add(oldArgument);
compareCommand.Add(newArgument);
compareCommand.Add(formatOption);
compareCommand.Add(failOnOption);
compareCommand.Add(suppressFileOption);
compareCommand.Add(consumersOption);
compareCommand.Add(saveOption);
compareCommand.Add(explainOption);
compareCommand.Add(explainModelOption);

var checkCommand = new Command("check", "Compara el contrato del árbol de trabajo contra el spec en un ref de git (gate de CI)");
checkCommand.Add(baselineOption);
checkCommand.Add(specArgument);
checkCommand.Add(formatOption);
checkCommand.Add(failOnOption);
checkCommand.Add(suppressFileOption);
checkCommand.Add(consumersOption);
checkCommand.Add(saveOption);
checkCommand.Add(explainOption);
checkCommand.Add(explainModelOption);

var initCommand = new Command("init", "Crea los archivos de configuración (.contractwatch.json, .contractwatchignore, consumers.json) sin sobreescribir los existentes");

var historyDirOption = new Option<string>("--dir") { DefaultValueFactory = _ => "reports" };
var historyLimitOption = new Option<int>("--limit") { DefaultValueFactory = _ => 20 };
var historyShowOption = new Option<string?>("--show");

var historyCommand = new Command("history", "Lista y consulta los reportes guardados localmente con --save");
historyCommand.Add(historyDirOption);
historyCommand.Add(historyLimitOption);
historyCommand.Add(historyShowOption);

string? ValidateFormat(string format)
{
    if (format is "console" or "json" or "markdown" or "sarif")
        return null;

    return $"--format desconocido '{format}' (console|json|markdown|sarif)";
}

string? ValidateFailOn(string? failOn)
{
    if (failOn is null or "breaking" or "potentially" or "never")
        return null;

    return $"--fail-on desconocido '{failOn}' (breaking|potentially|never)";
}

string? ValidateExplain(string? explain)
{
    if (explain is null || ExplanationProviders.Known.Contains(explain))
        return null;

    return $"--explain desconocido '{explain}' (fake|openai)";
}

async Task<int> ReportAndExit(ApiContract previous, ApiContract current, string? failOn, string artifactUri, string format, string? suppressFile, string? consumersFile, string commandKind, IReadOnlyList<string> inputs, string? saveDirectory, string? explainFlag, string? explainModelFlag, CancellationToken cancellationToken)
{
    var original = new ContractComparer().Compare(previous, current);

    ContractPolicy policy;
    try
    {
        policy = PolicyFile.LoadOrDefault(null);
    }
    catch (PolicyFileException ex)
    {
        Console.Error.WriteLine($"error: {ex.Message}");
        return 2;
    }

    IReadOnlyList<Suppression> suppressions;
    try
    {
        suppressions = SuppressionFile.LoadOrDefault(suppressFile);
    }
    catch (SuppressionFileException ex)
    {
        Console.Error.WriteLine($"error: {ex.Message}");
        return 2;
    }

    var result = SuppressionFile.Apply(PolicyFile.Apply(original, policy), suppressions);
    var suppressed = SuppressionFile.CountSuppressed(original, result);

    ConsumerRegistry registry;
    try
    {
        registry = ConsumerRegistryFile.LoadOrDefault(consumersFile);
    }
    catch (ConsumerRegistryException ex)
    {
        Console.Error.WriteLine($"error: {ex.Message}");
        return 2;
    }

    if (ExplanationOptions.Resolve(explainFlag, policy.Explain, explainModelFlag, policy.ExplainModel) is { } settings)
    {
        IExplanationProvider provider;

        try
        {
            provider = ExplanationProviderFactory.Create(settings);
        }
        catch (ExplanationConfigurationException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }

        var outcome = await ExplanationEnricher.EnrichAsync(result, provider, cancellationToken);

        if (outcome.Failures > 0)
            Console.Error.WriteLine(
                $"aviso: la explicación con IA ({settings.Provider}) falló para {outcome.Failures} hallazgo(s): {outcome.FirstFailureReason}; se mantiene la sugerencia determinista");

        result = outcome.Result;
    }

    var impact = ImpactAnalyzer.Analyze(result, registry);

    Console.Out.WriteLine(format switch
    {
        "json" => JsonReporter.Render(result, impact),
        "markdown" => MarkdownReporter.Render(result, impact),
        "sarif" => SarifReporter.Render(result, artifactUri),
        _ => ConsoleReporter.Render(result.Changes, impact),
    });

    if (suppressed > 0 && format is not ("json" or "sarif"))
        Console.Out.WriteLine($"{suppressed} cambio(s) suprimido(s) según {suppressFile ?? SuppressionFile.DefaultFileName}");

    if (saveDirectory is not null)
    {
        var savedAt = DateTime.UtcNow;
        var json = JsonReporter.Render(result, impact, new ReportMeta(savedAt.ToString("o"), commandKind, inputs));

        try
        {
            var path = HistoryStore.Save(saveDirectory, json, commandKind, savedAt);
            var line = $"Reporte guardado en {path}";

            if (format is "json" or "sarif")
                Console.Error.WriteLine(line);
            else
                Console.Out.WriteLine(line);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"aviso: no se pudo guardar el reporte ({ex.Message})");
        }
    }

    return result.FailsAt(PolicyFile.ResolveThreshold(failOn, policy.FailOn)) ? 1 : 0;
}

compareCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var format = parseResult.GetValue(formatOption)!;
    var failOn = parseResult.GetValue(failOnOption)!;

    if (ValidateFormat(format) is { } formatError)
    {
        Console.Error.WriteLine($"error: {formatError}");
        return 2;
    }

    if (ValidateFailOn(failOn) is { } failOnError)
    {
        Console.Error.WriteLine($"error: {failOnError}");
        return 2;
    }

    var explain = parseResult.GetValue(explainOption);

    if (ValidateExplain(explain) is { } explainError)
    {
        Console.Error.WriteLine($"error: {explainError}");
        return 2;
    }

    var oldFile = parseResult.GetValue(oldArgument)!;
    var newFile = parseResult.GetValue(newArgument)!;

    if (!oldFile.Exists)
    {
        Console.Error.WriteLine($"error: no existe el contrato base {oldFile.FullName}");
        return 2;
    }

    if (!newFile.Exists)
    {
        Console.Error.WriteLine($"error: no existe el contrato propuesto {newFile.FullName}");
        return 2;
    }

    try
    {
        var previous = await OpenApiLoader.LoadAsync(oldFile.FullName, cancellationToken);
        var current = await OpenApiLoader.LoadAsync(newFile.FullName, cancellationToken);
        return await ReportAndExit(previous, current, failOn, SarifReporter.NormalizeArtifactUri(newFile.FullName), format, parseResult.GetValue(suppressFileOption), parseResult.GetValue(consumersOption), "compare", [oldFile.FullName, newFile.FullName], parseResult.GetValue(saveOption), explain, parseResult.GetValue(explainModelOption), cancellationToken);
    }
    catch (Exception ex) when (ex is ContractLoadException or IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"error: {ex.Message}");
        return 2;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"error inesperado: {ex.Message}");
        return 2;
    }
});

checkCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var format = parseResult.GetValue(formatOption)!;
    var failOn = parseResult.GetValue(failOnOption)!;

    if (ValidateFormat(format) is { } formatError)
    {
        Console.Error.WriteLine($"error: {formatError}");
        return 2;
    }

    if (ValidateFailOn(failOn) is { } failOnError)
    {
        Console.Error.WriteLine($"error: {failOnError}");
        return 2;
    }

    var explain = parseResult.GetValue(explainOption);

    if (ValidateExplain(explain) is { } explainError)
    {
        Console.Error.WriteLine($"error: {explainError}");
        return 2;
    }

    var gitRef = parseResult.GetValue(baselineOption)!;
    var specPath = parseResult.GetValue(specArgument)!.Replace('\\', '/');

    try
    {
        var baseline = await GitSpecSource.LoadAsync(gitRef, specPath, cancellationToken);
        var current = await OpenApiLoader.LoadAsync(specPath, cancellationToken);
        return await ReportAndExit(baseline, current, failOn, SarifReporter.NormalizeArtifactUri(Path.GetFullPath(specPath)), format, parseResult.GetValue(suppressFileOption), parseResult.GetValue(consumersOption), "check", [gitRef, specPath], parseResult.GetValue(saveOption), explain, parseResult.GetValue(explainModelOption), cancellationToken);
    }
    catch (Exception ex) when (ex is GitSpecException or ContractLoadException or IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"error: {ex.Message}");
        return 2;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"error inesperado: {ex.Message}");
        return 2;
    }
});

initCommand.SetAction(_ =>
{
    try
    {
        var results = ContractWatchInit.Init(Directory.GetCurrentDirectory());
        var created = 0;
        var exists = 0;

        foreach (var result in results)
        {
            if (result.Status == "created")
            {
                created++;
                Console.Out.WriteLine($"✓ creado  {result.FileName}");
            }
            else
            {
                exists++;
                Console.Out.WriteLine($"- ya existe  {result.FileName}");
            }
        }

        Console.Out.WriteLine($"Listo. {created} creado(s), {exists} existente(s).");
        return 0;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"error: {ex.Message}");
        return 2;
    }
});

historyCommand.SetAction(parseResult =>
{
    var directory = parseResult.GetValue(historyDirOption)!;
    var limit = parseResult.GetValue(historyLimitOption)!;
    var show = parseResult.GetValue(historyShowOption);

    try
    {
        if (show is not null)
        {
            var showPath = show;

            if (!Path.IsPathRooted(showPath) && !File.Exists(showPath))
                showPath = Path.Combine(directory, showPath);

            var content = HistoryStore.Read(showPath);
            Console.Out.Write(content);

            if (!content.EndsWith('\n'))
                Console.Out.WriteLine();

            return 0;
        }

        foreach (var entry in HistoryStore.List(directory, limit))
        {
            if (!entry.IsLegible)
            {
                Console.Out.WriteLine($"— {entry.FileName}: ilegible, omitido");
                continue;
            }

            Console.Out.WriteLine(FormatEntry(entry));
        }

        return 0;
    }
    catch (HistoryException ex)
    {
        Console.Error.WriteLine($"error: {ex.Message}");
        return 2;
    }
});

static string FormatEntry(HistoryEntry entry)
{
    var savedAt = entry.Meta?.SavedAt is { } savedAtValue && DateTime.TryParse(savedAtValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
        ? parsed.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
        : "—";
    var command = entry.Meta?.Command ?? "—";

    if (entry.Summary is not { } summary)
        return $"{savedAt}  {command}  —  —  {entry.FileName}";

    var verdict = summary.Breaking > 0 ? "FAILED" : summary.PotentiallyBreaking > 0 ? "WARNING" : "PASSED";
    var counts = $"{summary.Breaking} breaking · {summary.PotentiallyBreaking} potentially · {summary.Compatible} compatible";

    return $"{savedAt}  {command}  {verdict}  {counts}  {entry.FileName}";
}

var rootCommand = new RootCommand("ContractWatch: detecta cambios que rompen consumidores de tu API");
rootCommand.Add(compareCommand);
rootCommand.Add(checkCommand);
rootCommand.Add(initCommand);
rootCommand.Add(historyCommand);

return await rootCommand.Parse(args).InvokeAsync();
