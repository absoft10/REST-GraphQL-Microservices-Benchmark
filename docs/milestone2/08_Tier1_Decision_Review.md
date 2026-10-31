# 08 — Tier 1 Decision Review (pre-freeze)

| | |
|---|---|
| Date | 2026-11-01 |
| Status | **Answered by the researcher on 2026-11-01.** O-5 was approved with modification and O-7 in principle; all others were approved as recommended. Recorded in DECISION-002 … DECISION-008. The O-5 row below is superseded by DECISION-005. |
| Input | Researcher approval of 2026-11-01 ("Phase 0 Approval and Implementation Preparation"), approved directions **1–10**, referred to here as **[AP-1]…[AP-10]** |
| Purpose | Check each of the 22 Tier 1 items in doc 06 §1 against AP-1…AP-10. Propose exact values for everything that is not covered. Nothing here is treated as approved. |

## 1. Coverage of the 22 Tier 1 items

Legend:
- **Covered:** fully decided by an approval.
- **Covered + detail:** decided in principle; the detail listed is an implementation detail with no effect on what is measured.
- **Outstanding:** needs your explicit approval. The item number refers to §4.

| # | ID | Item | Status | Basis / what remains |
|---|---|---|---|---|
| 1 | AD-01 | Topology, Option A | **Covered** | AP-2 |
| 2 | AD-25 | Pushdown excluded and documented | **Covered** | AP-8. The scope text of doc 01 §1.1 is the documentation. |
| 3 | D-2 | Both nested REST variants | **Covered** | AP-4 (five configurations, 25 cells) |
| 4 | AD-07 | Batched 3-call chain | **Covered + detail** | AP-5. The ID-list wire format affects `sentBytes` and URL length → **O-9** |
| 5 | A-6 | Required field lists | **Outstanding** | → **O-1, O-2** |
| 6 | AD-34 | CRIR + JCS; raw-body Actual Bytes (CL-1) | **Outstanding** | AP-7 covers GraphQL selection, not the Required Bytes definition → **O-3** |
| 7 | AD-09 | Full REST representation | **Covered, but one material detail remains** | AP-7 approves "full" representations. *What* a full representation contains depends on which columns are stored, and that drives REST over-fetching → **O-4**. The lean-weight offline sensitivity (option c) is a Tier 4 reporting choice, not needed now. |
| 8 | AD-10 | Composite embeds full representations | **Covered** | Follows from AP-7 |
| 9 | AD-32 | Serializer policy | **Covered in principle; values outstanding** | AP-3 asks for "documented serializer settings". The exact escaping, null and number policy changes the bytes → **O-5** |
| 10 | AD-04 | .NET 10 | **Covered** | AP-3 |
| 11 | AD-05 | Wire settings | **Partly covered** | AP-3 covers HTTP/1.1. Compression and TLS change bandwidth, CPU and latency → **O-6**. POST JSON and `Accept` headers are details. |
| 12 | AD-08 | Dataset source | **Partly covered** | AP-6 covers determinism. The source, its columns and the licence still need confirming → **O-7** (it also gates O-1 and O-4) |
| 13 | AD-11 | Database per service | **Covered** | AP-6 |
| 14 | AD-36 | Data-freeze verification | **Covered + detail** | AP-10 (reproducibility). The method is an implementation detail with no effect on what is measured. |
| 15 | AD-15 | Fixed request per configuration | **Covered** | This is M2 §2.3.1 as written. Its cache-hot nature is disclosed. Listed for awareness only. |
| 16 | AD-03 | Omit Notification and Auth services | **Outstanding** | It changes the system described in the paper (Fig. 1) and what the CPU scope covers → **O-10** |
| 17 | D-4a | Build the JWT toggle (off) | **Outstanding** | Scope (R2-4) → **O-11** |
| 18 | D-5 | No write workload | **Outstanding** | Scope (R2-4) → **O-11** |
| 19 | D-3 | Memory and DB instrumentation | **Covered** | AP-9 |
| 20 | AD-21 | `_dbq.csv` per-second aggregate (AM-2) | **Outstanding** | AP-9 approves the instrumentation, not the amendment to the C-3 format → **O-12** |
| 21 | AD-23 | REST controllers vs Minimal APIs | **Outstanding** | It affects REST-edge CPU and latency (RQ1, RQ2) → **O-13** |
| 22 | AD-14 | `benchmark/` + version control | **Covered + detail** | AP-1 and the Phase 0 instruction. *How* version control is set up safely → **O-14** |

