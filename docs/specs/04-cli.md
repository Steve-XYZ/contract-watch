# CLI

## Comandos

### compare

```
contractwatch compare <old.json> <new.json> [options]
```

| Argumento/Opción | Tipo | Default | Descripción |
|---|---|---|---|
| `old` | path | — | contrato base (ej. `main/openapi.json`) |
| `new` | path | — | contrato propuesto (ej. `PR/openapi.json`) |
| `--format` | `console` \| `json` \| `markdown` \| `sarif` | `console` | formato del reporte |
| `--fail-on` | `breaking` \| `potentially` \| `never` | `breaking` (o `.contractwatch.json`) | severidad mínima que produce exit code 1 |
| `--save` | directorio | — | guarda el reporte como JSON en el historial local (ver [history](#history---historial-local-de-reportes)) |
| `--explain` | `fake` \| `openai` | desactivado (o `.contractwatch.json`) | activa las explicaciones con IA por proveedor (ver [explicaciones con IA](#explicaciones-con-ia-opcional)) |
| `--explain-model` | string | default del proveedor | modelo que usa el proveedor de explicaciones |

`check` comparte `--format`, `--fail-on`, `--save`, `--explain` y `--explain-model`.

## Exit codes

| Código | Condición |
|---|---|
| `0` | Sin cambios con severidad >= umbral de `--fail-on` |
| `1` | Hay cambios que superan el umbral (bloquea CI) |
| `2` | Error: archivo inexistente, JSON inválido, documento que no es OpenAPI, `.contractwatch.json`, `.contractwatchignore` o `consumers.json` inválidos; proveedor de explicaciones desconocido o activo sin `CONTRACTWATCH_AI_KEY` |

Con el default `--fail-on breaking`, cambios `PotentiallyBreaking` no fallan el build: se muestran como advertencia.

## Salida console

```
✗ BREAKING POST /orders
  Required request property added: customerId        [CW004]
    ↳ Introduce the property as optional with a default value and promote it to required only in a major version.

✗ BREAKING GET /orders/{id}
  Response property changed:
    amount: number → string                          [CW008]

⚠ POTENTIAL  GET /payments
  Response enum widened: PAID, FAILED → + PENDING    [CW010]

✓ COMPATIBLE POST /orders
  Optional property added: metadata                  [CW015]

─────────────────────────────────────
3 breaking · 1 potentially breaking · 7 compatible
```

Orden de reporte: `Breaking`, luego `PotentiallyBreaking`, luego `Compatible`; dentro de cada grupo por método y path. Bajo cada detalle puede aparecer una línea adicional `↳` con la sugerencia determinista de remediación para esa regla (CW001–CW018 tienen texto en el catálogo) y, con `--explain` activo, una segunda `↳ IA:` con la explicación del proveedor (multi-línea se aplasta en una sola).

## Salida markdown

Pensada para comentarios de PR (la consume la GitHub Action de Fase 2). Veredicto según severidad máxima presente: `FAILED` (hay breaking), `WARNING` (solo potentially breaking), `PASSED`.

```markdown
## API compatibility: FAILED

This PR introduces **6 breaking** contract changes.

| Severity | Operation | Change | Rule | Suggestion |
|---|---|---|---|---|
| ✗ Breaking | `POST /orders` | Required request property added: currency | CW004 | Introduce the property as optional with a default value and promote it to required only in a major version. |
| ⚠ Potentially breaking | `GET /payments` | Response enum widened: status | CW010 | Announce the new case in the changelog and let consumers handle it before emitting it. |

6 breaking · 1 potentially breaking · 7 compatible
```

## Salida json

```json
{
  "tool": "contractwatch",
  "version": "0.1.0",
  "summary": {
    "breaking": 3,
    "potentiallyBreaking": 1,
    "compatible": 7
  },
  "changes": [
    {
      "ruleId": "CW004",
      "ruleName": "RequiredPropertyAdded",
      "severity": "Breaking",
      "location": {
        "path": "/orders",
        "method": "POST",
        "jsonPointer": "/paths/~1orders/post/requestBody/required"
      },
      "message": "Required request property added: customerId",
      "oldValue": null,
      "newValue": "customerId",
      "suggestion": "Introduce the property as optional with a default value and promote it to required only in a major version.",
      "explanation": null
    }
  ]
}
```

El campo `suggestion` lleva la remediación determinista de la regla y puede ser `null` (igual que `oldValue`/`newValue`). El campo `explanation` lleva la explicación del proveedor de IA y es `null` cuando `--explain` no está activo o el proveedor falló para ese hallazgo.

Errores van a stderr en texto plano; nada de JSON parcial ante fallo de parsing.

## Salida sarif

`--format sarif` emite [SARIF 2.1.0](https://docs.oasis-open.org/sarif/sarif/v2.1.0/sarif-v2.1.0.html), el formato que consume GitHub code scanning: los hallazgos aparecen anotados inline en el PR y en la pestaña Security.

- Solo incluye cambios `Breaking` (level `error`) y `PotentiallyBreaking` (level `warning`); los compatibles se excluyen porque SARIF reporta problemas.
- Cada result lleva `ruleId`, `ruleIndex` (índice en `tool.driver.rules`), `message.text` y `locations[0].physicalLocation.artifactLocation.uri` con la ruta del spec evaluado.
- `properties` preserva la clasificación de ContractWatch: `severity`, `path`, `method`, la `suggestion` de remediación (puede ser `null`) y la `explanation` del proveedor de IA (`null` sin `--explain`).

```json
{
  "$schema": "https://json.schemastore.org/sarif-2.1.0.json",
  "version": "2.1.0",
  "runs": [
    {
      "tool": {
        "driver": {
          "name": "contractwatch",
          "version": "0.1.0",
          "informationUri": "https://github.com/Steve-XYZ/contract-watch",
          "rules": [
            { "id": "CW003", "name": "RequiredParameterAdded" }
          ]
        }
      },
      "results": [
        {
          "ruleId": "CW003",
          "ruleIndex": 0,
          "level": "error",
          "message": { "text": "Required parameter added: page" },
          "locations": [
            {
              "physicalLocation": {
                "artifactLocation": { "uri": "openapi.json" }
              }
            }
          ],
          "properties": {
            "severity": "Breaking",
            "path": "/orders",
            "method": "POST",
            "suggestion": "Introduce the parameter as optional with a server-side default and promote it to required only in a major version."
          }
        }
      ]
    }
  ]
}
```

## Ejemplos

```
contractwatch compare examples/v1.json examples/v2.json
contractwatch compare main/openapi.json pr/openapi.json --format json
contractwatch compare old.json new.json --fail-on potentially   # CI estricto
contractwatch compare main/openapi.json pr/openapi.json --format sarif > contractwatch.sarif
contractwatch compare old.json new.json --save reports          # guarda en el historial local
contractwatch compare old.json new.json --explain fake          # explicación determinista sin red
CONTRACTWATCH_AI_KEY=... contractwatch compare old.json new.json --explain openai
```

## check — gate de CI

```
contractwatch check --baseline <git-ref> <spec-path> [options]
```

Resuelve la versión base del spec vía `git show <ref>:<spec-path>` (sin checkout) y la compara contra el archivo del árbol de trabajo, en la misma ruta relativa. Comparte `--format`, `--fail-on`, `--save`, `--explain`, `--explain-model`, suppressions y policies con `compare`; mismos exit codes. Fallos de git (ref inexistente, archivo ausente en el ref) → exit 2.

```
contractwatch check --baseline origin/main openapi.json
contractwatch check --baseline HEAD examples/v1.json --format markdown
contractwatch check --baseline origin/main openapi.json --save reports
```

## init — scaffolding de configuración

```
contractwatch init
```

Crea en el directorio actual los tres archivos de configuración que `compare` y `check` auto-detectan: `.contractwatch.json`, `.contractwatchignore` y `consumers.json`, cada uno con una plantilla mínima válida e inerte (no cambia el comportamiento de la herramienta). Nunca sobreescribe: un archivo que ya existe se reporta como existente y se preserva intacto; no hay `--force`.

```
✓ creado  .contractwatch.json
- ya existe  .contractwatchignore
✓ creado  consumers.json
Listo. 2 creado(s), 1 existente(s).
```

Errores de I/O → exit 2 con el mensaje en stderr; caso normal exit 0.

## history — historial local de reportes

`--save <directorio>` (en `compare` y `check`) guarda cada reporte como JSON en `<directorio>/yyyyMMdd-HHmmss-<tipo>.json` (`tipo` es `compare` o `check`; dos saves en el mismo segundo agregan sufijo `-2`, `-3`, …). El directorio se crea si no existe. El archivo contiene exactamente el objeto de `--format json` más un sobre `meta`:

```json
{
  "tool": "contractwatch",
  "version": "0.2.0",
  "summary": { "breaking": 6, "potentiallyBreaking": 0, "compatible": 4 },
  "changes": [ ... ],
  "meta": {
    "savedAt": "2026-08-22T17:03:11.3917594Z",
    "command": "compare",
    "inputs": ["/repo/main/openapi.json", "/repo/pr/openapi.json"]
  }
}
```

En `check`, `inputs[0]` es el git-ref y `inputs[1]` la ruta del spec. Guardar es best-effort: tras guardar se imprime `Reporte guardado en <ruta>` (stderr en formatos `json`/`sarif` para no contaminar la salida estructurada; stdout en `console`/`markdown`) y un fallo de I/O emite un aviso en stderr sin alterar el exit code.

El comando `history` consulta ese historial:

```
contractwatch history [--dir <directorio>] [--limit N] [--show <archivo>]
```

| Opción | Default | Descripción |
|---|---|---|
| `--dir` | `reports` | directorio del historial |
| `--limit` | `20` | máximo de reportes listados |
| `--show` | — | imprime el contenido crudo de un archivo del historial y termina |

Sin `--show`, lista los reportes del más nuevo al más viejo:

```
2026-08-22T17:03:11Z  compare  FAILED      6 breaking · 0 potentially · 4 compatible   yyyyMMdd-HHmmss-compare.json
2026-08-21T09:15:02Z  check    PASSED      0 breaking · 0 potentially · 3 compatible   yyyyMMdd-HHmmss-check.json
— sin-meta.json: ilegible, omitido
```

El veredicto deriva del resumen: `FAILED` si hay breaking, `WARNING` si solo potentially breaking, `PASSED` en el resto. Archivos que no parsean o no tienen `summary` ni `meta` se marcan `ilegible, omitido` y el listado continúa. `--show <archivo>` acepta una ruta relativa a `--dir` o absoluta.

Errores: directorio inexistente o archivo inexistente en `--show` → mensaje claro + exit 2; caso normal exit 0.

## Supresiones (`.contractwatchignore`)

Archivo por repo, auto-detectado en el directorio de trabajo para `compare` y `check` (`--suppress-file` lo sobreescribe). Una supresión por línea:

```
# <ruleId> <path> [<method>] :: <razón obligatoria>
CW001 /legacy/orders :: retirada planificada Q4
CW003 /orders POST :: headers acordados con mobile (#42)
```

- Coincidencia exacta de ruleId y path; el method es opcional.
- La razón es obligatoria: una supresión sin justificación es error de parsing (exit 2).
- Los cambios suprimidos se excluyen del reporte y del cálculo del exit code; en console/markdown se imprime cuántos fueron suprimidos.
- La revisabilidad vive en el diff: el PR que introduce o amplía el archivo muestra qué deja de bloquearse y por qué.

## Policies (`.contractwatch.json`)

Archivo por repo, auto-detectado en el directorio de trabajo para `compare` y `check`:

```json
{
  "failOn": "potentially",
  "severityOverrides": {
    "CW010": "compatible",
    "CW011": "breaking"
  }
}
```

Ambos campos son opcionales.

- `failOn`: umbral **por defecto** que aplica solo cuando el usuario NO pasa `--fail-on`. Precedencia: **flag > policy > default (`breaking`)**. Valores: `breaking|potentially|never`.
- `severityOverrides`: re-mapea la severidad de TODOS los cambios de esa regla después de comparar y antes de suprimir/reportear. Valores: `breaking|potentially|compatible`. Útil para endurecer reglas potencialmente breaking o degradar a compatible las que tu consumers ya toleran.
- `explain` y `explainModel`: activación por defecto del proveedor de explicaciones con IA (ver [explicaciones con IA](#explicaciones-con-ia-opcional)). Precedencia **flag > policy > desactivado**; `explain` acepta `fake|openai`, un valor desconocido es error de parsing.
- Anti-typos: las claves de `severityOverrides` deben ser reglas del catálogo (CW001–CW018); una regla desconocida es error de parsing. Lo mismo aplica a `explain`.
- Errores → exit 2 con mensaje claro: JSON malformado, `failOn` inválido, regla desconocida o severidad inválida.

El re-mapeo ocurre antes de suppressions y del reporte: un cambio degradado a `compatible` deja de superar umbrales, se cuenta como compatible y no aparece en la salida SARIF.

## Explicaciones con IA (opcional)

Capa opcional sobre la sugerencia determinista: un proveedor explica cada hallazgo (por qué rompe y el paso mínimo de migración). **Desactivado por defecto**; sin `--explain` la salida de los cuatro formatos es exactamente la de siempre.

```json
{
  "failOn": "potentially",
  "explain": "openai",
  "explainModel": "gpt-4o-mini"
}
```

- Activación: flag (`--explain fake|openai`) o policy (`.contractwatch.json`, claves `explain` y `explainModel`). Precedencia: **flag > policy > desactivado**. Anti-typos igual que el resto: un proveedor desconocido es error de parsing (exit 2).
- Proveedores:
  - `fake`: determinista, sin red, eco del hallazgo con formato `[fake] <ruleId> (<regla>) at <METHOD> <path>: <mensaje>.`. Para pruebas, demos y CI.
  - `openai`: llamada HTTP directa a chat-completions (sin SDK), modelo configurable (`--explain-model` / `explainModel`, default `gpt-4o-mini`). Compatible con cualquier endpoint OpenAI-style vía la variable `CONTRACTWATCH_AI_BASE_URL`.
- Claves SOLO por entorno: `CONTRACTWATCH_AI_KEY`. Nunca se loguea, nunca se persiste en reportes ni en el historial. Si el proveedor está activo pero la variable falta → error claro + exit 2.
- Degradación por hallazgo: si la API falla (red, status no exitoso, payload inesperado) ese hallazgo conserva solo la sugerencia determinista y se emite UN aviso agregado en stderr al final del enriquecimiento. El exit code no cambia por fallos del proveedor.
- Salida: la explicación viaja junto a `suggestion` — línea `↳ IA:` en consola, columna `AI` en markdown (solo cuando hay explicaciones), campo `explanation` en json y `properties.explanation` en sarif.
- El texto de IA SÍ se embebe en los reportes guardados con `--save` (es contenido del reporte); lo que jamás viaja es material sensible.

```
contractwatch compare old.json new.json --explain fake
CONTRACTWATCH_AI_KEY=... contractwatch compare old.json new.json --explain openai --explain-model gpt-4o-mini
```

## Registro de consumidores (`consumers.json`)

Archivo por repo, auto-detectado en el directorio de trabajo para `compare` y `check` (`--consumers <ruta>` lo sobreescribe):

```json
{
  "consumers": [
    { "service": "admin-web", "operations": ["GET /players/{id}", "POST /bets"] },
    { "service": "reporting-service", "operations": ["/players/{id}"] }
  ]
}
```

Cada operación es `"METHOD /path"` o `"/path"` (también vale `"* /path"`). El análisis de impacto cruza los cambios del diff con esas declaraciones y responde la pregunta de la Fase 5: *¿quién se rompe?*

### Semántica de confianza

| Entrada | Coincidencia | Confianza |
|---|---|---|
| `POST /orders` | método + path exactos (método sin distinción de mayúsculas) | **high** |
| `/orders` o `* /orders` | solo path, cualquier método | **medium** |

- Un cambio afecta a un consumidor si su path coincide exactamente con el de una entrada y (la entrada no declara método, o es `*`, o el método coincide). Los cambios a nivel de path — sin método, como la eliminación de un endpoint — afectan a todas las entradas de ese path, incluidas las que declaran método.
- Solo cuentan cambios `Breaking` y `PotentiallyBreaking`: los `Compatible` no impactan a nadie.
- Si varias entradas del mismo consumidor coinciden con grados distintos, prevalece `high`; cada cambio se cuenta una sola vez por consumidor.
- La lista sale ordenada por confianza descendente y luego servicio; sin duplicados.

### Interacción con policies y suppressions

El impacto se calcula **después** de aplicar `.contractwatch.json` (severityOverrides) y `.contractwatchignore`: un cambio degradado a compatible o suprimido deja de afectar a cualquier consumidor. El reporte aparece en console (`Consumidores afectados:` tras el resumen), markdown (sección `### Affected consumers`) y JSON (`affectedConsumers`); SARIF no cambia.

### El exit code no se ve afectado

El análisis es informativo: no agrega ni quita severidad al diff. Los exit codes siguen saliendo exclusivamente del umbral de `--fail-on` sobre los cambios post-supresión. Errores del archivo (JSON malformado, servicio vacío o duplicado, consumidor sin operaciones, entrada que no es `METHOD /path` ni `/path`) → exit 2.

## Decisiones

- **Exit code determinista y documentado desde el día uno.** Es el contrato real para CI; el texto es secundario.
- **JSON estable antes de GitHub integration.** El comentario de PR y cualquier integración futura consumen este schema, no la salida de consola.
- **Sin comando `check --baseline` todavía.** Primero `compare` sólido; el modo baseline (git) llega en el roadmap.
