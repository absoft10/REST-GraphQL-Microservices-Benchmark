# Sample requests and responses: local TEMPORARY fixture

> **NOT RESEARCH DATA.** These files were captured from the running application on 2026-11-01. The application served the deterministic **TEMPORARY book fixture** (`books_source = TEMPORARY_FIXTURE_NOT_RESEARCH_DATA`), seed `20261101`, on LocalDB. The book fields and values are placeholders until the Kaggle dataset has been verified (DECISION-004, O-7). Byte counts here say nothing about REST vs GraphQL performance, and they must not be cited as results.

| File | Content |
|---|---|
| `F-R.*`, `F-G.*`, `N-RC.*`, `N-G.*`, `N-RM_call1..3.*` | `*.response.json` = raw response body exactly as transmitted. `*.exchange.txt` = method, URL, request body and response headers. |
| `bytes_summary.csv` | Required Bytes, Actual Bytes and the four-term decomposition per configuration (required-bytes-spec.md §4) |
| `flat_keys.txt`, `nested_keys.txt` | C-6 key lists per configuration. They are identical within each workload. |
| `report.json` | Equivalence result. All five configurations equal the dataset oracle. |
| `dataset_manifest.json`, `data_freeze_local-fixture.json` | Generator manifest (seed, options, per-table canonical SHA-256) and the passing data-freeze verification |

Regenerate everything with:

```powershell
benchmark/scripts/db-init.ps1
benchmark/scripts/start-services.ps1
benchmark/scripts/verify-equivalence.ps1
```

See `benchmark/README.md`.
