using System.CommandLine;
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

compareCommand.SetAction(parseResult =>
{
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

    Console.Error.WriteLine("error: la comparación aún no está implementada (ver docs/specs)");
    return 2;
});

var rootCommand = new RootCommand("ContractWatch: detecta cambios que rompen consumidores de tu API");
rootCommand.Add(compareCommand);

return rootCommand.Parse(args).Invoke();
