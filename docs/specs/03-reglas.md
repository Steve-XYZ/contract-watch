# Catálogo de reglas

Cada cambio detectado tiene un `RuleId` estable (para suppressions y políticas futuras), una severidad y un contexto de dirección.

## Severidades

| Severidad | Significado |
|---|---|
| `Breaking` | Un consumidor conforme al contrato anterior fallará. |
| `PotentiallyBreaking` | Seguro para consumidores tolerantes; rompe consumidores estrictos o exhaustivos. |
| `Compatible` | Sin impacto para consumidores válidos. |

La categoría "potentially breaking" existe porque algunos cambios no tienen veredicto universal: depende de cómo consumen los clientes (enums exhaustivos, validación estricta de schemas, codegen).

## Dirección

- **Input** — lo que envía el consumidor: parámetros y request body.
- **Output** — lo que devuelve el productor: response bodies y headers.

Regla general: endurecer inputs es breaking; endurecer outputs es compatible para quien consume; ampliar outputs puede romper consumidores exhaustivos.

## Breaking (MVP)

| RuleId | Regla | Contexto | Ejemplo |
|---|---|---|---|
| CW001 | EndpointRemoved | — | desaparece `DELETE /orders/{id}` |
| CW002 | OperationRemoved | — | `/orders` pierde el method POST (el path sigue vivo) |
| CW003 | RequiredParameterAdded | input | query `page` pasa a required |
| CW004 | RequiredPropertyAdded | input | request body: `currency` entra en `required` |
| CW005 | ParameterTypeChanged | input | `limit: integer → string` |
| CW006 | EnumNarrowed | input | `["USD","EUR"] → ["USD"]` |
| CW007 | ResponseStatusRemoved | output | se elimina la documentación del 404 |
| CW008 | ResponsePropertyTypeChanged | output | `amount: number → string` |
| CW009 | ResponsePropertyRemoved | output | `createdAt` ya no aparece en la respuesta |

Nota sobre CW009: quitar una propiedad documentada rompe a los consumidores que la leían, aunque fuera opcional en el schema anterior.

## Potentially breaking (MVP)

| RuleId | Regla | Contexto | Ejemplo |
|---|---|---|---|
| CW010 | EnumWidened | output | `["PAID","FAILED"] → ["PAID","FAILED","PENDING"]` |
| CW011 | RequiredResponsePropertyAdded | output | la respuesta exige un campo nuevo; consumidores con validación estricta fallan |
| CW012 | NullableRemoved | output | `settledAt: string\|null → string` |

CW010 es el caso bandera:

```json
// response enum antes          // response enum después
["PAID", "FAILED"]              ["PAID", "FAILED", "PENDING"]
```

Un cliente con un `switch` exhaustivo sobre esos valores no maneja `PENDING`. Para un cliente tolerante es ruido. De ahí la severidad intermedia.

## Compatible (MVP)

| RuleId | Regla | Contexto | Ejemplo |
|---|---|---|---|
| CW013 | EndpointAdded | — | nuevo `GET /refunds` |
| CW014 | OptionalParameterAdded | input | nuevo query opcional `locale` |
| CW015 | OptionalPropertyAdded | input | `metadata` opcional en request body |
| CW016 | EnumWidened | input | `["USD","EUR"] → ["USD","EUR","BRL"]` |
| CW017 | ResponseStatusAdded | output | se documenta un 404 que antes no existía |
| CW018 | MetadataOnlyChanged | ambos | cambian summaries, descriptions, tags o examples |

## AsyncAPI (CW019–CW027)

El catálogo es único para todos los formatos: la numeración continúa tras las reglas HTTP y `severityOverrides` y suppressions validan contra el mismo rango (`CW001..CW027`). El tipo de cada documento se auto-detecta por contenido: si la raíz tiene campo `asyncapi` (2.x y 3.x, en JSON) se trata como AsyncAPI; mezclar un OpenAPI con un AsyncAPI en una misma comparación es error de carga (exit 2).

Cada canal con una operación se normaliza igual que un endpoint HTTP: el canal hace de path y la acción de método (`PUBLISH`/`SUBSCRIBE` en 2.x, `SEND`/`RECEIVE` en 3.x). La dirección respeta la asimetría general del catálogo:

- **Outbound** (`publish`/`send`) — mensajes que el dueño del contrato emite hacia los consumidores; semántica de *output*: quitar propiedades rompe a quien lee, ampliar enums puede romper consumidores exhaustivos.
- **Inbound** (`subscribe`/`receive`) — mensajes que el dueño consume de quienes producen; semántica de *input*: endurecer el payload rompe a quien envía.

### Breaking

| RuleId | Regla | Dirección | Ejemplo |
|---|---|---|---|
| CW019 | ChannelRemoved | — | desaparece el canal `legacy/audit` |
| CW020 | ChannelOperationRemoved | — | `orders/events` pierde su operación `publish` (el canal sigue vivo vía `subscribe`) |
| CW021 | MessageRequiredPropertyAdded | inbound | el payload consumido exige `referenceId` nuevo |
| CW022 | MessagePropertyTypeChanged | ambas | `eta: string → number` en el payload publicado |
| CW023 | MessagePropertyRemoved | outbound | ya no se publica `email` en `users/signedup` |
| CW025 | MessageEnumNarrowed | inbound | `["card","transfer"] → ["card"]` en un mensaje consumido |

### Potentially breaking

| RuleId | Regla | Dirección | Ejemplo |
|---|---|---|---|
| CW024 | MessageEnumWidened | outbound | `["pending","shipped","delivered"] → [+ "cancelled"]`: el switch exhaustivo del consumidor no maneja el caso nuevo |

### Compatible

| RuleId | Regla | Dirección | Ejemplo |
|---|---|---|---|
| CW026 | ChannelAdded | — | nuevo canal `refunds/issued` |
| CW027 | MessageOptionalPropertyAdded | outbound | `marketingOptIn` opcional en el payload publicado |

### Límites explícitos

- Solo documentos JSON: un AsyncAPI en YAML se rechaza con un error que lo indica.
- Mensajes compuestos (`oneOf`) y operaciones v3 con varios mensajes: se compara el primer miembro.
- `$ref` solo locales al documento (`#/...`); referencias externas son error.

## Asimetrías que las reglas deben respetar

El mismo cambio físico tiene veredictos opuestos según dirección:

```json
// request enum:  ["USD","EUR"] → ["USD"]           ⇒ BREAKING   (CW006)
// request enum:  ["USD","EUR"] → ["USD","BRL"]     ⇒ COMPATIBLE (CW016)
// response enum: ["PAID","FAILED"] → [+ "PENDING"] ⇒ POTENTIAL  (CW010)
```

Y según severidad de política del consumidor: CW011 (required en output) solo rompe a quien valida estrictamente. El MVP lo reporta como advertencia; bloquear o no en CI queda como decisión de política (`04-cli.md`).

## Fuera del MVP (explícito)

- Cambios en servidores (`servers[]`), security schemes y scopes.
- Detección de renombres semánticos (`total → totalAmount`).
- Compatibilidad de `oneOf/anyOf` más allá de tipos primitivos.
- Versionado de docs múltiples; el MVP compara exactamente dos documentos.
