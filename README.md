# ContractWatch

Detecta automáticamente cuándo un cambio en una API rompe consumidores. Compara dos contratos OpenAPI y clasifica cada cambio por su impacto real:

```
contractwatch compare main/openapi.json pr/openapi.json
```

```
✗ BREAKING POST /orders
  Required request property added: customerId
```

No hace un diff: entiende compatibilidad de contratos — y entiende que la compatibilidad no es simétrica entre lo que envían los consumidores (input) y lo que devuelve el productor (output).

## Stack

.NET 10 · System.CommandLine · OpenAPI.NET · xUnit

## Documentación

| Spec | Contenido |
|---|---|
| [01-visión](docs/specs/01-vision.md) | problema, posicionamiento, objetivo del MVP |
| [02-arquitectura](docs/specs/02-arquitectura.md) | stack, estructura de la solución, modelo de dominio |
| [03-reglas](docs/specs/03-reglas.md) | catálogo completo: breaking / potentially breaking / compatible |
| [04-cli](docs/specs/04-cli.md) | comando, opciones, exit codes, formatos de salida |
| [05-roadmap](docs/specs/05-roadmap.md) | GitHub integration, gate de CI, "¿quién se rompe?" |

## Uso en tu repo (GitHub Action)

```yaml
# .github/workflows/api-compatibility.yml
name: api-compatibility
on: [pull_request]
permissions:
  contents: read
  pull-requests: write
jobs:
  contractwatch:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: Steve-XYZ/contract-watch@main
        with:
          base-spec: openapi.json    # generado por TU build, resuelto en la rama base
          head-spec: openapi.json    # la versión de este PR
```

El action comenta el veredicto en el PR (idempotente) y falla si hay cambios breaking.

## Instalación

### Desde un GitHub Release

Descarga `ContractWatch.<versión>.nupkg` de un [release](https://github.com/Steve-XYZ/contract-watch/releases) e instala como tool global de .NET:

```bash
dotnet tool install -g --add-source <carpeta-con-el-nupkg> contractwatch
```

El paquete es autocontenido (no necesita fuentes adicionales). Requiere .NET 10 SDK/runtime.

### Build local

```bash
git clone https://github.com/Steve-XYZ/contract-watch && cd contract-watch
dotnet pack -c Release -o artifacts
dotnet tool install -g --add-source artifacts contractwatch
```

Con el tool instalado:

```bash
contractwatch init    # scaffolding opcional: crea .contractwatch.json, .contractwatchignore y consumers.json (nunca sobreescribe)
contractwatch --help
contractwatch compare openapi-v1.json openapi-v2.json
```

Para actualizar a una nueva versión: `dotnet tool update -g --add-source <carpeta> contractwatch`; para desinstalar: `dotnet tool uninstall -g contractwatch`.

## Estado

Fases 1–3 completas: CLI con las 18 reglas del catálogo (`compare` + `check --baseline`, formatos console/json/markdown/sarif, exit codes), GitHub Action con comentario idempotente en PRs y gate de CI con suppressions justificadas (`.contractwatchignore`). Fase 4 parcial: policies por repo (`.contractwatch.json` con `failOn` y `severityOverrides`) y salida SARIF (`--format sarif`). Fase 5 MVP: análisis de impacto declarativo (`consumers.json`) que responde *¿quién se rompe?* con confianza alta/media por consumidor; el grafo multi-API encadena el impacto a consumidores de consumidores vía `spec` (confianza compuesta por el mínimo de la cadena, ciclos rechazados). Historial local: `--save <dir>` guarda cada reporte como JSON con metadatos y `contractwatch history` lista/consulta lo guardado. Empaquetado como .NET global tool (`dotnet tool install contractwatch`, paquete autocontenido con versión única desde MSBuild) y release automático por tags `v*` que adjunta los nupkg a un GitHub Release; la publicación en NuGet.org queda pendiente. Sin BD durable ni confianza por telemetría; los demás formatos de contrato quedan para después. Cada cambio reportado incluye una sugerencia determinista de remediación (`↳` en consola, columna/campo `suggestion` en markdown/json/sarif).
