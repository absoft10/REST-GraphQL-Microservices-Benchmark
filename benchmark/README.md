# LMS REST vs GraphQL benchmark: local execution guide

This is the new system under test (DECISION-002, AP-1). It runs **locally on Windows with SQL Server LocalDB**, with no Docker and no AWS. The contracts are in `../docs/api-contracts/`. The design is in `../docs/milestone2/`.

> **Book data is a TEMPORARY fixture.** It is not the research dataset, and it is labelled `TEMPORARY_FIXTURE_NOT_RESEARCH_DATA` in every database. The Kaggle dataset still has to be downloaded and verified (DECISION-004, O-7). No number produced locally is a research result.

## 0. Prerequisites (checked on this workstation, 2026-11-01)

| Tool | Version | Check |
|---|---|---|
| .NET SDK | 10.0.401 (pinned in `global.json`, `latestPatch`) | `dotnet --list-sdks` |
| SQL Server LocalDB | `MSSQLLocalDB` 17.0.4025.3 | `sqllocaldb info MSSQLLocalDB` |
| PowerShell | 5.1+ | — |
| Optional | VS Code REST Client / Visual Studio / JetBrains (for `http/scenarios.http`); Postman (for `../docs/api-contracts/postman/`) | — |

All commands run from `benchmark/`.

## 1. Build

```powershell
.\scripts\build.ps1
```

This restores the pinned packages (`Directory.Packages.props`) and builds with warnings treated as errors. It writes `artifacts/BUILD_INFO.md`, which lists the SDK and all resolved package versions (a B-4 draft).

## 2. Create and seed the local databases

```powershell
.\scripts\db-init.ps1
```

It runs these steps:
1. `lms-bench db reset`: drops and recreates `lms_catalog`, `lms_members` and `lms_loans` from `data/ddl/*.sql`.
2. `lms-bench db seed`: generates the dataset deterministically. The default seed is `20261101`, with 2,000 fixture books, 10,000 members, M = 10 and L = 5. It writes `data/generated/local-fixture/{books,members,loans}.csv` and `manifest.json`, then bulk loads the data.
3. `lms-bench db verify`: data-freeze verification. It checks content hashes against the generator, row counts, cross-database orphan references, the O-8 target-set invariants, the compatibility level and a schema fingerprint. It writes `data_freeze_local-fixture.json`. **Every line must show PASS.**

Parameters: `-Seed`, `-Books`, `-Members`, `-TargetMembers`, `-LoansPerTarget`, `-Prefix`, `-Label`. The DDL also runs with plain sqlcmd:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -i data\ddl\00_create_databases.sql -v CatalogDb=lms_catalog MembersDb=lms_members LoansDb=lms_loans
```

Then run `10_catalog.sql`, `20_members.sql` and `30_loans.sql`, each with `-d <db>` and the same `-v` variables.

## 3. Start the five services

```powershell
.\scripts\start-services.ps1      # waits for /health on all five
.\scripts\stop-services.ps1       # stops them (by recorded PID)
```

| Service | URL | Role |
|---|---|---|
| Lms.Catalog.Service | http://localhost:5101 | Domain: books (`/internal/books`) |
| Lms.Members.Service | http://localhost:5102 | Domain: members (`/internal/members`) |
| Lms.Loans.Service | http://localhost:5103 | Domain: loans (`/internal/loans`) |
| Lms.Edge.Rest | http://localhost:5201 | Public REST edge |
| Lms.Edge.GraphQL | http://localhost:5202 | Public GraphQL edge (`POST /graphql`) |

Logs go to `artifacts/logs/`. The connection strings use LocalDB with integrated authentication; no secrets are stored.

## 4. Execute the five experimental scenarios

Use `http/scenarios.http`, which contains every scenario plus error examples, or the Postman collection. N-RM computes its ID lists automatically in Postman.

| # | Config | Request(s) |
|---|---|---|
| 1 | F-R | `GET /api/books?offset=0&limit=100` |
| 2 | F-G | `POST /graphql`, operation `FlatBooks`, variables `{"offset":0,"limit":100}` |
| 3 | N-RC | `GET /api/members-with-loans?offset=0&limit=10` |
| 4 | N-RM | `GET /api/members?offset=0&limit=10`, then `GET /api/loans?memberIds=<ids>`, then `GET /api/books?ids=<sorted unique bookIds>` |
| 5 | N-G | `POST /graphql`, operation `NestedMembers`, variables `{"offset":0,"limit":10}` |

## 5. Verify data and response equivalence (needs the running services)

```powershell
.\scripts\verify-equivalence.ps1
```

This runs all five configurations and normalises each response to the canonical required-information representation. It compares the result with the **oracle built directly from the databases**, and it computes Required Bytes, Actual Bytes and the exact decomposition. Outputs go to `artifacts/equivalence/local-fixture/`: `bytes_summary.csv`, `flat_keys.txt` and `nested_keys.txt` (C-6), `report.json`, and `samples/`. The run must end with `EQUIVALENCE: PASS`.

## 6. Automated tests (no Docker)

```powershell
.\scripts\stop-services.ps1       # running services lock build outputs
.\scripts\run-tests.ps1           # -Suite unit | integration | all (default)
```

- `tests/Lms.UnitTests` needs no database. It covers parsing, RFC 8785, Required Bytes and the worked example, the serialization policy, generator determinism, and contract-file conformance.
- `tests/Lms.IntegrationTests` uses **real LocalDB**. It drops and recreates `lms_it_catalog`, `lms_it_members` and `lms_it_loans` (seed 424242), hosts all five services in-memory, and covers:
  - database consistency;
  - the domain API;
  - REST and GraphQL edges (the schema is compared with the contract SDL);
  - all five configurations and their equivalence;
  - the N-RM workflow;
  - cross-stack serialization parity;
  - internal-call counts;
  - captured SQL (written to `artifacts/sql/captured-sql.md`).

TRX results go to `artifacts/test-results/`.

## 7. Layout

```
src/   Lms.Contracts · Lms.Hosting · Lms.Data · Lms.DomainClient · Lms.RequiredBytes · Lms.Bench.Tool (lms-bench)
       Lms.Catalog.Service · Lms.Members.Service · Lms.Loans.Service · Lms.Edge.Rest · Lms.Edge.GraphQL
tests/ Lms.UnitTests · Lms.IntegrationTests
data/  ddl/ (committed DDL) · generated/ (git-ignored; reproducible from seed)
http/  scenarios.http
scripts/  build · db-init · start-services · stop-services · verify-equivalence · run-tests (.ps1)
```

## 8. Not in this phase

The following are not part of this phase:
- telemetry sidecar and runtime and DB-time logging (Phase 6);
- JMeter plans and runner (Phase 7);
- the analysis pipeline (Phase 8);
- IaC and AWS (Phases 9–11; the researcher only);
- the real Kaggle dataset (O-7).
