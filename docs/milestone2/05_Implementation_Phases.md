# 05 — Implementation Phases

| | |
|---|---|
| Status | **DRAFT for researcher approval.** No phase has started. Revised in the refinement pass of 2026-11-01. |
| Rule | A phase starts only when its entry criteria hold. ✋ marks a researcher approval gate. |
| Zero-spend boundary | **Phases 0–9 incur no AWS expenditure.** They use no AWS credentials and make no `aws` / Terraform-apply calls. Phase 9 writes IaC *files* only. |
| AWS authorisation | Phase 10 (deploy + pilot) and Phase 11 (campaign) each need a **separate, explicit written authorisation** from the researcher, recorded as a DECISION file (`AUTH-AWS-1`, `AUTH-AWS-2`). Approving a design or a phase gate is never an AWS authorisation. |
| Decision tiers | The tiers are defined in [06](06_Methodology_Decisions_Required.md):<br>• **T1** before code;<br>• **T2** after local validation;<br>• **T3** in the pilot;<br>• **T4** paper only. |

| Phase | Work | Entry criteria | Exit criteria (verifiable) | Lead | M2 items |
|---|---|---|---|---|---|
| **0. Decisions and freeze** ✋ | Record T1 decisions. Run `git init` at the root (AD-14). Create the baseline manifest. | This document set approved | Every T1 item in 06 §1 has a `DECISION-*.md` that names the researcher and the date. `baseline_manifest.txt` matches `git ls-tree -r ff9b302`, and `git status --porcelain` in `existing-source/` is empty. | Methodology review | — |
| **1. Scaffolding** | `benchmark/` solution on .NET 10: `global.json`, `Directory.Packages.props`, `Lms.Hosting`, Dockerfiles, compose file with SQL Server, local build script | Phase 0 | `dotnet build` and an empty `dotnet test` pass. Images build locally. SDK and package versions are recorded (B-4 draft). | .NET architecture | B-4 |
| **2. Data** | DDL, dataset cleaning, seeded generator, canonical row encoding, `verify_data_freeze`, CRIR oracle and golden files | Phase 1; the Kaggle file is downloaded by the researcher or with approval; licence checked | Same seed gives an identical generator hash on two OSes. The DB content hash equals the generator hash. Orphans = 0. Target-set invariants hold. Golden files are committed with their SHA-256. | .NET architecture | A-4 |
| **3. Domain services** | Catalog, Members, Loans (EF Core, `AsNoTracking`, fixed projections, sorted batched ID queries), health endpoints, `Lms.Telemetry` | Phase 2 | Integration tests (Testcontainers) pass. Captured SQL is committed per query. | .NET architecture | — |
| **4. Edge services** | `Lms.Edge.Rest`, `Lms.Edge.GraphQL`, `Lms.DomainClient`. Serializer policy (AD-32). | Phase 3 | Tests pass. Internal-call counts and ID parameters per task are asserted. Serializer byte-equality passes. `docs/milestone2/symmetry_checklist.md` is complete, with file:line evidence for every row of doc 01 §5. | API implementation (controlled change procedure) | A-2, B-7, B-8 |
| **5. Equivalence** ✋ | Golden-response suite (doc 03 §4.4) against all five configurations. Over-fetching decomposition. | Phase 4 | Every check in doc 03 §4.4 passes for every configuration. `ActualBytes ≥ RequiredBytes`. C-6 is produced. | Equivalence validation | A-3, A-6, C-6 |
| **6. Instrumentation** | Stats sidecar, runtime counters, DB interceptor, host logger, log export | Phase 4 | TEL-1 to TEL-5 and TEL-7 pass locally (doc 04 §4.7). Overhead is below the AD-30 threshold. | .NET architecture; AWS infrastructure | B-6, C-2/C-3 format |
| **7. Load plans and runner** | Five plans, properties, schedule generator, campaign runner, RUNBOOK | Phases 5 and 6 | T-TX-1 to T-TX-7 pass (doc 04 §1.1), with the report committed. Smoke tests at 1–5 threads pass against the local stacks. The schedule has the expected row count. Deliberately wrong assertions fail. | Load-testing engineering | A-5, B-9 template, C-4 |
| **8. Analysis pipeline** | Ingest, metrics, primary statistics, sensitivity analysis S1–S4, cost model, figures, `run_all.py` | Can run in parallel with 4–7 | pytest passes on synthetic fixtures, including a published worked example for the van Elteren test. Table hashes are identical across two runs. `SENSITIVITY_PLAN.md` is drafted. | Statistical analysis | — |
| **— Local-validation gate** ✋ | Researcher review of the Phase 5–8 evidence | Phases 5–8 complete | Every **T2** item in 06 §2 is recorded | — | — |
| **9. Infrastructure drafts** | Terraform files (AD-13) for VPC, ECS, task definitions by digest, RDS, Cloud Map, log groups, JMeter EC2, cost tags | Local-validation gate | The drafts are reviewed. **Not applied.** | AWS infrastructure | B-1 to B-5 drafts |
| **10. Deploy and capacity pilot** ✋💲 | The researcher applies the IaC, loads the data, verifies the data freeze, runs the remote golden checks, runs TEL-1/2/3/5/6/8/9 on Fargate, and runs the capacity-only pilot (AD-12) | `AUTH-AWS-1` recorded | All **T3** items are recorded. All B-records are committed, with a commit time before any measured run. The remote golden checks match. Pilot data is archived in `experiment/pilot/` and never analysed. | Researcher (outputs reviewed in the AI-assisted development workflow) | B-1 to B-9 |
| **11. Campaign** 💲 | Warm-ups and measured runs according to the RUNBOOK | `AUTH-AWS-2` recorded. `SENSITIVITY_PLAN.md` and the data-freeze record committed. QA pre-flight PASS. | Run-log completeness as in doc 04 §2. Data-freeze checks pass each day. The archive is in write-once storage, and `SHA256SUMS` is committed. | Researcher | C-1 to C-6 |
| **12. Validation and analysis** | Conformance check, then the primary analysis and sensitivity analysis | Phase 11 | `VALIDATION-<campaign>.md` PASS. Outputs regenerate with identical hashes. | Results validation; reproducibility QA | Tables, figures |
| **13. Replication package** | C-5 README, manifest, workload table, mapping from reviewer comments to resolutions | Phase 12 | From a clean checkout, `run_all.py` fetches and verifies the raw archive and regenerates every table, with hashes identical to Phase 12 | Reproducibility reporting | C-5 |

**QA reviews.** Before the gates at Phases 5, 7, 10 and 12, a reproducibility QA review and a methodology review audit the evidence. Blocking findings stop the gate.

**Critical path:** 0 → 1 → 2 → 3 → 4 → 5 → 7 → local-validation gate → 9 → 10 → 11 → 12. Phases 6 and 8 run alongside it.

## Risks

| Risk | Mitigation |
|---|---|
| T-TX-2 shows that the include-processor flag does not behave as documented | Stop. Report to the researcher. AD-28 is re-decided before Phase 7 closes. |
| TEL-1 fails on Fargate (stats refresh is too slow) | Fall back to 5 s polling (still meets M2). Re-run TEL-1. Disclose. |
| SQL Server edition limits on RDS distort the DB tier | Choose the edition under B-2 knowingly. DB work is identical across configurations anyway. |
| The JMeter host saturates at 1,000 threads (N-RM extraction now counts toward task time) | P-2 plus per-core monitoring. Pilot. Split the generator (M2 §2.6.5). |
| Every level saturates for some configuration | The pilot sets task sizes before measurement, without any protocol comparison |
| Run volume | CSV only. Compressed archival with checksums. |
| A decision changes after Phase 4 | A new DECISION record, then Phase 5 and the later phases are re-run |
