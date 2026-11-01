# 03 — Workload and Data Model Design

| | |
|---|---|
| Status | **DRAFT for researcher approval** |
| Date | 2026-11-01 |
| Confirmed requirements | M2 §2.1 (workloads and configurations), §2.3 (equivalence, Required Bytes), §2.4 (task definition), §2.5 (assertions) |
| Open | A-6 (required fields), AD-07…AD-11, and all numeric sizes marked *placeholder* |

> **Approval status (Phase 0, 2026-11-01).**
> - Required fields, the CRIR and Required Bytes: DECISION-003.
> - Representations, dataset and record sets: DECISION-004.
> - Serialization: DECISION-005.
> - Wire and ID format: DECISION-006.
>
> **Every element that depends on the book dataset is PROVISIONAL** until the downloaded file has been verified (O-7). That covers the `book` DDL, the book field names, cleaning, volumes and golden files. The member and loan schemas below are approved. See `PROTOCOL_STATUS.md`.

All numeric values below (page sizes, member and loan counts, row counts) are **placeholders** that illustrate the design. They are not decisions. They become fixed only through `docs/decisions/`.

---

## 1. Workload definitions

### 1.1 Flat retrieval [REQ M2 §2.1]

**Task:** retrieve one fixed page of books.

| Element | Design |
|---|---|
| Record set | `N` books, ordered by `book_id` ascending, at a fixed offset `O`. *Placeholder:* N = 100, O = 0. |
| Configurations | F-R, F-G |
| Count invariant | Exactly N records at every concurrency level [REQ M2 §2.3.1] |
| Equivalence keys | The ordered list of `book_id` values (C-6) |

The paper describes the flat workload as "getting books lists, basic information on the book". A fixed page of a list endpoint is the closest reproducible form of that description.

**Limitation on the research dataset (DS-9, DECISION-009).** The first 100 books in file order have 59.0% missing descriptions, against 31.9% across all 103,063 books. The flat record set is kept as approved and the limitation is disclosed. Changing record selection or pagination needs researcher authorisation.

### 1.2 Nested retrieval [REQ M2 §2.1]

**Task:** retrieve a fixed page of members, each member's loans, and the book of each loan. The chain is **member → loans → book**. It uses the three entities named in the manuscript (§III-A-2: "users, loans, and books") and crosses all three domain services.

| Element | Design |
|---|---|
| Record set | `M` members (fixed offset, ordered by `member_id`). Each has **exactly** `L` loans in the generated dataset. The M·L loans reference M·L books that are **distinct across the whole target set**, so no de-duplication can change a count. *Placeholder:* M = 10, L = 5, giving 10 members, 50 loans and 50 books. The generator enforces this, and the integrity check verifies it. |
| Configurations | N-RC, N-RM, N-G |
| Count invariant | M members, M·L loans and M·L books, fixed by the data generator, so the count cannot change with concurrency |
| Equivalence keys | Ordered `member_id`; `(member_id, loan_id)` pairs; `(loan_id, book_id)` pairs |
| Same chain everywhere | [REQ M2 §2.3.4] Every configuration traverses members → loans (by member) → books (by loan) |

Two properties are chosen deliberately. Each loan has a distinct book, so nothing is deduplicated. Every target member has the same L, so record counts are exactly predictable for the JMeter assertions.

**Fixed request versus rotating IDs [DECISION AD-15].** M2 §2.3.1 and §2.3.5 imply one fixed request per configuration: the same keys every time. This means every task reads the same rows, so the SQL Server buffer pool and plan cache are always warm. That holds equally for all configurations, so it is fair, but it tests a cache-hot read path.
- Recommended: keep the fixed request exactly as M2 specifies, and disclose that it is cache-hot.
- Alternative (a protocol change, researcher only): rotate through a fixed list of K pages using a JMeter CSV Data Set, with an equivalence check per page.

---

## 2. Data model [PROPOSED]

### 2.1 Storage layout [DECISION AD-11]

There is one RDS SQL Server instance with **one database per domain service**: `lms_catalog`, `lms_members` and `lms_loans`. Each service has a login that can read only its own database. This is the classic database-per-service microservice pattern, as in the baseline.

| Option | Assessment |
|---|---|
| **Database per service (recommended)** | Faithful to microservices. No cross-service foreign keys, so referential integrity is enforced by the generator and verified by a check script. |
| Schema per service in one database | Equivalent for the benchmark. Allows foreign keys, but blurs service ownership. |
| One shared schema | Contradicts the microservice framing |

### 2.2 Tables

