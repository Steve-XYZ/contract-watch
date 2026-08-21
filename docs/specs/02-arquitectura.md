# Arquitectura

## Stack

| Capa | Tecnología |
|---|---|
| CLI | .NET 10 + System.CommandLine |
| Parsing OpenAPI | OpenAPI.NET |
| Tests | xUnit |
| Persistencia | ninguna (archivos de entrada/salida) |
| Frontend | ninguno en el MVP |

## Estructura de la solución

```
contract-watch/
├── ContractWatch.slnx
├── src/
│   ├── ContractWatch.Core/          # librería pura: parsing, comparación, reglas, reporting
│   │   ├── Parsing/
│   │   │   └── OpenApiLoader        # openapi.json → ApiContract
│   │   ├── Comparison/
│   │   │   ├── CompareOperations    # endpoints y methods
│   │   │   ├── CompareParameters    # parámetros de path/query/header
│   │   │   ├── CompareSchemas       # propiedades, required, tipos, enums
│   │   │   └── CompareResponses     # status codes y response schemas
│   │   ├── Rules/
│   │   │   ├── IContractRule
│   │   │   ├── EndpointRemoved
│   │   │   ├── RequiredPropertyAdded
│   │   │   ├── ParameterTypeChanged
│   │   │   ├── EnumNarrowed
│   │   │   └── ...
│   │   └── Reporting/
│   │       ├── ConsoleReporter      # salida humana (default del MVP)
│   │       └── JsonReporter         # salida estructurada para CI e integraciones
│   └── ContractWatch.Cli/           # host System.CommandLine: args → Core → exit code
├── tests/
│   └── ContractWatch.Core.Tests/    # cada regla con fixtures old/new
├── examples/                        # pares v1/v2 reales para demo y golden tests
└── docs/specs/
```

## Modelo de dominio

```csharp
public enum ChangeSeverity { Compatible, PotentiallyBreaking, Breaking }

public sealed record ChangeLocation(string Path, string? Method = null, string? JsonPointer = null);

public sealed record ContractChange(
    string RuleId,
    string RuleName,
    ChangeSeverity Severity,
    ChangeLocation Location,
    string Message,
    string? OldValue = null,
    string? NewValue = null);

public interface IContractRule
{
    IEnumerable<ContractChange> Evaluate(ApiContract previous, ApiContract current);
}
```

`ApiContract` es un modelo normalizado propio que produce `OpenApiLoader`. Las reglas no conocen detalles de OpenAPI.NET; eso permite sumar AsyncAPI u otros formatos después sin tocar las reglas.

## Flujo

```
old.json + new.json
   ↓ OpenApiLoader            (Parsing/)
   ↓ walkers de Comparison/    recorren operations → parameters → schemas → responses
   ↓ reglas evaluadas          → IEnumerable<ContractChange>
   ↓ agregación                orden por severidad y ubicación; resumen con conteos
   ↓ ConsoleReporter | JsonReporter
exit code                       (ver 04-cli.md)
```

## Decisiones

- **Reglas puras e intercambiables.** Cada regla recibe ambos contratos completos y devuelve cambios. Sin estado compartido ni orden implícito: agregar una regla no toca a las demás.
- **La severidad la decide la regla, no el reporter.** La misma condición puede emitir severidades distintas según dirección (required añadido: `Breaking` en input, `PotentiallyBreaking` en output).
- **Contexto explícito input/output.** La compatibilidad depende de la dirección del cambio; el catálogo (`03-reglas.md`) lo codifica por regla.
- **Core sin dependencias de consola ni I/O.** El Cli solo parsea args, invoca Core y mapea el resultado a exit code.
- **Golden files sobre mocks.** Cada regla se prueba con un par de documentos OpenAPI en `examples/`, comparando la lista de `ContractChange` resultante.
