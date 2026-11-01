# Milestone 2 Progress Tracker

| | |
|---|---|
| Updated | 2026-11-02 |
| Rule | Ten checkpoints, 10% each. A checkpoint counts only when its implementation **and** validation evidence exist. This tracker reports progress. It has no authority over the scientific protocol (`PROTOCOL_STATUS.md`, docs 01–08, DECISION records). |
| **Completion** | **60% confirmed.** Checkpoints 1–6 complete with validation evidence; 7–10 not started. |

| # | Checkpoint | Status | Weight | Evidence |
|---|---|---|---|---|
| 1 | Workspace configuration and project analysis | ✅ Complete | 10% | Project analysis (baseline findings in doc 02 §2); local development configuration (not published) |
| 2 | Experimental architecture design and Phase 0 baseline approval | ✅ Complete | 10% | `docs/milestone2/01–08`, DECISION-001…008, baseline fingerprints, git initialised |
| 3 | Decisions and API specifications | ✅ Complete | 10% | `docs/api-contracts/` (v0.3; book fields final); §3 choices approved |
| 4 | Database schema and experimental dataset | ✅ Complete | 10% | Research dataset imported (DECISION-009): 103,063 books, source-to-DB hash verified independently, data freeze 24/24 PASS (§4) |
| 5 | Full application implementation | ✅ Complete | 10% | `benchmark/src/`; the five configurations served from the research data in LocalDB (§5) |
| 6 | Local testing and validation | ✅ Complete | 10% | **151/151 tests passed** (§6); live equivalence PASS on the research data |
| 7 | Instrumentation and telemetry (stats sidecar, runtime counters, DB-time aggregation, host logger, log export) | ⬜ Not started | 0 / 10% | doc 04 §4, doc 05 Phase 6 |
| 8 | JMeter plans, campaign runner and analysis pipeline | ⬜ Not started | 0 / 10% | doc 04 §1–§2, §5–§6; doc 05 Phases 7–8 |
| 9 | AWS infrastructure, deployment and capacity pilot (**researcher**) | ⬜ Not started | 0 / 10% | doc 05 Phases 9–10; needs AUTH-AWS-1 |
| 10 | Benchmark campaign, statistical analysis and replication package | ⬜ Not started | 0 / 10% | doc 05 Phases 11–13; needs AUTH-AWS-2 |

## 4. Task 4: database and dataset (complete, 2026-11-02)

| Deliverable | Status | Evidence |
|---|---|---|
| DDL for three service-owned databases, schemas, tables, indexes | ✅ Final | `benchmark/data/ddl/*.sql`; book table unchanged and marked FINAL; collation and compatibility level 160 pinned |
| Research dataset import | ✅ | `KaggleBooksImporter` (rules R1–R9). The archive and entry SHA-256 are pinned. **103,063 / 103,063 rows**, `book_id` = file order. |
| Source-to-database verification | ✅ | `catalog.book` canonical SHA-256 `dd40286a…5201`, identical from the importer, LocalDB and an independent re-implementation |
| Synthetic members and loans (fixed, recorded seed) | ✅ | Seed 20261101: 10,000 members, 100,933 loans, 0 orphans |
| Row counts, manifests, checksums, integrity verification | ✅ | `manifest.json` (source and rule statistics), `lms-bench db verify` **24/24 PASS** |
| Initialisation and reset scripts, instructions | ✅ | `scripts/db-init.ps1` (default: research dataset), `benchmark/README.md` §2–§2b |
| TEMPORARY fixture | ✅ Kept for tests only | `lms_it_*` databases, labelled `TEMPORARY_FIXTURE_NOT_RESEARCH_DATA` |
| Documented limitation | ✅ | DS-9: the first 100 books have 59% missing descriptions (31.9% overall) |

## 5. Task 5: application

All five services use .NET 10 and run on LocalDB: Catalog, Members, Loans, REST edge and GraphQL edge. Shared libraries: Contracts, Hosting (SP-1, host defaults, problem+json), Data, DomainClient and RequiredBytes. The five configurations are F-R, F-G, N-RC, N-RM and N-G. No responses are hard-coded: the oracle is read independently from the databases, and responses must equal it.

## 6. Task 6: tests actually executed (2026-11-02, TRX in `benchmark/artifacts/test-results/`)

| Suite | Executed | Passed | Failed | Covers |
|---|---|---|---|---|
| `Lms.UnitTests` | 96 | 96 | 0 | Parsing and validation, RFC 8785 (vectors, sorting, numbers, escapes), Required Bytes worked example (179/199/292/283) and decomposition, SP-1 serialization (SER-2..5), generator determinism and invariants, canonical encoding, contract-file conformance |
| `Lms.IntegrationTests` (real LocalDB) | 55 | 55 | 0 | Data freeze and tamper detection, DDL idempotency and constraints, domain API (paging, ordering, IDs, validation, 404), REST edge (representations, headers, errors, 502), GraphQL (schema vs the contract SDL, results, INVALID_ARGUMENT, UPSTREAM_FAILURE, GET and batching disabled), **all five configurations equivalent to the oracle**, Required Bytes identical within each workload, decomposition exact, compactness, C-6 keys, **internal-call counts identical** (1 flat / 3 nested), N-RM workflow, cross-stack serialization parity (SER-1), captured SQL |
| **Total** | **151** | **151** | **0** | |

Additional live evidence on the **research dataset** (services running on localhost; local pre-benchmark validation, not experimental results):
- `lms-bench equivalence`: PASS for all 5 configurations. Required Bytes flat 18,675, nested 9,560 (`docs/api-contracts/samples/kaggle-v3-local-prebenchmark/`).
- All 12 requests in `http/scenarios.http` executed with the statuses the contract specifies.
- A scripted re-import (`db-init.ps1`) reproduced identical canonical hashes.

## Open items affecting the next checkpoints

1. **AD-37:** HotChocolate `MaxConcurrentExecutions` default of 64 (GraphQL-only limiter). Proposed: unlimited.
2. **Tier 2 decisions** (doc 06 §2), including N, M and L and the flat record set, before Phase 9.
3. Checkpoints 7–10: telemetry, JMeter plans, runner and analysis, AWS pilot (researcher), campaign.
