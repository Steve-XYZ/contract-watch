using System.CommandLine;
using ContractWatch.Core;
using ContractWatch.Core.Comparison;
using ContractWatch.Core.Parsing;
using ContractWatch.Core.Reporting;

var oldArgument = new Argument<FileInfo>("old");
var newArgument = new Argument<FileInfo>("new");
var formatOption = new Option<string>("--format") { DefaultValueFactory = _ => "console" };
var failOnOption = new Option<string>("--fail-on") { DefaultValueFactory = _ => "breaking" };

var compareCommand = new Command("compare", "Compara dos contratos OpenAPI y clasifica los cambios por compatibilidad");
compareCommand.Add(oldArgument);
compareCommand.Add(newArgument);
compareCommand.Add(formatOption);
compareCommand.Add(failOnOption);

compareCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var format = parseResult.GetValue(formatOption)!;
    var failOn = parseResult.GetValue(failOnOption)!;

    if (format is not ("console" or "json"))
    {
        Console.Error.WriteLine($"error: --format desconocido '{format}' (console|json)");
        return 2;
    }

    if (failOn is not ("breaking" or "potentially" or "never"))
    {
        Console.Error.WriteLine($"error: --fail-on desconocido '{failOn}' (breaking|potentially|never)");
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

        var result = new ContractComparer().Compare(previous, current);

        Console.Out.WriteLine(format == "json"
            ? JsonReporter.Render(result)
            : ConsoleReporter.Render(result.Changes));

        ChangeSeverity? threshold = failOn switch
        {
            "breaking" => ChangeSeverity.Breaking,
            "potentially" => ChangeSeverity.PotentiallyBreaking,
            _ => null,
        };

        return result.FailsAt(threshold) ? 1 : 0;
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

var rootCommand = new RootCommand("ContractWatch: detecta cambios que rompen consumidores de tu API");
rootCommand.Add(compareCommand);

return await rootCommand.Parse(args).InvokeAsync();
