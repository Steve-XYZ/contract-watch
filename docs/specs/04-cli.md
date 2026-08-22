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

## Exit codes

| Código | Condición |
|---|---|
| `0` | Sin cambios con severidad >= umbral de `--fail-on` |
| `1` | Hay cambios que superan el umbral (bloquea CI) |
| `2` | Error: archivo inexistente, JSON inválido, documento que no es OpenAPI, `.contractwatch.json`, `.contractwatchignore` o `consumers.json` inválidos |

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

Orden de reporte: `Breaking`, luego `PotentiallyBreaking`, luego `Compatible`; dentro de cada grupo por método y path. Bajo cada detalle puede aparecer una línea adicional `↳` con la sugerencia determinista de remediación para esa regla (CW001–CW018 tienen texto en el catálogo).

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
      "suggestion": "Introduce the property as optional with a default value and promote it to required only in a major version."
    }
  ]
}
```

El campo `suggestion` lleva la remediación determinista de la regla y puede ser `null` (igual que `oldValue`/`newValue`).

Errores van a stderr en texto plano; nada de JSON parcial ante fallo de parsing.

## Salida sarif

`--format sarif` emite [SARIF 2.1.0](https://docs.oasis-open.org/sarif/sarif/v2.1.0/sarif-v2.1.0.html), el formato que consume GitHub code scanning: los hallazgos aparecen anotados inline en el PR y en la pestaña Security.

- Solo incluye cambios `Breaking` (level `error`) y `PotentiallyBreaking` (level `warning`); los compatibles se excluyen porque SARIF reporta problemas.
- Cada result lleva `ruleId`, `ruleIndex` (índice en `tool.driver.rules`), `message.text` y `locations[0].physicalLocation.artifactLocation.uri` con la ruta del spec evaluado.
- `properties` preserva la clasificación de ContractWatch: `severity`, `path`, `method` y la `suggestion` de remediación (puede ser `null`).

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
```

## check — gate de CI

```
contractwatch check --baseline <git-ref> <spec-path> [options]
```

Resuelve la versión base del spec vía `git show <ref>:<spec-path>` (sin checkout) y la compara contra el archivo del árbol de trabajo, en la misma ruta relativa. Comparte `--format`, `--fail-on`, suppressions y policies con `compare`; mismos exit codes. Fallos de git (ref inexistente, archivo ausente en el ref) → exit 2.

```
contractwatch check --baseline origin/main openapi.json
contractwatch check --baseline HEAD examples/v1.json --format markdown
```

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
- Anti-typos: las claves de `severityOverrides` deben ser reglas del catálogo (CW001–CW018); una regla desconocida es error de parsing.
- Errores → exit 2 con mensaje claro: JSON malformado, `failOn` inválido, regla desconocida o severidad inválida.

El re-mapeo ocurre antes de suppressions y del reporte: un cambio degradado a `compatible` deja de superar umbrales, se cuenta como compatible y no aparece en la salida SARIF.

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
