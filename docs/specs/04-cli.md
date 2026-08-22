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
| `--format` | `console` \| `json` \| `markdown` | `console` | formato del reporte |
| `--fail-on` | `breaking` \| `potentially` \| `never` | `breaking` | severidad mínima que produce exit code 1 |

## Exit codes

| Código | Condición |
|---|---|
| `0` | Sin cambios con severidad >= umbral de `--fail-on` |
| `1` | Hay cambios que superan el umbral (bloquea CI) |
| `2` | Error: archivo inexistente, JSON inválido, documento que no es OpenAPI |

Con el default `--fail-on breaking`, cambios `PotentiallyBreaking` no fallan el build: se muestran como advertencia.

## Salida console

```
✗ BREAKING POST /orders
  Required request property added: customerId        [CW004]

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

Orden de reporte: `Breaking`, luego `PotentiallyBreaking`, luego `Compatible`; dentro de cada grupo por método y path.

## Salida markdown

Pensada para comentarios de PR (la consume la GitHub Action de Fase 2). Veredicto según severidad máxima presente: `FAILED` (hay breaking), `WARNING` (solo potentially breaking), `PASSED`.

```markdown
## API compatibility: FAILED

This PR introduces **6 breaking** contract changes.

| Severity | Operation | Change | Rule |
|---|---|---|---|
| ✗ Breaking | `POST /orders` | Required request property added: currency | CW004 |
| ⚠ Potentially breaking | `GET /payments` | Response enum widened: status | CW010 |

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
      "newValue": "customerId"
    }
  ]
}
```

Errores van a stderr en texto plano; nada de JSON parcial ante fallo de parsing.

## Ejemplos

```
contractwatch compare examples/v1.json examples/v2.json
contractwatch compare main/openapi.json pr/openapi.json --format json
contractwatch compare old.json new.json --fail-on potentially   # CI estricto
```

## check — gate de CI

```
contractwatch check --baseline <git-ref> <spec-path> [options]
```

Resuelve la versión base del spec vía `git show <ref>:<spec-path>` (sin checkout) y la compara contra el archivo del árbol de trabajo, en la misma ruta relativa. Comparte `--format`, `--fail-on` y suppressions con `compare`; mismos exit codes. Fallos de git (ref inexistente, archivo ausente en el ref) → exit 2.

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

## Decisiones

- **Exit code determinista y documentado desde el día uno.** Es el contrato real para CI; el texto es secundario.
- **JSON estable antes de GitHub integration.** El comentario de PR y cualquier integración futura consumen este schema, no la salida de consola.
- **Sin comando `check --baseline` todavía.** Primero `compare` sólido; el modo baseline (git) llega en el roadmap.
