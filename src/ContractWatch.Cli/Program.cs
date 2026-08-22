using System.CommandLine;
using ContractWatch.Core;
using ContractWatch.Core.Comparison;
using ContractWatch.Core.Parsing;
using ContractWatch.Core.Reporting;

var formatOption = new Option<string>("--format") { DefaultValueFactory = _ => "console" };
var failOnOption = new Option<string?>("--fail-on") { DefaultValueFactory = _ => null };
var suppressFileOption = new Option<string?>("--suppress-file");

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

var checkCommand = new Command("check", "Compara el contrato del árbol de trabajo contra el spec en un ref de git (gate de CI)");
checkCommand.Add(baselineOption);
checkCommand.Add(specArgument);
checkCommand.Add(formatOption);
checkCommand.Add(failOnOption);
checkCommand.Add(suppressFileOption);

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

async Task<int> ReportAndExit(ApiContract previous, ApiContract current, string? failOn, string artifactUri, string format, string? suppressFile)
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

    Console.Out.WriteLine(format switch
    {
        "json" => JsonReporter.Render(result),
        "markdown" => MarkdownReporter.Render(result),
        "sarif" => SarifReporter.Render(result, artifactUri),
        _ => ConsoleReporter.Render(result.Changes),
    });

    if (suppressed > 0 && format is not ("json" or "sarif"))
        Console.Out.WriteLine($"{suppressed} cambio(s) suprimido(s) según {suppressFile ?? SuppressionFile.DefaultFileName}");

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
        return await ReportAndExit(previous, current, failOn, SarifReporter.NormalizeArtifactUri(newFile.FullName), format, parseResult.GetValue(suppressFileOption));
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

    var gitRef = parseResult.GetValue(baselineOption)!;
    var specPath = parseResult.GetValue(specArgument)!.Replace('\\', '/');

    try
    {
        var baseline = await GitSpecSource.LoadAsync(gitRef, specPath, cancellationToken);
        var current = await OpenApiLoader.LoadAsync(specPath, cancellationToken);
        return await ReportAndExit(baseline, current, failOn, SarifReporter.NormalizeArtifactUri(Path.GetFullPath(specPath)), format, parseResult.GetValue(suppressFileOption));
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

var rootCommand = new RootCommand("ContractWatch: detecta cambios que rompen consumidores de tu API");
rootCommand.Add(compareCommand);
rootCommand.Add(checkCommand);

return await rootCommand.Parse(args).InvokeAsync();
