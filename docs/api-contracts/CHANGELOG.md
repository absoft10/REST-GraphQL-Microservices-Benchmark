# API Contract Change Log

## v0.2-provisional (2026-11-01): findings from implementation (Tasks 4–6)

Each entry below was found while implementing the contract and testing it on LocalDB. **None changes an approved scientific decision.** Entries marked ⚠ need a researcher decision.

| # | Change or finding | Kind | Impact | Status |
|---|---|---|---|---|
| C-1 | ⚠ **HotChocolate 16 limits concurrent GraphQL executions to 64 by default** (`GraphQLServerOptions.MaxConcurrentExecutions`). The REST stack has no equivalent limiter. | Finding (library default) | At high concurrency this throttles **GraphQL only**, a protocol-specific admission limit that confounds latency and throughput at levels 100–1000. It is now explicit configuration `GraphQL:MaxConcurrentExecutions` (0 = unlimited), left at the library default. | **Open: new decision AD-37 (Tier 2)** |
| C-2 | HotChocolate adds the response header **`Vary: Accept`** to GraphQL responses. errors-and-pagination.md §5 said `Vary` is not sent. | Contract correction | Adds about 14 header bytes to the JMeter `bytes` (bandwidth) of F-G and N-G. Over-fetching is unaffected (headers are excluded). It is **not stripped**: removing a library header only on one stack would be per-protocol tuning. It is recorded and disclosed instead. | Documented. The researcher may override. |
| C-3 | Response framing **observed**: REST responses use `Transfer-Encoding: chunked`; GraphQL responses carry `Content-Length`. | Recorded, as the contract requires ("recorded, not forced") | Only the header part of JMeter `bytes` differs (a few bytes). Bodies are identical in meaning. | Recorded in `samples/local-fixture/*.exchange.txt` |
| C-4 | GraphQL parse errors return **HTTP 200** with `errors[].extensions.code = "HC0011"` (Accept: application/json). GET `/graphql` falls through to the **404 problem+json** fallback. | Behaviour pinned (the contract allowed "200 or 400, recorded") | None for success-path measurements. JMeter treats both as failures. | Recorded |
| C-5 | EF Core 10 translates ID-list `Contains` to **one scalar parameter per ID** (`IN (@ids1, …)`), not `OPENJSON`. | Recorded (doc 01 §5 required capture) | The SQL is identical across N-RC, N-G and N-RM, because all three use the same domain code with the same sorted ID lists. See [captured-sql.md](captured-sql.md). | Recorded |
| C-6 | The Required Bytes implementation is in **C# (`benchmark/src/Lms.RequiredBytes`)** instead of the planned Python module. There is still exactly one implementation, used by the `lms-bench equivalence` tool and by the tests. The analysis pipeline will consume its `bytes_summary.csv`. | Implementation-location change (required-bytes-spec.md header) | None on the algorithm. The RFC 8785 number domain is restricted to ≤ 15 significant digits in [1e-6, 1e21), which covers all CRIR values (integers and DECIMAL(10,2)). Values outside it are rejected, never approximated. | Documented |
| C-7 | Index `ix_loan_member` keyed on **(member_id, loan_id)** (doc 03 had `member_id` only), to support `ORDER BY member_id, loan_id` directly. | DDL refinement | Same for all configurations, because the Loans service is shared | Applied in `30_loans.sql` |
| C-8 | `loans.loan` has a **CHECK constraint**: `is_returned = 1 ⇔ return_date IS NOT NULL` | DDL hardening | Guarantees the O-8 null semantics at the database level | Applied |
| C-9 | The contract scalar names `Decimal` and `Date` are verified on the live schema. `DateOnly` is bound explicitly to `Date` and serialises as `"YYYY-MM-DD"`. Decimals keep the stored scale (`12.50`) in **both** stacks. | Verification of DECISION-005 rules 5–6 | The formatting delta is identical for REST and GraphQL (flat 767 B, nested 289 B on the fixture), so the serializers emit byte-identical scalar tokens | Verified by tests |
| C-10 | Request batching **disabled** (`AllowedBatching.None`). HotChocolate 16 enables variable batching by default. | Contract requirement applied (graphql-api.md §1) | — | Applied, tested |
| C-11 | Problem bodies contain exactly `type, title, status, detail, instance`. ASP.NET's default `traceId` and `errors` extensions are not emitted. | Implementation detail matching the contract | — | Tested |

## v0.1-provisional (2026-11-01)

The initial contract set (Task 3).