The review also found two record-set choices. They are not separate rows in doc 06, but they define the workload semantics → **O-8**.

**Summary:** 10 items are covered, 4 are covered with an implementation detail, and **14 decisions (O-1 to O-14) are outstanding.**

---

## 2. Proposed A-6 required-field lists

### O-1 Flat workload: "a list of books with basic information"

| Field | Type | Nullable | Justification |
|---|---|---|---|
| `bookId` | integer | no | Primary key, needed for C-6 and the golden tests |
| `title` | string | no | The manuscript lists the dataset attributes as title, author information, genre, price and publication year (§III-A-1) |
| `authors` | string | no | "author information" |
| `category` | string | yes | "genre" |
| `price` | decimal | yes | "price" |
| `publishYear` | integer | yes | "publication year" |

**Not required, so returned only by REST:** `description`, `publisher`, `publishMonth`, if these columns exist (see O-7).

**Rationale.** The required set is exactly the attributes the paper itself names as the book information, plus the key. This choice comes from the manuscript, not from the expected result.

### O-2 Nested workload: "each member's loan history, with the borrowed book"

| Level | Field | Type | Nullable | Justification |
|---|---|---|---|---|
| member | `memberId` | integer | no | Key (C-6) |
| member | `firstName`, `lastName` | string | no | Identifies the borrower |
| loan | `loanId` | integer | no | Key (C-6) |
| loan | `loanDate` | date | no | When it was borrowed |
| loan | `returnDate` | date | **yes** | Return status. `null` means not yet returned, which exercises null semantics. |
| book | `bookId` | integer | no | Key (C-6) |
| book | `title`, `authors` | string | no | Identifies the book |

**Not required, so returned only by REST:**
- member: `email`, `phoneNumber`, `address`;
- loan: `memberId`, `bookId`, `isReturned`;
- book: `category`, `price`, `publishYear`, `description`, `publisher`, `publishMonth`.

`isReturned` is excluded because it duplicates `returnDate`'s null status. The linking IDs (`loan.memberId`, `loan.bookId`) are not required, because the CRIR conveys those relations by nesting. N-RM still needs them in transit, and the bytes it spends on them are a genuine multi-call cost.

---

## 3. Canonical required-information representation (CRIR)

Serialisation is RFC 8785 (JCS). Keys are sorted (by UTF-16 code units). Arrays are ordered by primary key. Dates are `YYYY-MM-DD`. Decimals use ES6 shortest form. Nulls are explicit. There is no whitespace.

**Values below are illustrative placeholders, not data.**

### 3.1 Flat (array of N books, `bookId` ascending)

Structure, pretty-printed for reading only:
```json
[
  { "authors": "<authors of book 1>", "bookId": 1, "category": "<category or null>",
    "price": 12.5, "publishYear": 2004, "title": "<title of book 1>" },
  { "authors": "...", "bookId": 2, "category": null, "price": null, "publishYear": 1998, "title": "..." }
]
```
Canonical bytes, which are what Required Bytes counts:
```
[{"authors":"A1","bookId":1,"category":"C1","price":12.5,"publishYear":2004,"title":"T1"},{"authors":"A2","bookId":2,"category":null,"price":null,"publishYear":1998,"title":"T2"}]
```

### 3.2 Nested (array of M members, `memberId` ascending; loans by `loanId`; one book per loan)

