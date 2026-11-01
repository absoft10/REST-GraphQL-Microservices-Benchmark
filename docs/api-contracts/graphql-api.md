# GraphQL API: `Lms.Edge.GraphQL`

| | |
|---|---|
| Endpoint | `POST http://{edge-graphql-host}:{port}/graphql`. Plain HTTP/1.1 (O-6). |
| Server | HotChocolate. The version is pinned at implementation (Replace, doc 02 §3). |
| Schema | [schema.graphql](schema.graphql) |
| Shared rules | [errors-and-pagination.md](errors-and-pagination.md) |
| Selection | Clients select **exactly** the A-6 required fields (AP-7, DECISION-003) |

## 1. Transport

- **Method:** `POST` only. `GET` queries are disabled: they are not used, and they would enable HTTP caching paths (B-7).
- **Request headers:** `Content-Type: application/json`, `Accept: application/json`, `Accept-Encoding: identity`.
- **Request body:** compact JSON with exactly the members `query`, `operationName` and `variables`:

```json
{"query":"<canonical query text>","operationName":"FlatBooks","variables":{"offset":0,"limit":100}}
```

- **Canonical query text.** The transmitted `query` is the committed document ([queries/](queries/)) with comment lines removed, runs of whitespace collapsed to one space, and the result trimmed. The exact strings follow; their SHA-256 is recorded per run as `request_body_sha256`.

```
query FlatBooks($offset: Int!, $limit: Int!) { books(offset: $offset, limit: $limit) { bookId title authors category price publishYear } }
```
```
query NestedMembers($offset: Int!, $limit: Int!) { members(offset: $offset, limit: $limit) { memberId firstName lastName loans { loanId loanDate returnDate book { bookId title authors } } } }
```

These are ⟨P⟩: the book field names may change after O-7.

- **Persisted queries, automatic persisted queries and query batching:** disabled (B-7).
- **Response:** `200 application/json; charset=utf-8`, body `{"data":{...}}` on success. There is no `errors` key on success, and no `extensions` key; any server timing or tracing extensions are disabled.

## 2. Operations used by the benchmark

### 2.1 F-G: `FlatBooks`

Variables are `{"offset":0,"limit":N}`. The response uses placeholder values, with `limit=2` for brevity:

```json
{"data":{"books":[{"bookId":1,"title":"T1","authors":"A1","category":"C1","price":12.50,"publishYear":2004},{"bookId":2,"title":"T2","authors":"A2","category":null,"price":null,"publishYear":1998}]}}
```

Internal calls: exactly 1. `GET /internal/books?offset=0&limit=N`.

### 2.2 N-G: `NestedMembers`

Variables are `{"offset":0,"limit":M}`. The response:

```json
{"data":{"members":[{"memberId":1,"firstName":"F1","lastName":"L1","loans":[{"loanId":101,"loanDate":"2026-03-02","returnDate":"2026-03-20","book":{"bookId":17,"title":"T17","authors":"A17"}},{"loanId":102,"loanDate":"2026-05-11","returnDate":null,"book":{"bookId":42,"title":"T42","authors":"A42"}}]}]}}
```

Internal calls: exactly 3, matching N-RC and N-RM ([internal-domain-api.md](internal-domain-api.md)):
1. The `members` resolver calls `GET /internal/members?offset&limit`.
2. `Member.loans` uses a **batch DataLoader** keyed by `memberId`, which makes one call `GET /internal/loans?memberIds=<sorted unique>` per request.
3. `Loan.book` uses a **batch DataLoader** keyed by `bookId`, which makes one call `GET /internal/books?ids=<sorted unique>` per request.

**DataLoader rules:**
- Request-scoped. One batch per level.
- The maximum batch size is set at least as high as the largest key set (M·L), so that a level is never split. Verify at implementation.
- Keys are sorted and de-duplicated by `Lms.DomainClient`.
- The results are re-associated to their parents by key, preserving the §1 ordering of errors-and-pagination.md.
- `[UseProjection]`, `[UseFiltering]` and `[UseSorting]` are **not used** (AP-8).

## 3. Field-to-configuration mapping

| Configuration | Operation | Selected fields |
|---|---|---|
| F-G | `FlatBooks` | `bookId title authors category price publishYear` ⟨P⟩ |
| N-G | `NestedMembers` | `memberId firstName lastName`; `loans { loanId loanDate returnDate }`; `book { bookId title authors }` ⟨P⟩ |

## 4. Errors

These follow errors-and-pagination.md §4 and the GraphQL-over-HTTP conventions for `application/json`. Exact status codes are verified against the pinned HotChocolate version.

| Case | HTTP | Body |
|---|---|---|
| Malformed JSON body, or a GraphQL parse or validation error | 200 or 400 (HotChocolate behaviour with `Accept: application/json`, recorded) | `{"errors":[{"message":"…","extensions":{"code":"…"}}]}`, no `data` |
| Argument out of range (`offset < 0`, `limit` outside 1–1000) | 200 | `{"data":null,"errors":[{"message":"…","path":["books"],"extensions":{"code":"INVALID_ARGUMENT"}}]}` |
| Upstream domain failure | 200 | A field error with `extensions.code = "UPSTREAM_FAILURE"`. Nulls propagate to the nearest nullable parent; with non-null lists, that yields `"data": null`. |
| Unhandled exception | 200 | `extensions.code = "INTERNAL"`. No exception detail (`IncludeExceptionDetails = false`). |

**Benchmark consequence:** JMeter asserts a 2xx status, **and** the absence of an `errors` key, **and** the expected record count (M2 §2.5). Any of these cases is therefore a failed task.

## 5. Server settings recorded under B-4 (not tuned)

- HotChocolate defaults are kept (AD-24, a Tier 2 decision). That includes **cost analysis**: the nested query is checked locally against the default limits, and the result is recorded.
- Recorded: the operation document cache, introspection (enabled; not used by the benchmark), `IncludeExceptionDetails = false`, and the scalar bindings for `Decimal` and `Date`.
