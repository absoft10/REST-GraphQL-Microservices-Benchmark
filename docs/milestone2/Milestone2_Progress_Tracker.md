# Milestone 2 Progress Tracker

| | |
|---|---|
| Updated | 2026-11-01 |
| Rule | Ten checkpoints, 10% each. A checkpoint counts only when its implementation **and** validation evidence exist. This tracker reports progress. It has no authority over the scientific protocol (`PROTOCOL_STATUS.md`, docs 01–08, DECISION records). |
| **Completion** | **50% confirmed.** Task 4 is **provisional**: it reaches 60% once the Kaggle dataset has been downloaded and verified and the book-dependent parts are finalised. |

| # | Checkpoint | Status | Weight | Evidence |
|---|---|---|---|---|
| 1 | Workspace configuration and project analysis | ✅ Complete | 10% | Project analysis (baseline findings in doc 02 §2); local development configuration (not published) |
| 2 | Experimental architecture design and Phase 0 baseline approval | ✅ Complete | 10% | `docs/milestone2/01–08`, DECISION-001…008, baseline fingerprints, git initialised |
| 3 | Decisions and API specifications | ✅ Complete | 10% | `docs/api-contracts/` (v0.2-provisional); §3 choices approved |
| 4 | Database schema and experimental dataset | 🟡 **Provisional** (not counted) | 0 / 10% | See §4 below. Everything independent of the Kaggle file is done and verified; the book table and book data are a TEMPORARY fixture. |
| 5 | Full application implementation | ✅ Complete (book fields provisional, as specified) | 10% | `benchmark/src/`, the five configurations served from real LocalDB data (§5) |
| 6 | Local testing and validation | ✅ Complete | 10% | **125/125 tests passed** (§6), equivalence PASS on live services, data freeze 21/21 PASS |
| 7 | Instrumentation and telemetry (stats sidecar, runtime counters, DB-time aggregation, host logger, log export) | ⬜ Not started | 0 / 10% | doc 04 §4, doc 05 Phase 6 |
| 8 | JMeter plans, campaign runner and analysis pipeline | ⬜ Not started | 0 / 10% | doc 04 §1–§2, §5–§6; doc 05 Phases 7–8 |
| 9 | AWS infrastructure, deployment and capacity pilot (**researcher**) | ⬜ Not started | 0 / 10% | doc 05 Phases 9–10; needs AUTH-AWS-1 |
| 10 | Benchmark campaign, statistical analysis and replication package | ⬜ Not started | 0 / 10% | doc 05 Phases 11–13; needs AUTH-AWS-2 |

## 4. Task 4: database and dataset (provisional)

| Deliverable | Status | Evidence |
|---|---|---|
| DDL for three service-owned databases, schemas, tables, indexes | ✅ | `benchmark/data/ddl/00,10,20,30,99_*.sql`. Collation `SQL_Latin1_General_CP1_CI_AS` and compatibility level 160 are pinned. |
| Member and loan tables (approved O-4) | ✅ Final | `20_members.sql`, `30_loans.sql` (with a CHECK constraint for null semantics) |
| **Book table** | 🟡 **Provisional** | `10_catalog.sql`. The column set mirrors the provisional contract and **does not claim** the Kaggle schema. |
| Deterministic preparation and seeding tool | ✅ | `lms-bench db seed` (SplitMix64, recorded seed) |
| Synthetic member and loan generators with fixed recorded seeds | ✅ | `DatasetGenerator.cs`. Seed 20261101 (local), 424242 (tests). Both are recorded in the manifest and in `meta.dataset_info`. |
| Book data | 🟡 **TEMPORARY fixture** | `BookFixture`. Labelled `TEMPORARY_FIXTURE_NOT_RESEARCH_DATA` in every database and every artifact. |
| Validation of logical references | ✅ | Verifier: cross-database orphan checks = 0 |
| Row-count checks and reproducible manifests | ✅ | `manifest.json`, `COUNT_BIG` cross-check |
| Dataset checksums and integrity verification | ✅ | Canonical-row SHA-256, which must equal the generator hash; schema fingerprint (replaces `CHECKSUM_AGG`, AD-36) |
| Initialisation and reset scripts | ✅ | `scripts/db-init.ps1`, `lms-bench db init/reset/drop` |
| Local creation instructions | ✅ | `benchmark/README.md` §2 |
| **Kaggle dataset download, inspection, licence, final schema, cleaning rules, golden files** | ❌ **Blocked: needs the researcher** | DECISION-004 O-7 (Kaggle requires your account) |

## 5. Task 5: application

All five services use .NET 10 and run on LocalDB: Catalog, Members, Loans, REST edge and GraphQL edge. Shared libraries: Contracts, Hosting (SP-1, host defaults, problem+json), Data, DomainClient and RequiredBytes. The five configurations are F-R, F-G, N-RC, N-RM and N-G. No responses are hard-coded: the oracle is read independently from the databases, and responses must equal it.

## 6. Task 6: tests actually executed (2026-11-01, TRX in `benchmark/artifacts/test-results/`)

| Suite | Executed | Passed | Failed | Covers |
|---|---|---|---|---|
| `Lms.UnitTests` | 70 | 70 | 0 | Parsing and validation, RFC 8785 (vectors, sorting, numbers, escapes), Required Bytes worked example (179/199/292/283) and decomposition, SP-1 serialization (SER-2..5), generator determinism and invariants, canonical encoding, contract-file conformance |
| `Lms.IntegrationTests` (real LocalDB) | 55 | 55 | 0 | Data freeze and tamper detection, DDL idempotency and constraints, domain API (paging, ordering, IDs, validation, 404), REST edge (representations, headers, errors, 502), GraphQL (schema vs the contract SDL, results, INVALID_ARGUMENT, UPSTREAM_FAILURE, GET and batching disabled), **all five configurations equivalent to the oracle**, Required Bytes identical within each workload, decomposition exact, compactness, C-6 keys, **internal-call counts identical** (1 flat / 3 nested), N-RM workflow, cross-stack serialization parity (SER-1), captured SQL |
| **Total** | **125** | **125** | **0** | |

Additional live evidence (services running on localhost):
- `lms-bench equivalence`: PASS for all 5 configurations.
- All 12 requests in `http/scenarios.http` executed with the statuses the contract specifies.

## Open items affecting the next checkpoints

1. **O-7:** download the Kaggle dataset. This is the only blocker for Task 4.
2. **AD-37 (new):** HotChocolate `MaxConcurrentExecutions` default of 64 (GraphQL-only limiter). Proposed: unlimited.
3. **Tier 2 decisions** (doc 06 §2) before Phase 9.
