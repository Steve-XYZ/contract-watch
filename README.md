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

## Estado

Fase 1 (MVP): CLI con `compare`. Sin BD, sin frontend, sin integraciones.
