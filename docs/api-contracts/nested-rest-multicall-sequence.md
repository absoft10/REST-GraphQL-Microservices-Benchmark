# N-RM: Three-Call Nested REST Sequence

| | |
|---|---|
| Decisions | AP-5 (batched 3-call chain), O-9 (ID-list format), DECISION-003 (Required Bytes). The timing definition is AD-28/CL-4, **Tier 2: recommended, not yet approved**. |
| Endpoints | [rest-api.md](rest-api.md) §2.2–§2.4 |
| Task | One complete chain = one task. The task fails if any call fails (M2 §2.4). |

## 1. Sequence

| Step | Request | Assertions | Extraction for the next step |
|---|---|---|---|
| 1 | `GET /api/members?offset=0&limit={M}` | Status 2xx. JSON array length = **M**. | `memberIds` = every `$[*].memberId`, joined with `,` in **ascending** order (the response is already ascending, so the order is kept). Default sentinel `__NONE__`. |
| 1a | — | `memberIds` ≠ `__NONE__` | — |
| 2 | `GET /api/loans?memberIds=${memberIds}` | Status 2xx. Array length = **M·L**. | `bookIds` = every `$[*].bookId`, **sorted ascending and de-duplicated** (see §2), joined with `,`. Default `__NONE__`. |
| 2a | — | `bookIds` ≠ `__NONE__` | — |
| 3 | `GET /api/books?ids=${bookIds}` | Status 2xx. Array length = **M·L** (books are distinct by O-8). | — |

Expected counts come from the golden files, never typed by hand (doc 03 §4.5). The headers are as in errors-and-pagination.md §5.

**Wire equality with N-RC and N-G.** For each call, the edge forwards to the internal API with the same parameters:
- `/internal/members?offset=0&limit=M`;
- `/internal/loans?memberIds=…`;
- `/internal/books?ids=…`.

The internal ID lists are therefore byte-identical to those that N-RC and N-G produce. This is verified by the internal-call URL log (doc 04 §4.4).

## 2. ID-list construction (bookIds)

The loans response is ordered by (`memberId`, `loanId`), so its `bookId` values are **not** sorted. The approved format (O-9) requires sorted, de-duplicated IDs. The plan therefore:
1. extracts all `bookId` values with a JSON Extractor (`$[*].bookId`, match number −1);
2. sorts and joins them with **one** JSR223 PreProcessor on step 3. It runs in Groovy, compiled and cached, with a script whose hash is recorded. It is the only script in the plan.

Alternative: the edge accepts unsorted lists and sorts them server-side (errors-and-pagination §3). The **client** would then send an unsorted URL to `/api/books`, which still satisfies O-9 server-side, but the public URL bytes would differ from a sorted list. **Recommended: keep the client sorting** so that the transmitted request is canonical. The JSR223 cost falls inside the task time under AD-28.

## 3. JMeter structure

```
Transaction Controller "N-RM"  (Generate parent sample = off; Include duration of timer and pre-post processors = ON ⟨AD-28, Tier 2⟩)
 ├─ HTTP Request "N-RM-1 members"  GET /api/members?offset=0&limit=${M}
 │    ├─ JSON Assertion  $.length() == ${M}          (exact JSON Assertion form verified at implementation)
 │    ├─ JSON Extractor  memberIds  $[*].memberId  match -1, concatenation var, default __NONE__
 │    └─ Response Assertion  ${memberIds_ALL} != __NONE__
 ├─ HTTP Request "N-RM-2 loans"    GET /api/loans?memberIds=${memberIds_ALL}
 │    ├─ JSON Assertion  $.length() == ${ML}
 │    └─ JSON Extractor  bookIdsRaw $[*].bookId  match -1, concatenation var, default __NONE__
 └─ HTTP Request "N-RM-3 books"    GET /api/books?ids=${bookIds}
      ├─ JSR223 PreProcessor  bookIds = sort-unique(bookIdsRaw_ALL)  (fails the sample if __NONE__)
      └─ JSON Assertion  $.length() == ${ML}
Thread Group: on sample error → Start Next Thread Loop
```

## 4. Measurement (M2 §2.3.4, §2.5)

- **Task time:** the transaction row's `elapsed`, from the start of call 1 to the end of call 3. Extraction and the step-3 preprocessor are included if AD-28 is approved (Tier 2). This is validated by T-TX-1 to T-TX-7 (doc 04 §1.1).
- **Per-call metrics:** the child rows (labels `N-RM-1/2/3`).
- **Actual Bytes (over-fetching):** Σ of the three raw response bodies ([required-bytes-spec.md](required-bytes-spec.md) §4).
- **Bandwidth:** the JMeter `bytes`. The rule for transaction vs Σ child rows is fixed by T-TX-6.
- **Call count:** 3, recorded per task (M2 §2.3.4).

## 5. Normalisation to the CRIR (for equivalence and Required Bytes)

1. Take members from body 1.
2. For each member, take the loans from body 2 where `memberId` matches, ordered by `loanId`.
3. For each loan, take the book from body 3 where `bookId` matches.
4. Project to the A-6 fields, then emit JCS.

The result must equal the oracle CRIR.
