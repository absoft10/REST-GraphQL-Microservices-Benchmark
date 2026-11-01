# 09 — Dataset Profiling Report: Kaggle Books Dataset (Task 4)

| | |
|---|---|
| Date | 2026-11-02 (download 2026-11-01T21:06:15Z) |
| Status | **Approved by the researcher on 2026-11-02 (DECISION-009) and implemented.** See §8 for the import results. |
| Scope | Archive verification, profiling of both CSV files, fit with the DDL and API contracts, proposed deterministic cleaning rules |
| Changes made | Download only, into the git-ignored `benchmark/data/source/kaggle-books-dataset-v3/` (`archive.zip`, `DOWNLOAD_RECORD.json`). No code, database, seed data, decision record or git history was modified. |

## 1. Source and provenance (verified)

| Item | Value | Source |
|---|---|---|
| Dataset | "Books Dataset", subtitled "A Comprehensive Dataset of 103,063 Books" | Kaggle Croissant metadata, API |
| Kaggle ref / URL | `elvinrustam/books-dataset` — https://www.kaggle.com/datasets/elvinrustam/books-dataset | Manuscript ref. [33] and its embedded PDF hyperlink; Croissant `url` |
| Creator | Elvin Rustamov | Croissant `creator` |
| Licence | CC0: Public Domain (https://creativecommons.org/publicdomain/zero/1.0/) | Croissant, API `licenseName` |
| Version | 3 ("Update 2023-12-20"), last updated 2023-12-20T20:46:34Z. Storage object `kaggle-data-sets/4200521/7250132/bundle/archive.zip`. | Kaggle API, download redirect |
| Provenance | Scraped from wonderbk.com, an online bookstore. No collection date is stated. | Dataset description |
| Download | Official public endpoint `…/api/v1/datasets/download/elvinrustam/books-dataset?datasetVersionNumber=3`. No authentication was required. HTTP 200, 2026-11-01T21:06:15Z. | `DOWNLOAD_RECORD.json` |

## 2. Archive verification

| File | Bytes | SHA-256 | Check |
|---|---|---|---|
| `archive.zip` | 55,469,565 (= 52.899 MiB, as Kaggle lists) | `e42f3dc8a64ed4a549bcf956b4f712452978ede7aa8ce49d3cdd27a3bf831839` | ZIP CRC test OK. No unsafe paths. Exactly 2 entries. |
| `BooksDataset.csv` | 69,747,726 (matches the Kaggle file list) | `2ada37a11b35575f566025b326bf62026f24e6053dfd62a7563df0d934555077` | — |
| `BooksDatasetClean.csv` | 66,321,656 (matches the Kaggle file list) | `e59f031f238b44b7c470ec4926e1a5c231b5cf00be9923308c19e577dd4bda53` | — |

**Git exclusion:**
- Confirmed by `.gitignore:10` (`/benchmark/data/source/`) for both the archive and `DOWNLOAD_RECORD.json`.
- No file under `benchmark/data/source/` is tracked, and `git status` is clean.
- Nothing was extracted: both CSVs were read in place from the ZIP.

## 3. File structure and encoding

| | `BooksDataset.csv` (raw) | `BooksDatasetClean.csv` |
|---|---|---|
| Data rows | **103,082** | **103,063** |
| Header | `Title, Authors, Description, Category, Publisher, Publish Date, Price` | `Title, Authors, Description, Category, Publisher, Price Starting With ($), Publish Date (Month), Publish Date (Year)` |
| Encoding | Valid UTF-8, no BOM, CRLF line endings | Valid UTF-8, no BOM, CRLF line endings |
| Parsing | Every row has 7 fields; no embedded newlines | Every row has 8 fields; no embedded newlines |

**Relationship.** The clean file is an **order-preserving subsequence** of the raw file. It is the raw file with exactly 19 rows removed:
- **8 exact duplicates;**
- **11 rows with impossible publication dates:** 2030 (×2), 2055, 2095, 2099 (×2) and 9999 (×5).

Its price and date values match the raw text in every retained row: **0 mismatches** for prices (`"Price Starting at $X"` → number) and 0 for dates (`"Friday, January 1, 1993"` → `January` / `1993`).

## 4. Column profile: `BooksDatasetClean.csv` (n = 103,063)

| Column | Missing (empty) | Max length (chars) | p99 length | Distinct | Other observations |
|---|---|---|---|---|---|
| Title | 0 | 293 | 133 | 97,807 | 16 with leading/trailing whitespace (incl. NBSP); 22 non-NFC; 6 with HTML tags (e.g. `<i>…</i>`, `&#0151;`); 3 mojibake |
| Authors | 0 | 305 | 90 | 63,570 | **102,020 prefixed `By `**. **1,043 are exactly `"By"`**, a placeholder for a missing author. 3 non-NFC; 5 mojibake (e.g. `NemcovÃ¡`). |
| Description | **32,859 (31.9%)** | **23,502** (23,809 UTF-8 bytes) | 2,809 | 68,828 | HTML entities and tags in 1,077 values (`&#39;` 1,532×, `&#151;` 842×, `&apos;` 368×, `<br>` 41×…); 25 mojibake (e.g. `â€”`, `Ã©`); mean 736 chars when present |
| Category | **26,161 (25.4%)** | 98 | 69 | 3,106 | **Every non-empty value starts with a space** (e.g. `" Fiction , General"`). 76,730 use the hierarchical ` , ` separator. |
| Publisher | 8 | 150 | 38 | 13,025 | 12 with edge whitespace; 8 non-NFC; 1 mojibake |
| Price Starting With ($) | 0 | — | — | 1,387 | All parse as decimals. 100,037 have 2 decimal places, 3,026 have 1 (e.g. `5.0`). Range 4.89–1,095.00. No zeros or negatives. |
| Publish Date (Month) | 0 | 9 | — | 12 | Valid month names. **January = 24.6%** (uniform ≈ 8.3%). |
| Publish Date (Year) | 0 | — | — | 181 | Integers 1755–2023 |

**Duplicates:**
- Exact duplicate rows: **0**, including after the proposed trimming and NFC normalisation.
- Same Title + Authors (case-insensitive): 3,691 extra rows in 3,437 groups.
- Same Title + Authors + Publisher: 1,037 extra rows.

These are separate listings, typically other editions or formats, with different descriptions and prices. They are not data errors.

**Date precision.** In the raw file the day is always `1` (102,848 of 102,848 dated rows), and 25,269 dates are exactly "January 1". Dates are therefore effectively **month precision**, and January is very likely a default for unknown months. There are 215 rows with a year only.

**Encoding issues are small and real:**
- 34 values have mojibake, and one title contains U+FFFD.
- 3,926 fields contain NBSP (U+00A0).
- HTML entities appear in descriptions.

All of these are present in the source as published.

## 5. Fit with the existing schema and API contracts

| Item | Provisional definition | Actual data | Fit |
|---|---|---|---|
| `title NVARCHAR(400) NOT NULL` | | max 293, 0 missing | ✅ |
| `authors NVARCHAR(400) NOT NULL` | | max 305, 0 empty, **1,043 = `"By"`** | ✅ technically. Semantics: see DS-5. |
| `description NVARCHAR(MAX) NULL` | | max 23,502; 31.9% missing | ✅ |
| `category NVARCHAR(200) NULL` | | max 98 after trim; 25.4% missing | ✅ |
| `publisher NVARCHAR(200) NULL` | | max 150; 8 missing | ✅ |
| `price DECIMAL(10,2) NULL` | | 1–2 decimals, max 1,095.00 | ✅ exact, with **no rounding** (`5.0` → `5.00`) |
| `publish_month NVARCHAR(20) NULL` | | max 9 | ✅ |
| `publish_year SMALLINT NULL` | | 1755–2023 | ✅ |
| REST Book representation / GraphQL `Book` / CRIR | `bookId, title, authors, description, category, publisher, price, publishMonth, publishYear` | Column-for-column match with `BooksDatasetClean.csv` | ✅ **No contract change needed.** The provisional book field names can be confirmed as final. |
| Required flat fields (O-1) | `bookId, title, authors, category, price, publishYear` | All available | ✅ |

**Workload implications (estimates, not measurements):**
- The first 100 books in file order serialise to **about 57.9 KB as full representations** against **about 19.1 KB of required fields**. Measured on the TEMPORARY fixture, F-R was 35.3 KB and its required fields 14.3 KB.
- The approved flat record set ("the first N books by `bookId`", O-8) is **less representative on descriptions**: 59% of the first 100 books lack one, against 31.9% overall.
- Nested target books are drawn by a seeded shuffle across all 103,063 books, so they are not affected.

## 6. Proposed deterministic cleaning rules

The rules apply to `BooksDatasetClean.csv` v3 (SHA-256 `e59f031f…bda53`), in this order:

| # | Rule | Effect on the actual data |
|---|---|---|
| R1 | Read UTF-8 strictly. Fail on any decoding error, field-count mismatch or header mismatch with the pinned header. | None (the file is clean) |
| R2 | `book_id` = 1-based position of the data row in the file (the header is not counted) | 1 … 103,063 |
| R3 | Every text value: Unicode **NFC**, then **trim** Unicode whitespace at both ends (including NBSP). Internal whitespace is kept. | 76,966 values change. 76,902 of these are the leading space on categories. |
| R4 | A nullable column whose value is empty after R3 becomes `NULL` | description 32,859, category 26,161, publisher 8 |
| R5 | `price` = exact decimal parse of the token, stored as DECIMAL(10,2). Any value that would need rounding is a hard error. | 103,063 values. 0 rounded. |
| R6 | `publish_year` = integer parse; `publish_month` = verbatim month name | 103,063 each |
| R7 | **No** row exclusion, de-duplication, HTML decoding, mojibake repair, internal whitespace collapsing, category splitting or case change | All 103,063 rows kept as the source states them |
| R8 | The text transformation for `Authors` is set by DS-5 | — |
| R9 | The generator records the source file SHA-256, the rule-set version and the output canonical hashes in the manifest. The data-freeze verifier checks them. | — |

The rationale for R7 is **minimal transformation**. Each extra heuristic would add a researcher degree of freedom and weaken the traceability to the published source. The dataset author already removed the duplicate and invalid-date rows.

## 7. Decisions requiring researcher approval (before import)

| ID | Decision | Options | Recommendation | Approval type |
|---|---|---|---|---|
| **DS-1** | Source file | `BooksDatasetClean.csv` / raw `BooksDataset.csv` | **Clean, v3, pinned by SHA-256.** Typed columns that match the schema; derived from raw by the author (§3). | Closes O-7 (data source) |
| **DS-2** | Row inclusion | All rows / additional exclusions | **All 103,063 rows**, with no further exclusion | Dataset definition |
| **DS-3** | `book_id` | File order / other | **1-based file order (R2)**. This confirms DECISION-004. | Confirmation |
| **DS-4** | Text normalisation | R3–R4 as stated / variants | **R3–R4** | Confirms the approved "trim + NFC" with exact semantics |
| **DS-5** | Authors `By ` prefix and the 1,043 bare `"By"` | **A** keep verbatim / **B** strip `By ` and map bare `"By"` → `NULL` / **C** strip `By ` and map bare `"By"` → `""` / **D** exclude the 1,043 rows | **A: verbatim.** No schema or contract change and no transformation; the artefact is disclosed. B would make `authors` nullable in the DDL, REST, the GraphQL schema (`String`) and the CRIR (a contract change). D changes the dataset. | **Scientific** (data semantics) |
| **DS-6** | Category | Trim only / split into a list / normalise separators | **Trim only (R3).** Keep the hierarchical string as published. | Representation |
| **DS-7** | HTML entities and tags, mojibake | Keep verbatim / decode or repair | **Verbatim** (R7). Counts are disclosed. | Data semantics |
| **DS-8** | Month precision | Use as is / treat January as unknown | **As is.** Month is not a required field. The January-default artefact is disclosed. | Disclosure only |
| **DS-9** | Flat record set | Keep O-8 "first N by `bookId`" / another deterministic page or sample | **Keep O-8** (approved), and disclose its lower description coverage. Changing it is a protocol change, decided with N at Tier 2. | **Protocol (Tier 2)** |
| **DS-10** | Replication package | Redistribute CSV (CC0) / ship download script + SHA-256 only | **Script + hash.** The text is scraped third-party content, and CC0 from the uploader cannot clear third-party rights. | Publication |
| DS-11 | Paper citation (Tier 4) | — | Correct [33] to "E. Rustamov, Books Dataset, Kaggle, v3, 2023" | Paper |
| DS-12 | TEMPORARY fixture | Keep for automated tests / remove | **Keep for tests only.** It is labelled and is never used for measured runs. | Engineering |

**What approval unlocks:**
- an importer implementing R1–R9;
- the final `10_catalog.sql`, with the column set unchanged;
- regenerated manifests and the data-freeze record;
- the golden CRIR files from the real data;
- re-running the equivalence suite;
- updating DECISION-004 and `PROTOCOL_STATUS.md` from provisional to final.

## 8. Import results (2026-11-02)

| Check | Result |
|---|---|
| Rows imported | **103,063** of 103,063, with `book_id` 1 … 103,063 in file order |
| Rule statistics (importer) | Values changed by NFC/trim: 76,966. NULL description 32,859, category 26,161, publisher 8, price 0. Bare `"By"` authors kept: 1,043. One-decimal prices: 3,026. **All equal the read-only profile above.** |
| Source-to-database hash | `catalog.book` canonical SHA-256 `dd40286a317d42092e9730fdc9da5312291481f6fb749b1dabf1427fa9625201`. It is identical from the C# importer, from the LocalDB contents, and from an independent re-implementation of R1–R8 reading the CSV in the archive. |
| Data freeze (`lms-bench db verify`) | **24/24 PASS**, including the pinned archive and entry SHA-256 stored in the database |
| Members / loans (seed 20261101) | 10,000 members / 100,933 loans; 0 orphans; target-set invariants hold |
| Schema | Unchanged; all values fit (the `10_catalog.sql` comment now says FINAL) |
| Contracts | Unchanged (api-contracts v0.3: markers resolved, CHANGELOG C-12) |
| Live equivalence (five configurations) | PASS. See `docs/api-contracts/samples/kaggle-v3-local-prebenchmark/`. |

**Documented limitation (DS-9).** The approved flat record set (the first 100 books) has 59.0% missing descriptions against 31.9% overall, so its full REST representation is smaller than an average 100-book page. Record selection and pagination are unchanged.