```json
[
  { "firstName": "<first>", "lastName": "<last>", "memberId": 1,
    "loans": [
      { "book": { "authors": "...", "bookId": 17, "title": "..." },
        "loanDate": "2026-03-02", "loanId": 101, "returnDate": "2026-03-20" },
      { "book": { "authors": "...", "bookId": 42, "title": "..." },
        "loanDate": "2026-05-11", "loanId": 102, "returnDate": null }
    ] }
]
```
In JCS the key order is `firstName, lastName, loans, memberId` and `book, loanDate, loanId, returnDate`. Sorting changes no byte counts.

### 3.3 Rules that make Required Bytes protocol-independent

1. `RequiredBytes(workload) = len(UTF-8(JCS(CRIR)))`, which is one number per workload.
2. The CRIR is built from the frozen dataset (the oracle). It must equal each configuration's **normalised** response:
   - GraphQL: strip `data`.
   - REST: project onto the A-6 fields.
   - N-RM: join the 3 bodies on `memberId` and `bookId`.
3. No envelope, call count or linking key ever enters Required Bytes. All of these enter Actual Bytes, as transmitted.
4. `ActualBytes = Σ raw response body lengths as transmitted` (headers excluded, compression off). Genuine differences remain: the envelope, extra REST fields, N-RM's repeated keys and array wrappers, and serializer formatting.

---

## 4. Outstanding Tier 1 decisions: recommended values

