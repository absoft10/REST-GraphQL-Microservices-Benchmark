# DECISION-009: Final research dataset (DS-1 … DS-12)

| Field | Value |
|---|---|
| Date | 2026-11-02 |
| Decided by | Researcher. Written approval of the recommendations in `docs/milestone2/09_Dataset_Profiling_Report.md`, with explicit requirements 1–14. |
| Recorded by | AI-assisted development tooling (transcribed; requirements kept verbatim in meaning) |
| Closes | DECISION-004 O-7 (dataset verification). The book-dependent provisional elements of DECISION-003/004 become final. |
| Evidence | `docs/milestone2/09_Dataset_Profiling_Report.md` (profile); `docs/api-contracts/samples/kaggle-v3-local-prebenchmark/` (import, freeze and equivalence evidence); `benchmark/tests/golden/kaggle-v3.golden.json` |

## Approved decisions

| ID | Decision (approved as recommended) |
|---|---|
| DS-1 | Source: `BooksDatasetClean.csv` from Kaggle `elvinrustam/books-dataset`, **version 3**. Archive SHA-256 `e42f3dc8a64ed4a549bcf956b4f712452978ede7aa8ce49d3cdd27a3bf831839`; entry SHA-256 `e59f031f238b44b7c470ec4926e1a5c231b5cf00be9923308c19e577dd4bda53`. Downloaded 2026-11-01T21:06:15Z from the official public endpoint. These values are pinned in code (`KaggleBooksSource`). Any other file is rejected. |
| DS-2 | All **103,063** records are kept. There are no exclusions. |
| DS-3 | `book_id` = 1-based data-row order of the cleaned CSV |
| DS-4 | Text rules: NFC, then trim of Unicode white space at both ends; empty optional value → NULL; internal whitespace kept |
| DS-5 | Authors **verbatim**, including the `By ` prefix and the 1,043 bare `"By"` placeholders (option A). No schema or contract change. |
| DS-6 | Category: trim only. The hierarchical string is kept as one value. |
| DS-7 | HTML entities and tags and existing encoding artefacts are kept verbatim. There is no content correction. |
| DS-8 | Month and year as published. The January-default artefact is disclosed. |
| DS-9 | The flat workload stays at the **first 100 books** (O-8). Its 59% missing-description rate (31.9% overall) is a documented limitation. Record selection and pagination are unchanged without authorisation. |
| DS-10 | The replication package ships the download script and SHA-256 values, not the scraped CSV or bodies containing its text |
| DS-11 | Paper citation corrected (Tier 4): "E. Rustamov, Books Dataset, Kaggle, v3, 2023" |
| DS-12 | The TEMPORARY fixture is kept for automated tests only. It is labelled `TEMPORARY_FIXTURE_NOT_RESEARCH_DATA` and is never used for measured runs. |

Prices are parsed exactly. A value that would need rounding is a hard error; there are 0 such values. Distinct listings that share a title and author are preserved. The REST and GraphQL contracts and the A-6 required-field lists are **unchanged**.

## Implementation (2026-11-02)

- Importer: `benchmark/src/Lms.Data/Dataset/KaggleBooksImporter.cs` (rules R1–R9) and `Rfc4180Reader.cs`.
- Seeding: `lms-bench db seed --books-source kaggle`, or `scripts/db-init.ps1`, where `kaggle` is the default.
- Source-to-database verification:
  - the C# importer, the database content hash and an independent re-implementation of the rules all give `dd40286a317d42092e9730fdc9da5312291481f6fb749b1dabf1427fa9625201` for `catalog.book`;
  - data freeze: 24/24 checks pass.
