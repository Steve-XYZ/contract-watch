# Visión

## Problema

En sistemas de microservicios, un cambio aparentemente inocente en una API rompe consumidores en producción: un campo pasa a ser requerido, un tipo cambia, un enum se restringe. Nada lo detecta antes del deploy porque el diff textual de `openapi.json` no entiende compatibilidad.

## Qué es

ContractWatch recibe dos versiones de una API (`main/openapi.json` vs `PR/openapi.json`) y clasifica cada cambio por su impacto en los consumidores:

```
BREAKING

POST /payments
- `currency` is now required
- response.status changed integer → string

GET /players/{id}
- 404 response removed

SAFE

POST /bets
- optional field `metadata` added
```

No hace un diff: entiende compatibilidad de contratos.

## Posicionamiento

Junto a Webhook Replay, cubre la confiabilidad de integraciones en ambos extremos:

| Proyecto | Pregunta que responde | Momento |
|---|---|---|
| Webhook Replay | "Me llegó este webhook, ¿qué pasó?" | runtime |
| ContractWatch | "Voy a cambiar esta API, ¿a quién voy a romper?" | antes del deploy |

## Objetivo del MVP

Que esto:

```
contractwatch compare examples/v1.json examples/v2.json
```

produzca:

```
✗ BREAKING POST /orders
  Required request property added: customerId

✗ BREAKING GET /orders/{id}
  Response property changed:
    amount: number → string

✓ COMPATIBLE POST /orders
  Optional property added: metadata

3 breaking changes · 1 potentially breaking · 7 compatible changes
```

Alcance estricto del MVP: solo OpenAPI (3.x), solo CLI. Sin base de datos, sin frontend, sin integraciones hasta tener esta primera versión publicable.

## Principio central

La compatibilidad no es simétrica entre productores y consumidores:

- Restringir un enum de **entrada** rompe clientes; ampliarlo no.
- Ampliar un enum de **salida** es seguro para clientes tolerantes pero rompe consumidores exhaustivos (`switch` sin default).

Cada regla declara su contexto (input/output) y su severidad en consecuencia. El catálogo completo vive en `03-rules-catalog.md`.
