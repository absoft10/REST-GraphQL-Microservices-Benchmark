# API Contracts: REST vs GraphQL Benchmark (Milestone 2, Task 3)

| | |
|---|---|
| Version | **v0.3** (2026-11-02). Book fields final (DECISION-009); changes are in [CHANGELOG.md](CHANGELOG.md). The implementation is in `benchmark/`, with tests in `benchmark/tests/`. Local pre-benchmark evidence on the research dataset is in [samples/kaggle-v3-local-prebenchmark/](samples/kaggle-v3-local-prebenchmark/README.md). TEMPORARY-fixture samples are in [samples/local-fixture/](samples/local-fixture/README.md). Postman: [postman/](postman/LMS-Benchmark.postman_collection.json). `.http`: `benchmark/http/scenarios.http`. |
| §3 choices | **Approved by the researcher, 2026-11-01** |
| Status | The structure, routes, parameters, ordering, error handling and Required Bytes algorithm implement **approved** decisions. **Book fields are final** (DECISION-009, 2026-11-02): they map one-to-one onto `BooksDatasetClean.csv` of Kaggle `elvinrustam/books-dataset` v3. No field changed from the provisional contract. |
| Authority | These contracts implement DECISION-002 … DECISION-008 and doc 03. They introduce **no new scientific decision**. Implementation-level choices made here (error format, validation limits, header details) are listed in §3 for your review. |

## Files

| File | Content |
|---|---|
| [errors-and-pagination.md](errors-and-pagination.md) | Shared rules: ordering, pagination, ID lists, validation, error formats, headers |
| [rest-api.md](rest-api.md) | Public REST edge (`Lms.Edge.Rest`): the 5 routes used by F-R, N-RC and N-RM |
| [graphql-api.md](graphql-api.md) | Public GraphQL edge (`Lms.Edge.GraphQL`): transport, headers, errors |
| [schema.graphql](schema.graphql) | GraphQL SDL |
| [queries/FlatBooks.graphql](queries/FlatBooks.graphql), [queries/NestedMembers.graphql](queries/NestedMembers.graphql) | The exact query documents (M2 A-2) |
| [nested-rest-multicall-sequence.md](nested-rest-multicall-sequence.md) | The N-RM three-call chain: requests, extraction, assertions, timing |
| [internal-domain-api.md](internal-domain-api.md) | Protocol-neutral domain-service API used by both edges |
| [required-bytes-spec.md](required-bytes-spec.md) | CRIR, Required Bytes, Actual Bytes, normalisers, golden tests |
| [crir-flat.schema.json](crir-flat.schema.json), [crir-nested.schema.json](crir-nested.schema.json) | JSON Schemas of the canonical required-information representations |

## 1. Configuration-to-request map

| Config | Requests per task | Contract |
|---|---|---|
| F-R | `GET /api/books?offset=0&limit={N}` | rest-api.md §2.1 |
| F-G | `POST /graphql`, operation `FlatBooks` | graphql-api.md §3 |
| N-RC | `GET /api/members-with-loans?offset=0&limit={M}` | rest-api.md §2.5 |
| N-RM | `GET /api/members?…`, then `GET /api/loans?memberIds=…`, then `GET /api/books?ids=…` | nested-rest-multicall-sequence.md |
| N-G | `POST /graphql`, operation `NestedMembers` | graphql-api.md §3 |

N, O (= 0), M and L are Tier 2. The placeholders are N = 100, M = 10 and L = 5. They are supplied as configuration and never hard-coded.

## 2. Formerly provisional elements (finalised 2026-11-02, DECISION-009)

- **Book resource fields:** names, types, nullability and the full set are confirmed against the dataset. The full representation = every source column after cleaning rules R1–R9.
- **Book required fields:** `bookId, title, authors, category, price, publishYear`. The names map to the verified columns `Title, Authors, Category, Price Starting With ($), Publish Date (Year)`.
- **Example values:** every example in these files uses obvious placeholders (`"T1"`, `"A1"`). They are not data.

## 3. Implementation-level choices made in these contracts (for your review; not scientific decisions)

1. **Bare JSON arrays.** List responses are bare JSON arrays with no paging envelope, total count or links.
   - Reason: a total count would need an extra `COUNT` query on REST only, which would break the identical-internal-work design (DECISION-002/AP-2).
   - GraphQL list fields are bare lists too.
2. **Error formats.**
   - REST errors use RFC 9457 `application/problem+json`.
   - GraphQL errors follow the GraphQL over HTTP conventions for `application/json` (HTTP 200 with an `errors` array for execution errors).
   - Errors occur only on invalid input or failure, never on the measured success path.
3. **Validation limits:**
   - `limit` from 1 to 1000;
   - up to 1000 IDs per list;
   - IDs are positive 32-bit integers.

   They exist to reject malformed requests. The benchmark values are well inside them.
4. **The `Server` response header is disabled in both stacks** (Kestrel `AddServerHeader = false`). This is symmetric security hygiene. It also keeps the bandwidth metric free of an identical, irrelevant header.
5. **Unknown IDs in `?ids=` / `?memberIds=` are omitted, not errors** (filter semantics). The benchmark assertions verify that the counts are complete.
