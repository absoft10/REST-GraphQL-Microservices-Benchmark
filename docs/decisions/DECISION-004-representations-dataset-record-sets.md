# DECISION-004: Representations, dataset and record sets (O-4, O-7, O-8)

| Field | Value |
|---|---|
| Date | 2026-11-01 |
| Decided by | Researcher, in the "Approval of Tier 1 Decisions" written instruction |
| Status | O-4 **approved**, with the book column set **PROVISIONAL**. O-7 **approved in principle** and **pending verification**. O-8 **approved**, with its sizes left at Tier 2. |
| Source design | doc 08 §4; doc 03 §1–§3 |

## O-4: content of full REST representations (approved)

- **Books: PROVISIONAL.** Every column present in the source dataset after deterministic cleaning. No column is dropped for size.
- **Members (approved):** exactly the baseline `User` fields: `memberId, firstName, lastName, email, phoneNumber, address`.
- **Loans (approved):** exactly the baseline `Loan` fields: `loanId, memberId, bookId, loanDate, returnDate, isReturned`.
- The earlier proposals `joined_at` and `due_date` are **withdrawn**.

## O-7: dataset acquisition and verification (approved in principle)

- **Source:** the Kaggle "Books dataset" (manuscript ref. [33]) plus seeded synthetic members and loans.
- **Before the dataset schema and the workloads are finally frozen**, the researcher downloads the actual file into `benchmark/data/source/`. Kaggle requires the researcher's account. The downloaded file is then inspected to verify the column names and types, the row count and the licence and redistribution terms, and its SHA-256 and download date are recorded.
- **Non-blocking (researcher qualification).** Acquisition does **not** block initial scaffolding, the shared hosting infrastructure, or any other work that does not depend on the final data schema.
- **Provisional until verification:**
  - the `book` DDL and cleaning rules;
  - the book field names in O-1, O-2 and O-4;
  - the book part of the CRIR;
  - the golden files;
  - the dataset volumes.

## O-8: record-set semantics (approved; sizes remain Tier 2)

- **Flat:** the first N books by `bookId` (offset 0).
- **Nested:** the first M members by `memberId`. Each has exactly L loans, on M·L distinct books. Within each member, loans ordered by `loanId` alternate: odd positions are returned (with a `returnDate`), even positions are not (`returnDate = null`).
- N, M and L are Tier 2. The placeholders are 100, 10 and 5.

## Update 2026-11-02 — O-7 closed by DECISION-009

The Kaggle dataset was downloaded, verified and profiled (`docs/milestone2/09_Dataset_Profiling_Report.md`) and approved as the research source (DECISION-009). All elements listed above as **provisional until verification** are now **final**:

- The book column set equals the provisional set. It maps one-to-one onto `BooksDatasetClean.csv`, and no field is renamed, added or removed.
- The cleaning rules are R1–R9, the `book` DDL is unchanged and the volume is 103,063 books.
- The golden values are in `benchmark/tests/golden/kaggle-v3.golden.json`.
- The O-8 record-set semantics are unchanged (DS-9). The low description coverage of the first 100 books is a documented limitation.