```sql
-- lms_catalog
CREATE TABLE catalog.book (
  book_id        INT            NOT NULL PRIMARY KEY,
  title          NVARCHAR(400)  NOT NULL,
  authors        NVARCHAR(400)  NOT NULL,
  description    NVARCHAR(MAX)  NULL,      -- present in the source dataset (verify on download)
  category       NVARCHAR(200)  NULL,
  publisher      NVARCHAR(200)  NULL,
  price          DECIMAL(10,2)  NULL,
  publish_month  NVARCHAR(20)   NULL,
  publish_year   SMALLINT       NULL
);
-- lms_members
CREATE TABLE members.member (
  member_id  INT NOT NULL PRIMARY KEY,
  first_name NVARCHAR(100) NOT NULL, last_name NVARCHAR(100) NOT NULL,
  email      NVARCHAR(200) NOT NULL, phone_number NVARCHAR(40) NOT NULL, address NVARCHAR(300) NOT NULL
);                                       -- exactly the baseline User fields (DECISION-004, O-4)
-- lms_loans
CREATE TABLE loans.loan (
  loan_id     INT NOT NULL PRIMARY KEY,
  member_id   INT NOT NULL,              -- logical reference to members.member
  book_id     INT NOT NULL,              -- logical reference to catalog.book
  loan_date   DATE NOT NULL, return_date DATE NULL,
  is_returned BIT  NOT NULL               -- exactly the baseline Loan fields (DECISION-004, O-4)
);
CREATE INDEX ix_loan_member ON loans.loan(member_id) INCLUDE (book_id, loan_date, return_date, is_returned);
```

Column names follow the attributes the manuscript gives for dataset [33]: title, author information, genre, price and publication year. The **exact columns must be verified against the downloaded file** (A-4). Nothing here asserts the dataset's schema.

### 2.3 Dataset and generator [DECISION AD-08]

| Table | Source | Size (*placeholder*) |
|---|---|---|
| `book` | Kaggle "Books dataset" [33], cleaned deterministically: trim, dedupe, assign `book_id` by file order | The manuscript says "more than 103,000"; the exact count is recorded in A-4 |
| `member` | Synthetic. A deterministic generator with a recorded seed (for example .NET `Bogus` with a fixed seed). No real personal data. | 10,000 |
| `loan` | Synthetic. Target members get exactly L loans with distinct books. Other members get a seeded random 0–20 loans. | ~100,000 |

- The generator writes CSV files plus a `dataset_manifest.json`. The manifest records:
  - the seed;
  - the SHA-256 and download date of the Kaggle source file;
  - the pinned generator library versions;
  - the row counts;
  - the SHA-256 of each output file.

  The files are loaded with `bcp` or `SqlBulkCopy`. Byte-identical output is tested on two operating systems, Windows and the Linux build image.
- An integrity script checks that every `loan.member_id` and `loan.book_id` resolves and that the target-set invariants hold.
- The data freeze is verified with content hashes (§2.4), not with SQL `CHECKSUM_AGG`. `CHECKSUM_AGG` is a 32-bit, collision-prone aggregate and is not a proof of unchanged data.
- **Open:** the dataset licence and redistribution terms for the replication package. The researcher must check these on Kaggle before the data is published.

### 2.4 Data-freeze verification [DECISION AD-36]

The tool is `benchmark/data/verify_data_freeze` (Python, pinned). It connects read-only and emits `data_freeze_<label>.json`.

| Check | Method |
|---|---|
| **Content hash per table** | Stream `SELECT <columns in DDL order> FROM <table> ORDER BY <pk>` into a canonical row encoding, then SHA-256 it. The encoding:<br>• one line per row, UTF-8, fields separated by `\x1F`;<br>• `NULL` written as `\x00`;<br>• strings NFC as stored;<br>• `DATE` as `YYYY-MM-DD`, `DATETIME2` as ISO 8601 with fixed precision;<br>• decimals in invariant culture at the column's scale;<br>• `BIT` as `0`/`1`. |
| **Generator match** | The generator emits the same canonical encoding from its in-memory rows. The DB hash must **equal** the generator hash in `dataset_manifest.json`. This proves that what is in the database is exactly what was generated, not just that it is unchanged. |
| **Row counts** | `COUNT_BIG(*)` per table, compared with the manifest (A-4) |
| **Referential integrity** | Orphan counts must be 0: loans whose `member_id` is not in members, and loans whose `book_id` is not in books. Integrity is checked across databases by exporting key sets, because no cross-database foreign keys exist. |
| **Target-set invariants** | Exactly M target members. Each has exactly L loans. The M·L book IDs are distinct and present. |
| **Schema fingerprint** | A hash of `INFORMATION_SCHEMA.COLUMNS` and the index definitions, plus `@@VERSION`, collation and compatibility level |