| ID | Decision | Recommended value | Why it matters / rationale | Affects | M2 |
|---|---|---|---|---|---|
| **O-1** | Flat A-6 list | `bookId, title, authors, category, price, publishYear` (§2) | Defines the GraphQL query, the CRIR and REST over-fetching. Taken from the manuscript's own description of a book. | RQ3, RQ1 | No |
| **O-2** | Nested A-6 list | member `memberId, firstName, lastName`; loan `loanId, loanDate, returnDate`; book `bookId, title, authors` (§2) | As above, for the nested chain. Includes a nullable field. | RQ3, RQ1 | No |
| **O-3** | Required Bytes definition (CL-1) | CRIR + JCS from the oracle; Actual = raw transmitted bodies; invariant `Actual ≥ Required`; decomposition reported (§3) | Makes Required Bytes independent of the protocol and the call count, answering R1-5. | RQ3 | **Clarif.** §2.3.3 |
| **O-4** | What "full representation" contains | **Books:** every column present in the source dataset after cleaning, with none dropped for size. **Members:** exactly the baseline `User` fields (`memberId, firstName, lastName, email, phoneNumber, address`). **Loans:** exactly the baseline `Loan` fields (`loanId, memberId, bookId, loanDate, returnDate, isReturned`). The previously proposed `joined_at` and `due_date` are dropped. | REST over-fetching is set by these columns. Taking them from the data source and the original system, not choosing them, removes researcher discretion. | RQ3, RQ2 | No |
| **O-5** | Serializer values (AD-32) | Both stacks:<br>• escaping `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` ("unsafe" refers only to embedding in HTML);<br>• camelCase names;<br>• nulls **written** (no ignore condition);<br>• `DateOnly` as `YYYY-MM-DD`;<br>• decimals as JSON numbers at the stored scale;<br>• no indentation.<br>HotChocolate's formatter is configured to match. If a setting cannot be matched (verify at implementation), REST adopts HotChocolate's behaviour for that setting, and it is recorded. | Default ASP.NET escaping (`\uXXXX` for non-ASCII and `' < &`) would inflate REST bytes for reasons unrelated to the protocol | RQ3 | No |
| **O-6** | Compression and TLS (AD-05 rest) | **No response compression and no TLS inside the VPC**, for both stacks. Both are disclosed as out of scope, consistent with the original §III-F exclusion of compression. | Compression would dominate bandwidth and add CPU. TLS adds symmetric overhead that is not under study. | RQ1–RQ3 | No |
| **O-7** | Dataset source and **pre-freeze verification** | Kaggle "Books dataset" [33] plus seeded synthetic members and loans. **Before the freeze, you download the source file** (Kaggle requires your account, which I cannot use) into `benchmark/data/source/`. I then verify the actual column names, the row count and the licence, and adjust the *names* in O-1 and O-4 to match the file, never the meaning, before freezing. | O-1 and O-4 assume columns that have not been verified | RQ3 | No |
| **O-8** | Record-set semantics | **Flat:** the first N books by `bookId` (offset 0). **Nested:** the first M members by `memberId`, each with exactly L loans on M·L distinct books. Within each member, loans alternate returned/unreturned by `loanId` (odd = returned with a date, even = `returnDate null`), so that nulls occur deterministically. N, M and L stay Tier 2 (placeholders 100, 10, 5). | It fixes the workload semantics and the null coverage before any code exists | RQ1–RQ3 | No |
| **O-9** | ID-list wire format | One comma-separated query parameter (`?memberIds=1,2,3`, `?ids=…`) for the N-RM calls and the internal calls alike. Sorted, de-duplicated. GraphQL is sent with variables and `operationName`. | It affects `sentBytes` and URL length, identically in all designs. Deciding it now avoids ad-hoc choices later. | RQ1 (minor) | No |
| **O-10** | Notification and Auth services | **Omit both from the measured system.** The paper's Fig. 1 is redrawn (Tier 4). | They play no role in reads. Including idle services would blur the CPU scope (R1-4). | RQ2, paper | No |
| **O-11** | Auth and writes scope | **D-4a:** build symmetric JWT validation, off by default. Whether to run it is decided in Tier 3 (reduced set at 100 and 1000). **D-5:** no write workload. The paper states that writes are excluded because they would conflict with the data freeze and the no-restart rule. | Scoping answer to R2-4 | Scope | No |
| **O-12** | `_dbq.csv` format (AM-2) | Per-second aggregate per `query_id`: `timestamp_utc, service, query_id, count, mean_ms, p95_ms, max_ms` | Per-command rows at level 1000 would perturb the system | RQ2 (DB attribution) | **Amend.** C-3 |
| **O-13** | REST edge style | **`[ApiController]` controllers.** Disclosed as the REST implementation style. | Continuity with the original system and common enterprise practice. Minimal APIs would lower REST-edge overhead, so the choice is stated rather than optimised. | RQ1, RQ2 | No |
| **O-14** | Version-control setup | `git init` at the workspace root. `.gitignore` excludes `existing-source/` (its own `.git` is untouched), `research-documents/` (they contain confidential review correspondence; preserved by SHA-256 manifest instead), `experiment/raw/`, `experiment/pilot/`, `benchmark/data/source/`, local development-tool configuration and secret patterns. No remote is added and nothing is pushed. | Preserves the originals bit-for-bit. Keeps the confidential review out of any future public repository. | Reproducibility | No |

## 5. What happens after your approval (Phase 0, no application code)

1. Write `DECISION-002…` records for:
   - AP-1…AP-10;
   - every approved O-item;
   - the covered items, citing their approval.
2. Update docs 01, 03, 04 and 06 to reflect the approved values. In doc 06, mark the items as approved with their record IDs.
3. Version control as specified in O-14. Commit only the workspace's own files (`docs/`; local tool configuration stays untracked).
4. Baseline fingerprint:
   - `docs/milestone2/baseline_manifest.txt`, from `git ls-tree -r ff9b302`, plus confirmation that `git status --porcelain` is empty;
   - `research-documents.sha256`, covering the four original files.
5. Freeze the protocol: `docs/milestone2/PROTOCOL_FREEZE.md`, listing every frozen decision and every item still open for Tier 2/3.
6. Write the Phase 1 readiness checklist.

**Pre-freeze dependency:** O-7. If you approve O-7 as recommended, the freeze waits for the dataset file, so that the A-6 column names can be verified. Alternatively, you can approve freezing with the names marked "provisional until O-7 verification".
