# DECISION-003: Required information and Required Bytes (O-1, O-2, O-3)

| Field | Value |
|---|---|
| Date | 2026-11-01 |
| Decided by | Researcher, in the "Approval of Tier 1 Decisions" written instruction |
| Status | **Approved.** The field *names* and the flat field *mapping* are **PROVISIONAL** until dataset verification (DECISION-004, O-7). The *meaning* of each field is approved. |
| Source design | doc 08 §2–§3; doc 03 §4 |
| M2 relation | O-3 is clarification **CL-1** of M2 §2.3.3, approved |

## O-1: flat required fields (approved; names provisional)

`bookId, title, authors, category, price, publishYear`

These are the key plus the attributes the manuscript gives for the book dataset (§III-A-1): title, author information, genre, price and publication year. After the dataset is verified, each name maps to the actual source column with the same meaning. A column that turns out to be missing goes back to the researcher. It is not silently dropped.

## O-2: nested required fields (approved)

- member: `memberId, firstName, lastName`
- loan: `loanId, loanDate, returnDate` (nullable)
- book: `bookId, title, authors`. The book names are provisional, as in O-1.

## O-3: Required Bytes (approved, with the researcher's qualifications)

1. **The CRIR is used for both workloads.** It is a canonical required-information representation, defined as follows:
   - JSON;
   - arrays ordered by primary key;
   - nested chain member → `loans` → `book`;
   - explicit nulls;
   - serialised with RFC 8785 (JCS).
2. **Every configuration must produce equivalent required information.** Each configuration's normalised response must equal the CRIR oracle, which is built from the frozen dataset:
   - GraphQL: `data` is stripped.
   - REST: projected onto the required fields.
   - N-RM: the 3 bodies are joined on the keys.
3. **Required Bytes is kept separate from Actual Bytes.** `RequiredBytes(workload) = len(UTF-8(JCS(CRIR)))`. Actual Bytes is the raw transmitted response-body length (the sum over the N-RM calls). The two are never computed from each other.
4. **The full calculation is documented and tested reproducibly.** See doc 03 §4.2–§4.4. The calculation is implemented once, in a pinned script, with:
   - golden files committed with their SHA-256 (counts, IDs, ordering, required fields, values, null semantics, envelope and errors, compactness, the `Actual ≥ Required` invariant);
   - an over-fetching decomposition (envelope, non-required fields, formatting delta, multi-call overhead).
5. Genuine protocol payload differences are preserved. Nothing is normalised in Actual Bytes.