**When it runs:**
- after the load (the **freeze record**, committed to git);
- at the start of every campaign day;
- after the last run.

Any difference stops the campaign. **Why not `CHECKSUM_AGG`:** it is a 32-bit aggregate that can cancel or collide, and it depends on the engine. It cannot serve as a reproducible proof. The procedure is reproducible because anyone with the generator, the seed and the tool can recompute every hash.

---

## 3. API contracts

### 3.1 REST resource representations [DECISION AD-09]

REST returns the **full resource representation**: every column of the entity, in camelCase JSON. This is the conventional REST behaviour, and it is the over-fetching mechanism the RQ3 study examines.

How heavy a "full representation" is directly sets the REST over-fetching level and part of REST CPU. Examples are whether it includes `description` or the member's personal fields. The over-fetching result is therefore partly a **design artefact**, and the paper must say so. The options are:
- **(a) All stored columns (recommended).** Simple, defensible and documented in the workload table.
- **(b) A lean representation.** This reduces REST over-fetching by a design choice, not by protocol.
- **(c) Report both weights.** Compute over-fetching offline from sample bodies for (a) and (b). This needs no extra runs if only (a) is load-tested.

Whichever is chosen is fixed *before* any data is seen, so that the comparison is not tuned.

### 3.2 Flat

| | Request | Response body (shape) |
|---|---|---|
| F-R | `GET /api/books?offset=0&limit=100` | `[ {bookId,title,authors,description,category,publisher,price,publishMonth,publishYear}, … ]` |
| F-G | `POST /graphql`, JSON body `{"query":"query FlatBooks($o:Int!,$l:Int!){ books(offset:$o, limit:$l){ <A-6 fields> } }","operationName":"FlatBooks","variables":{"o":0,"l":100}}` | `{"data":{"books":[ {<A-6 fields>}, … ]}}` |

GraphQL is sent over `POST` with `application/json` [PROPOSED; AD-05 covers the GET alternative, which matters only if caching were in scope].

### 3.3 Nested: GraphQL (N-G)

```graphql
query NestedMembers($o: Int!, $l: Int!) {
  members(offset: $o, limit: $l) {
    memberId firstName lastName            # A-6 nested list (placeholder)
    loans { loanId loanDate returnDate
            book { bookId title authors } }
  }
}
```

The `Member.loans` resolver uses a **batch DataLoader** keyed by `member_id`, which makes one internal call: `GET /internal/loans?memberIds=…`. The `Loan.book` resolver uses a batch DataLoader keyed by `book_id`, which makes one internal call: `GET /internal/books?ids=…`. Without DataLoaders, GraphQL would issue M + M·L internal calls (the N+1 problem). That would be a defect in the implementation, not a protocol property.

### 3.4 Nested: REST composite (N-RC) [DECISION AD-10]

`GET /api/members-with-loans?offset=0&limit=10` returns the members, each with an embedded `loans` array, each loan with an embedded `book`.

The edge handler performs the same three internal calls as N-G, in the same order.

| Option for the embedded content | Effect |
|---|---|
| **Full representations embedded (recommended)** | A consistent resource model, with over-fetching behaving like REST's flat case |
| Use-case-tailored DTO (Backend-for-Frontend) | Near-zero over-fetching. This is a different design pattern from "REST composite" and would need its own justification. |

### 3.5 Nested: REST multi-call (N-RM) [DECISION AD-07]

| Option | Chain | Client round trips | Internal calls | Assessment |
|---|---|---|---|---|
| (i) Batched resource chain | 1. `GET /api/members?offset=0&limit=10` → extract `memberId[]`. 2. `GET /api/loans?memberIds=1,2,…` → extract `bookId[]`. 3. `GET /api/books?ids=…` | **3** | 3 (same IDs as N-RC and N-G) | DB and internal work are identical, so the contrast is attributable. It measures only the **minimum** multi-call penalty: within one AZ the RTT is small. It needs non-standard batch filters (`?ids=`). |
| (ii) Per-resource chain | `GET /api/members?…`, then `GET /api/members/{id}/loans` × M, then `GET /api/books/{id}` × M·L | 1 + M + M·L (61 at the placeholder sizes) | 1 + M + M·L | Closest to the "classic" under-fetching that R2 point 3 describes. DB work is multiplied too, so it is confounded with database load. At level 1000 it is about a 61k-request burst. |
| (iii) Hybrid | `GET /api/members?…`, then `GET /api/members/{id}/loans` × M, then one batched `GET /api/books?ids=…` | 1 + M + 1 (12) | 1 + M + 1 | A per-parent fan-out without a per-leaf explosion. Moderate confounding. |

