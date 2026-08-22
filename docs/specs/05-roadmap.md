# Roadmap post-MVP

Orden deliberado: cada etapa entrega valor autónomo sin obligar a la siguiente.

## Fase 1 — CLI útil (MVP) ✅

`compare` con catálogo completo de 18 reglas (CW001–CW018), salida console/json/markdown, exit codes. Publicable como tool de .NET (`dotnet tool install contractwatch`).

## Fase 2 — Integración en PRs ✅

```
PR opened
   ↓ checkout base ref
   ↓ ContractWatch action
   ↓ PR comment (idempotente)
   ↓ exit code según --fail-on
```

Implementada como acción compuesta (`action.yml`) reutilizable desde cualquier repo:

```yaml
- uses: Steve-XYZ/contract-watch@main
  with:
    base-spec: openapi.json      # resuelto en la rama base
    head-spec: openapi.json      # generado por el build del consumidor
    fail-on: breaking
```

El comentario se publica/actualiza de forma idempotente vía marcador `<!-- contractwatch:<tag> -->`; varios reportes por PR coexisten con tags distintos. El formato del comentario es `--format markdown` (evolución sobre el plan original de parsear JSON en bash: el reporter vive en Core y tiene tests). El JSON sigue disponible para integraciones programáticas.

Limitación conocida: en PRs de forks el GITHUB_TOKEN es read-only y el comentario no puede publicarse; el check de exit code sí funciona.

Después, si hay tracción: GitHub App con checks por commit.

## Fase 3 — Gate de CI

```
contractwatch check --baseline origin/main
```

Resuelve el spec de la rama base vía git y aplica exit codes (`04-cli.md`). Suppressions justificadas por archivo (`.contractwatchignore`): ruleId + path + razón, revisables en el PR que las introduce.

## Fase 4 — Políticas y formatos

- Compatibility policies por repo: qué severidades bloquean, allowlists por regla.
- SARIF output para mostrar hallazgos inline en el PR.
- Otros contratos, uno a la vez según demanda real:
  - AsyncAPI
  - GraphQL schema compatibility
  - protobuf/gRPC
  - JSON Schema genérico
  - database schema contracts
  - event contracts / MassTransit message contracts
  - consumer-driven contracts (Pact-like)

## Fase 5 — "¿Quién se rompe?"

Guardar relaciones de consumo:

```
PlayerManager ─── consumes ─── Payments API v2
Admin ────────── consumes ─── PlayerManager API
Propagator ───── consumes ─── DrawResultEvent
```

Un PR entonces puede decir:

> Changing `PlayerDto.country`
>
> Affected consumers:
> - admin-web
> - reporting-service
> - identity-sync
>
> Confidence: high

Esto convierte el diff en grafo de propagación y acerca el proyecto a producto comercial. Requiere registro de servicios (primer estado durable del sistema) y políticas de confianza sobre esas relaciones.

## Exploratorio

- Version history: evolución de compatibilidad de una API a lo largo del tiempo.
- Dashboard de contratos y sus consumidores.
- AI explanation de cada breaking change y sugerencia del cambio mínimo compatible.

## No hacer

- Frontend propio mientras el CLI cubra el flujo (decisión compartida con Webhook Replay: no mantener dos frontends).
- Base de datos hasta la fase 5.
- Soporte Swagger 2.0: migrar a OpenAPI 3 es requisito previo del usuario, no trabajo de la herramienta.
