# Shared Rules: Ordering, Pagination, ID Lists, Validation, Errors, Headers

These rules apply to both public edges and to the internal domain API, unless a rule is marked REST-only or GraphQL-only.

## 1. Ordering (approved, DECISION-003/004)

| Collection | Order |
|---|---|
| Books | `bookId` ascending |
| Members | `memberId` ascending |
| Loans (flat list, `/api/loans`) | `memberId` ascending, then `loanId` ascending |
| Loans nested under a member | `loanId` ascending |

Ordering is enforced in SQL (`ORDER BY` the primary key or key pair). It never depends on hashing or on the order of DataLoader keys.

## 2. Pagination

- Query parameters (REST) or arguments (GraphQL): `offset` and `limit`. Both are required, with no defaults, so that every benchmark request is fully explicit.
- `offset`: an integer ≥ 0. `limit`: an integer from 1 to 1000.
- Semantics: SQL `ORDER BY <pk> OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY`.
- The response holds at most `limit` items. There is **no** total count, cursor, `Link` header or envelope (README §3.1).
- Benchmark values: `offset = 0` (O, approved semantics), `limit = N` (flat) or `M` (nested). These are Tier 2 values.

## 3. ID lists (approved, DECISION-006 O-9)

- One query parameter with comma-separated decimal integers, for example `ids=3,17,42`. No spaces, no trailing comma.
- Each ID is a positive 32-bit integer. Between 1 and 1000 IDs per list.
- The server de-duplicates and sorts the IDs before querying. The response follows §1 ordering, regardless of the order of the input.
- IDs that match no row are omitted (filter semantics). This is not an error.
- Clients (`Lms.DomainClient`, the JMeter N-RM plan) send IDs **sorted ascending and de-duplicated**, so that request URLs are byte-identical across configurations.

## 4. Validation

| Condition | REST response | GraphQL response |
|---|---|---|
| A missing or non-integer `offset`/`limit` | 400 problem+json, `type` `…/errors/invalid-parameter` | GraphQL validation error (HTTP 200 with `application/json`; see graphql-api.md §4) |
| `offset < 0`, `limit` outside 1–1000 | 400 problem+json, `…/invalid-parameter` | Execution error, `extensions.code = "INVALID_ARGUMENT"`, `data: null` |
| A malformed ID list, or more than 1000 IDs | 400 problem+json, `…/invalid-id-list` | n/a (GraphQL takes no ID lists) |
| An unknown route | 404 problem+json, `…/not-found` | n/a (single endpoint) |
| A domain service is unavailable or times out (edge only) | 502 problem+json, `…/upstream-failure` | Field error on the failing field, `extensions.code = "UPSTREAM_FAILURE"`. Its non-null parent goes `null` per GraphQL rules. |
| An unhandled exception | 500 problem+json, `…/internal` (no stack trace) | `errors` with `extensions.code = "INTERNAL"` (no stack trace) |

### REST problem+json shape (RFC 9457)

```json
{"type":"https://lms-benchmark.local/errors/invalid-parameter","title":"Invalid parameter","status":400,"detail":"limit must be between 1 and 1000","instance":"/api/books"}
```

The `type` URIs are identifiers only and do not need to resolve. Errors are serialised with the same SP-1 policy (DECISION-005).

### Benchmark relevance

Every error counts as a failed task through the JMeter assertions (M2 §2.5). Error bodies are never included in latency or bytes statistics, which use successful tasks only (M2 §2.4).

## 5. Headers

### Requests (both stacks; JMeter and the internal client)

| Header | Value | Note |
|---|---|---|
| `Accept` | `application/json` | Identical for REST and GraphQL (DECISION-006) |
| `Accept-Encoding` | `identity` | No compression (O-6) |
| `Connection` | (HTTP/1.1 default keep-alive; not sent explicitly) | DECISION-002/AP-3 |
| `Content-Type` | `application/json` | GraphQL `POST` only |
| `Authorization` | `Bearer <token>` | **Only** if the JWT toggle is enabled (D-4a, off by default) |

### Responses (both stacks)

| Header | Value |
|---|---|
| `Content-Type` | `application/json; charset=utf-8` (problem responses: `application/problem+json; charset=utf-8`) |
| `Cache-Control`, `ETag`, `Last-Modified` | **Not sent** (M2 B-7; no caching) |
| `Vary` | Not sent by REST. **HotChocolate sends `Vary: Accept` on GraphQL responses.** This is a library default, recorded and not stripped (CHANGELOG C-2). |
| `Content-Encoding` | Not sent (no compression) |
| `Server` | Not sent (README §3.4) |
| Framing | Kestrel default (`Content-Length` or `Transfer-Encoding: chunked`). It is **recorded** per configuration from sample responses, not forced (verify at implementation; affects only the header part of JMeter `bytes`). |

## 6. Character encoding and JSON

All bodies are UTF-8 JSON serialised under policy SP-1 (DECISION-005):
- camelCase names;
- explicit nulls;
- decimals as JSON numbers at the stored scale;
- dates as `"YYYY-MM-DD"`;
- string escaping with `JavaScriptEncoder.Create(UnicodeRanges.All)` ⟨provisional, rule 7⟩;
- compact, with no BOM.