No option is labelled "fair" by default. (i) maximises attribution. (ii) maximises ecological realism for naive clients. **The researcher chooses [AD-07].** The paper then states the chosen option and the claim it supports. Recommended: (i) for the primary cells, with the scope statement from doc 01 §4. (ii) and (iii) remain options and would be extra cells, which is a protocol change.

**Client network latency [DECISION AD-27].** JMeter and the edge share an AZ, so the round-trip penalty is close to the lower bound. The options are:
- record the measured RTT and disclose it (recommended; no protocol change);
- emulate a WAN RTT on the generator with `tc netem`, for all configurations, as additional cells.

**Chain mechanics** (from the load-testing review; values are set in the plans):
- Each call asserts status, record count, and a non-empty ID list. JSON Extractor defaults are set to a sentinel and asserted against [REQ M2 §2.5].
- If any call fails, the Thread Group goes to "Start Next Thread Loop". The rest of the chain does not run with empty IDs, and the transaction counts as failed.
- **Extraction time is part of task time [DECISION AD-28; recommended: include].** M2 §2.3.4 defines N-RM latency as "the time from the first request of the chain to the last response". The ID extraction between calls falls inside that interval, so including it is the **literal M2 reading** and not an amendment. The Transaction Controller is set to "Include duration of timer and pre-post processors in generated sample" = **on**. With the default (off), JMeter would subtract processor time and under-report the task. The exact measurement and its validation are in doc 04 §1.1.

---

## 4. Canonical required information, Required Bytes and functional equivalence

### 4.1 A-6 field lists (placeholders, **researcher must supply or confirm**)

| Workload | Proposed required fields | Note |
|---|---|---|
| Flat | `bookId, title, authors, category, price, publishYear` | Mirrors "basic information on the book". Includes the primary key, which C-6 requires. |
| Nested | `member{memberId, firstName, lastName}`, `loan{loanId, loanDate, returnDate}`, `book{bookId, title, authors}` | Includes every key needed to verify the chain. `returnDate` is nullable, which exercises null semantics. |

### 4.2 Canonical Required-Information Representation (CRIR) [DECISION AD-34]

For each workload there is **one** canonical JSON document holding exactly the required information. It does not depend on any protocol, envelope or number of calls. In particular, Required Bytes no longer depends on how a sample response is shaped.

| Property | Rule |
|---|---|
| Structure | Flat: a JSON array of book objects. Nested: an array of member objects, each with a `loans` array, each loan with a `book` object. The nesting follows the conceptual chain, never a protocol's response shape. |
| Field names | Fixed by the CRIR specification (the camelCase names in §4.1). They are identical to the API contract, but defined independently of it. |
| Ordering | Arrays are ordered by primary key: members by `memberId`, loans within a member by `loanId`, books by `bookId`. Object keys are ordered by RFC 8785. |
| Values | Strings as stored (Unicode NFC, as loaded). Dates as `YYYY-MM-DD`. Decimals as JSON numbers. |
| Nulls | A required nullable field is always **present**, with an explicit `null`. "Absent" and "null" are different values. |
| Serialisation | **RFC 8785 JSON Canonicalization Scheme (JCS)**: UTF-8, no insignificant whitespace, lexicographic key order, minimal string escaping, ES6 number formatting |
| Size | `RequiredBytes(workload) = len(JCS(CRIR))`, a single number per workload |

**Two independent ways to obtain the CRIR, which must agree:**
1. **Oracle:** built directly from the frozen dataset (generator CSVs or the database export) with the record-set definition (§1).
2. **Per configuration:** extracted from that configuration's sample response or responses. The extraction is a *normaliser* specific to the configuration:
   - F-G / N-G: strip `data`, then select the fields.
   - F-R / N-RC: select the required fields from the full representations.
   - N-RM: **join the three response bodies** on `memberId` and `bookId` into the nested structure.

   Each normaliser then emits JCS.

**Relation to M2 §2.3.3.** M2 asks for "the compact JSON serialisation … of the required field list over the returned records, computed once from a sample response body". The CRIR **is** a compact JSON serialisation. It adds a fixed key order, ordering, number and escaping rules, and the oracle cross-check. These make the result protocol-independent and reproducible. This is classified as a **clarification of M2 §2.3.3** that needs your sign-off, not a change to the metric.

