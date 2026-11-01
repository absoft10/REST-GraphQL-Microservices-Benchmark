# Internal Domain API (protocol-neutral; used by both edges)

| | |
|---|---|
| Services | `Lms.Catalog.Service` (books), `Lms.Members.Service`, `Lms.Loans.Service` |
| Callers | `Lms.Edge.Rest` and `Lms.Edge.GraphQL`, **only** through the shared `Lms.DomainClient` (DECISION-002/AP-2) |
| Transport | HTTP/1.1 keep-alive, JSON, SP-1 serialization, no compression. One pooled `SocketsHttpHandler` per edge; settings recorded (AD-33, Tier 3 values). |
| Projection | **A fixed full-row contract.** No field selection is passed down (AP-8). Each endpoint always returns the full representation. |
| Shared rules | [errors-and-pagination.md](errors-and-pagination.md) |

## 1. Endpoints

| Service | Request | Response | SQL shape (EF Core, `AsNoTracking`, fixed projection) |
|---|---|---|---|
| Catalog | `GET /internal/books?offset={o}&limit={l}` | Book[] by `bookId` | `SELECT <all book columns> FROM catalog.book ORDER BY book_id OFFSET @o ROWS FETCH NEXT @l ROWS ONLY` |
| Catalog | `GET /internal/books?ids={sorted,unique}` | Book[] by `bookId` | `… WHERE book_id IN (<ids>) ORDER BY book_id` |
| Members | `GET /internal/members?offset={o}&limit={l}` | Member[] by `memberId` | `… FROM members.member ORDER BY member_id OFFSET … FETCH …` |
| Loans | `GET /internal/loans?memberIds={sorted,unique}` | Loan[] by (`memberId`, `loanId`) | `… FROM loans.loan WHERE member_id IN (<ids>) ORDER BY member_id, loan_id` (index `ix_loan_member`) |
| all | `GET /health` | 200 | none |

The representations are identical to the REST public representations (rest-api.md §1). Book is final (DECISION-009).

**`IN`-list translation:**
- The EF Core `Contains` translation mode and the database compatibility level are pinned, and the generated SQL is captured once per query and committed (doc 01 §5).
- Each query is tagged `TagWith("q:<query_id>")` for `_dbq.csv` (DECISION-007). The query IDs are `catalog.books.page`, `catalog.books.ids`, `members.page` and `loans.byMembers`.

## 2. Calls per task (invariant; asserted in tests)

| Config | Internal calls |
|---|---|
| F-R, F-G | `catalog.books.page` × 1 |
| N-RC, N-G, N-RM | `members.page` × 1, then `loans.byMembers` × 1, then `catalog.books.ids` × 1, with identical parameters across the three configurations |

## 3. Errors

The domain services return problem+json (400/404/500) under the same rules. The edges map timeouts, connection failures and 5xx to `UPSTREAM_FAILURE` (REST 502 / GraphQL field error). `Lms.DomainClient` performs **no retries**: retries would hide failures and add load asymmetrically.
