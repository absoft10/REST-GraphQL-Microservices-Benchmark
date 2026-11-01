# Canonical Required Bytes Specification

| | |
|---|---|
| Decisions | DECISION-003 (O-1, O-2, O-3 / CL-1), DECISION-005 (SP-1), DECISION-004 (O-8) |
| Status | The algorithm is **approved**. Book field names are final (DECISION-009). |
| Implementation | One C# library `benchmark/src/Lms.RequiredBytes` (CHANGELOG C-6; originally planned as a Python module). It is used by `lms-bench equivalence` and the tests. The analysis consumes its `bytes_summary.csv`. It is never re-implemented elsewhere. |

## 1. Definitions

| Term | Definition |
|---|---|
| **CRIR(w)** | Canonical Required-Information Representation of workload *w* ∈ {flat, nested}. A JSON value that contains exactly the A-6 required information of the workload's record set. Its structure is in §2; JSON Schemas are in `crir-*.schema.json`. |
| **JCS(x)** | The RFC 8785 JSON Canonicalization Scheme serialisation of x. Key properties: UTF-8, no insignificant whitespace, object keys sorted by UTF-16 code units, minimal string escaping, ES6 number formatting. |
| **RequiredBytes(w)** | `len(JCS(CRIR(w)))` in bytes. **One constant per workload.** It does not depend on any protocol, envelope or call count. |
| **ActualBytes(c)** | For configuration *c*: Σ over the task's HTTP responses of the **raw transmitted body length** in bytes. Headers are excluded. Compression is off, so transmitted = decoded. N-RM sums 3 bodies; all other configurations have 1. |
| **OverFetch(c)** | `(ActualBytes(c) − RequiredBytes(w)) / ActualBytes(c)`. A per-configuration constant (M2 §2.3.3). |

RequiredBytes and ActualBytes are computed **independently**. Neither is derived from the other (DECISION-003 item 3).

## 2. CRIR structure

### 2.1 Flat

The value is a JSON array of N objects, ordered by `bookId` ascending. Each object has exactly these keys:

| Key | Type | Null allowed |
|---|---|---|
| `bookId` | integer | no |
| `title` | string | no |
| `authors` | string | no |
| `category` | string | yes |
| `price` | number | yes |
| `publishYear` | integer | yes |

### 2.2 Nested

The value is a JSON array of M member objects, ordered by `memberId`:

| Level | Keys |
|---|---|
| member | `memberId` (int), `firstName` (string), `lastName` (string), `loans` (array, ordered by `loanId`, exactly L items) |
| loan | `loanId` (int), `loanDate` (`"YYYY-MM-DD"`), `returnDate` (`"YYYY-MM-DD"` or `null`), `book` (object) |
| book | `bookId` (int), `title` (string), `authors` (string) |

The linking keys (`loan.memberId`, `loan.bookId`) are **not** in the CRIR. The relationships are expressed by nesting.

### 2.3 Value rules

1. **Strings:** the stored value as loaded (Unicode NFC applied at load time by the generator). No trimming at CRIR time.
2. **Dates:** `YYYY-MM-DD`.
3. **Decimals:** JSON numbers. Under JCS they are written in ES6 shortest form: `12.50` becomes `12.5`.
4. **Nulls:** every required nullable key is present with an explicit `null`. An absent key is invalid.
5. **Integers:** JSON integers.

## 3. Construction: oracle and per-configuration normalisers

**Oracle** (the authoritative CRIR) is built from the frozen dataset (generator output or a database export) with the O-8 record-set definition:
- flat: the first N books by `bookId`;
- nested: the first M members, with their L loans and the corresponding books.

The oracle is projected onto the A-6 fields and emitted as JCS. The output is committed as `benchmark/tests/golden/<w>.crir.json`, with its SHA-256 and `RequiredBytes`.

**Normalisers** map a configuration's raw response body or bodies to a CRIR:

