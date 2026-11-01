# Local pre-benchmark validation evidence: research dataset (kaggle-v3)

> **LOCAL PRE-BENCHMARK VALIDATION — NOT EXPERIMENTAL RESULTS.**
> - Captured on 2026-11-02 from the five services running on one workstation against SQL Server LocalDB.
> - **No AWS deployment, no load test and no timing is involved.** The byte counts are deterministic properties of the response payloads for the approved record sets (N = 100, M = 10, Tier 2 placeholders). They are not performance measurements.

| Item | Value |
|---|---|
| Book source | Kaggle `elvinrustam/books-dataset` v3, `BooksDatasetClean.csv`. Archive SHA-256 `e42f3dc8…1839`; entry SHA-256 `e59f031f…da53` (DECISION-009) |
| Cleaning | Rules R1–R9 (`kaggle-books-clean-rules/1.0`, docs/milestone2/09 §6) |
| Generated data | Members and loans from seed 20261101 (10,000 members, 100,933 loans; M = 10, L = 5) |
| Equivalence | **PASS for all five configurations.** Every normalised response equals the CRIR oracle read from the databases. |

**Byte accounting** (`bytes_summary.csv`):

| Config | Required B | Actual B | Envelope | Non-required | Formatting | Multi-call |
|---|---|---|---|---|---|---|
| F-R | 18,675 | 58,468 | 0 | 39,461 | 332 | 0 |
| F-G | 18,675 | 19,026 | 19 | 0 | 332 | 0 |
| N-RC | 9,560 | 39,669 | 0 | 30,054 | 55 | 0 |
| N-RM | 9,560 | 39,221 | 0 | 28,653 | 55 | 953 |
| N-G | 9,560 | 9,636 | 21 | 0 | 55 | 0 |

## Files

| File | Content |
|---|---|
| `bytes_summary.csv`, `report.json` | Required and Actual Bytes, the decomposition, and the equivalence result per configuration |
| `flat_keys.txt`, `nested_keys.txt` | C-6 key lists (IDs only), identical within each workload |
| `exchanges/*.exchange.txt` | Request line, GraphQL request body and response headers for each call (no response bodies) |
| `dataset_manifest.json`, `data_freeze_kaggle-v3.json`, `dataset_download_record.json` | Source pinning, row counts, canonical table hashes, rule statistics, and the passing data-freeze report (24/24 checks) |
| `LOCAL_ONLY_SHA256SUMS.txt` | SHA-256 of the response bodies and CRIR files that are **kept local only** |

**Why bodies are not published (DS-10).** The response bodies and CRIR files contain scraped book text. Under DS-10 that text is not redistributed. They are regenerated locally with:

```powershell
benchmark/scripts/db-init.ps1
benchmark/scripts/start-services.ps1
benchmark/scripts/verify-equivalence.ps1
```

The regenerated files must match `LOCAL_ONLY_SHA256SUMS.txt`.

**Known limitation (DS-9).** The approved flat record set ("first 100 books by `bookId`") has 59% missing descriptions, against 31.9% across the whole dataset. Its REST payload is therefore not representative of the average book. The record set is kept as approved, and the limitation is disclosed.