### 4.3 Actual Bytes and over-fetching (genuine differences preserved)

```
ActualBytes(cfg) = Σ over the task's responses of len(raw response body as transmitted)   # N-RM: 3 bodies
OverFetch(cfg)   = (ActualBytes − RequiredBytes) / ActualBytes                           # constant per configuration
```

- **Raw transmitted bodies.** Actual Bytes uses the raw bodies as transmitted, not a re-serialisation, so that real protocol differences are kept: the GraphQL envelope, REST extra fields, N-RM's repeated linking IDs and per-call array wrappers, and each serializer's number and escape formatting.
  - Each body must already be compact; the test is that re-serialising it compactly gives the same length. If one is not compact, the configuration is fixed rather than corrected after the fact. This keeps the result consistent with M2's "compact serialisation of its full response body".
  - Headers are excluded [REQ M2 §2.3.3].
  - Compression is off (AD-05), so the transmitted body equals the decoded body.
- **Invariant:** for every configuration, `ActualBytes ≥ RequiredBytes`, and the normalised CRIR equals the oracle. A violation fails the equivalence gate.
- **Decomposition [PROPOSED].** It is reported per configuration and answers R1-5 by mechanism:

  `Actual − Required = envelope bytes + non-required field bytes + formatting delta (numbers/escaping vs JCS) + multi-call overhead (repeated keys, wrappers)`

  Each term is computed by the normaliser.
- The AD-32 shared encoder policy still applies. It prevents the *encoder* from becoming a protocol difference. Whatever difference remains after AD-32 is genuine, and it is reported.

### 4.4 Golden-response tests [REQ M2 §2.3; test design PROPOSED]

The golden files are generated from the oracle once the dataset is frozen. They are committed in `benchmark/tests/golden/<workload>.crir.json` together with their SHA-256. They are compared with every configuration:
- in the Phase 5 local gate, in CI-style local runs;
- in the Phase 10 remote pre-campaign check.

| Check | Failure condition |
|---|---|
| Record count | Members, loans or books count differs from the golden file (and from the M2 count invariant) |
| IDs | Any primary key or `(parent, child)` key pair differs |
| Ordering | Array order differs from the CRIR order. A configuration's *native* order is also asserted, so that ordering cannot silently depend on the hash order of a DataLoader. |
| Required fields | A field is missing in any configuration. In GraphQL, any non-required field is selected. |
| Values | A byte difference after JCS normalisation of a value. This catches date, decimal, whitespace and Unicode differences. |
| Null semantics | A required nullable field is absent instead of `null` (or the reverse), in any configuration |
| Envelope and errors | A GraphQL body has an `errors` key or lacks `data`. A REST body is not the documented shape. |
| Compactness | A raw body contains insignificant whitespace |
| Bytes invariant | `ActualBytes < RequiredBytes`, or the normalised CRIR differs from the oracle |

C-6 (the M2 key lists) is produced as a by-product of these tests. The JMeter assertion counts in §4.5 come from the golden files.

### 4.5 Expected assertion values

These come from the golden files, never typed by hand:
- Flat: N.
- N-RC and N-G: M members and M·L loans.
- N-RM calls: M, M·L and M·L.

---

## 5. Decision summary for this document

| ID | Decision | Recommendation |
|---|---|---|
| A-6 | Required field lists | Researcher supplies them. Placeholders are in §4.1. |
| AD-07 | Multi-call chain design | (i) for the primary cells, with a scope statement |
| AD-27 | Client RTT emulation | Record and disclose; no emulation |
| AD-28 | Extraction time inside the N-RM task | **Include**: the literal M2 §2.3.4 interval. Validated in doc 04 §1.1. |
| AD-32 | Serializer encoder, dates, decimals | One shared policy |
| AD-34 | Canonical required-information representation | CRIR + RFC 8785 JCS + an oracle cross-check (replaces AD-16) |
| AD-36 | Data-freeze verification | Content hashes + row counts + integrity checks (§2.4) |
| AD-08 | Dataset source and generator | Kaggle [33] + seeded synthetic members and loans. Check the licence. |
| AD-09 | REST representation | (a) all stored columns, optionally (c) both weights offline |
| AD-10 | Composite content | Full representations embedded |
| AD-11 | Storage layout | Database per service, one instance |
| AD-15 | Fixed request vs rotating IDs | Fixed, as M2 specifies |
| — | N, O, M, L, dataset sizes | Finalised after local validation (Tier 2) |