| Config | Normaliser |
|---|---|
| F-R | `[project(b, FLAT_FIELDS) for b in body]` |
| F-G | `[project(b, FLAT_FIELDS) for b in body["data"]["books"]]`. A body with `errors` fails. |
| N-RC | For each member: project the member fields; `loans = [ {project(l, LOAN_FIELDS), book: project(l.book, BOOK_FIELDS)} ]` |
| N-G | The same as N-RC, applied to `body["data"]["members"]` |
| N-RM | Join body 1 (members), body 2 (loans by `memberId`, ordered by `loanId`) and body 3 (books by `bookId`), then project as for N-RC |

`project` copies the listed keys and fails if a key is absent. It never invents nulls.

**Equivalence condition** (DECISION-003 item 2): `JCS(normalise_c(bodies_c)) == JCS(oracle)`, byte for byte, for every configuration c.

## 4. Over-fetching decomposition (reported per configuration)

```
ActualBytes − RequiredBytes = envelope + non-required fields + formatting delta + multi-call overhead
```

| Term | Computed as |
|---|---|
| envelope | Bytes of the GraphQL wrapper `{"data":{"<field>":` … `}}`. 0 for REST. |
| non-required fields | Bytes of `,"key":value` members that are not in A-6 (REST full representations) |
| formatting delta | The service's serialisation of the required values minus their JCS form (for example `12.50` vs `12.5`). Whitespace is 0, because bodies must be compact. |
| multi-call overhead | N-RM only: repeated linking keys, per-call array brackets, separators |

The four terms must sum exactly to `ActualBytes − RequiredBytes`. A unit test enforces this.

## 5. Worked example (placeholder values; not data)

The numbers were computed with a script, not written by hand. The example uses a 2-book flat set.

| Quantity | Value | Composition |
|---|---|---|
| RequiredBytes(flat) | **179** | `[{"authors":"A1","bookId":1,"category":"C1","price":12.5,"publishYear":2004,"title":"T1"},{"authors":"A2","bookId":2,"category":null,"price":null,"publishYear":1998,"title":"T2"}]` |
| ActualBytes(F-G) | **199** | 179 + envelope 19 (`{"data":{"books":` = 17, closing `}}` = 2) + formatting delta 1 (`12.50`). Key order differs from JCS but does not change length. |
| OverFetch(F-G) | 20 / 199 = **10.05 %** | — |
| ActualBytes(F-R) | **292** | 179 + non-required fields 112 (`description`, `publisher`, `publishMonth`) + formatting delta 1 |
| OverFetch(F-R) | 113 / 292 = **38.70 %** | — |
| RequiredBytes(nested), example with 1 member and 2 loans | **283** | `[{"firstName":"F1","lastName":"L1","loans":[{"book":{"authors":"A17","bookId":17,"title":"T17"},"loanDate":"2026-03-02","loanId":101,"returnDate":"2026-03-20"},{"book":{"authors":"A42","bookId":42,"title":"T42"},"loanDate":"2026-05-11","loanId":102,"returnDate":null}],"memberId":1}]` |

The example shows that the GraphQL envelope and decimal formatting are genuine, reported differences. Neither is normalised away.

## 6. Reproducibility requirements and tests

- **JCS implementation.** RFC 8785-conformant: a pinned library, or an in-house implementation that passes the RFC 8785 test vectors. Python's `json.dumps(sort_keys=True)` is **not** sufficient, because its number formatting and control-character escapes differ.
- **Tests (Phase 5 gate, doc 03 §4.4):**
  - the oracle is reproduced from the generator twice, byte-identical;
  - each normaliser is tested against golden bodies;
  - the decomposition sum is exact;
  - `ActualBytes ≥ RequiredBytes`;
  - the null, ordering, count, ID and compactness checks pass.
- **Recorded per workload** in `experiment/equivalence/bytes_summary.csv`:
  - `workload, config, required_bytes, actual_bytes, overfetch, envelope, nonrequired, formatting, multicall, calls`;
  - plus the golden-file SHA-256.
