# 07 — Final Design Review and Recommendations

| | |
|---|---|
| Date | 2026-11-01 |
| Status | **For researcher approval.** No code, infrastructure, experiment or result has been produced. |
| Scope of this pass | One focused refinement of docs 01–06, in response to the independent review (items A–G). The principal architecture is retained unchanged. |
| Approval state | Only DECISION-001 is recorded. Everything recommended here stays **unapproved** until you sign a DECISION file. |

## 1. Principal architecture: retained without redesign

The following are unchanged, and no methodological problem found in this pass requires reconsidering them:
- the new implementation in `benchmark/`, with `existing-source/` read-only;
- protocol-specific REST and GraphQL edge services over a shared, protocol-neutral domain-service implementation;
- five configurations and 25 cells;
- deterministic dataset generation;
- equivalent workloads;
- automated JMeter orchestration, instrumentation and reproducibility tooling;
- .NET 10.

## 2. Changes made in this pass

| Review item | Change | Where |
|---|---|---|
| **A. Scope and pushdown** | Added "Scope of the primary study". The study isolates client-facing API style over identical internal services. GraphQL projection pushdown, internal payload savings, selection-aware SQL and federation are **explicitly excluded**. A table maps each RQ to what it can and cannot support. A qualifier for the conclusion wording is set out. | 01 §1.1, §2 (AD-25) |
| **B. N-RM timing** | Recommended that extraction be **included** in task time. This is the literal M2 §2.3.4 interval, so it is not an amendment. Transaction Controller: parent sample off, include pre/post-processor duration **on**. JSON Extractors with sentinels, JSON Assertions, and failure short-circuit. A validation test plan, T-TX-1 to T-TX-7, against a fixed-delay local stub, separates transaction time, child time and extraction overhead. | 03 §3.5, 04 §1, §1.1 |
| **C. Required Bytes and equivalence** | Required Bytes now comes from a **Canonical Required-Information Representation (CRIR)** serialised with RFC 8785 JCS. The CRIR is built from a dataset oracle and must equal every configuration's normalised response. It is independent of envelopes and of the number of REST calls. Actual Bytes uses the **raw transmitted bodies**, which keeps the genuine differences, and the over-fetching decomposition is reported. A golden-response test suite checks counts, IDs, ordering, required fields, values, null semantics, envelope/errors, compactness and the bytes invariant. | 03 §4.2–§4.5 |
| **D. Statistics** | The primary analysis is unchanged. Added a **pre-registered, block-aware sensitivity analysis**:<br>• S1: repetition-paired Wilcoxon signed-rank;<br>• S2: day-stratified van Elteren;<br>• S3: two-stage day/repetition cluster bootstrap;<br>• S4: a descriptive day-variance share.<br>A robustness rule is stated. ≥ 3 days are recommended so that S3 and S4 are informative. | 04 §6.1 |
| **E. Dataset integrity** | `CHECKSUM_AGG` is replaced by `verify_data_freeze`. It covers:<br>• SHA-256 over canonical PK-ordered row encodings, which must **equal the generator's hash**;<br>• row counts;<br>• cross-database orphan checks;<br>• target-set invariants;<br>• a schema fingerprint.<br>It runs after load, every campaign day and after the campaign. | 03 §2.4, 04 §7 |
| **F. Placement and telemetry** | Daily redeploy plus per-cell warm-up is written up as **proposed amendment AM-3**. Telemetry feasibility was checked against AWS documentation (§4 below): what is confirmed, what is not documented, the resolution of each signal, the TEL-1 to TEL-9 acceptance tests, a 5 s fallback, and the measurement limitations to disclose. The host CPU model is read from `/proc/cpuinfo`, because the metadata does not contain it. `ClockDrift` is logged per task. | 01 §2, §6; 04 §2.5, §4.7 |
| **G. Cost** | The cost model uses allocated vCPU and memory × price, divided by throughput, giving the **modelled cost per successful task**. The RDS variant is reported separately. Data transfer comes from the applicable rule, with internet egress as a labelled scenario. Harness costs are excluded. **Actual AWS billing** is reported once per campaign as a reconciliation only, from cost-allocation tags, and is never divided per task. | 04 §8 |
| Decisions | Consolidated into four prioritised tiers. Each item has a recommended value, a rationale and its M2 status. A register lists amendments (AM-1 to AM-6) and clarifications (CL-1 to CL-7). | 06 |
| Phases | Phases 0–9 have zero AWS spend. A local-validation gate (T2) was added. Phase 10 needs `AUTH-AWS-1` and Phase 11 needs `AUTH-AWS-2`, separate written authorisations. TEL and T-TX tests were added to the exit criteria. | 05 |

## 3. Decisions recommended for researcher approval

The full list is in doc 06. These are the items that carry the most scientific weight:

1. **AD-01 + AD-25:** Option A topology, with projection pushdown excluded by design and the scope statement adopted.
2. **AD-07:** the batched 3-call N-RM chain, with the claim limited to the "minimum multi-call penalty".
3. **AD-34:** CRIR + JCS Required Bytes; raw-body Actual Bytes; golden tests (clarification CL-1).
4. **A-6:** your required-field lists. The placeholders are in 03 §4.1.
5. **AD-09:** full-column REST representation, with an offline lean-weight sensitivity analysis.
6. **AD-28:** extraction included in N-RM task time (CL-4), subject to T-TX validation.
7. **AD-32:** shared serializer policy.
8. **AD-36:** content-hash data-freeze verification.
9. **AM-3:** daily redeploy plus per-cell warm-up.
10. **AD-35:** the pre-registered sensitivity analysis (AM-5).

## 4. Telemetry verification against AWS documentation (checked 2026-11-01)

| Claim in the design | AWS documentation | Consequence |
|---|---|---|
| `/task/stats` on Fargate | **Confirmed** for platform version 1.4.0+. It returns Docker stats for all containers. | Primary CPU and memory source |
| Stats refresh rate ≤ 1 s | **Not documented.** Only "~1 second" before the first stats. | Decided by TEL-1. Fallback 5 s, which still meets M2. |
| Host CPU model in metadata | **Not present** | `/proc/cpuinfo`, verified by TEL-9 |
| Fargate clock sync | `ClockDrift` (Time Sync based) **is exposed** in the task metadata | Logged per run. TEL-6. |
| Container Insights below 60 s | **Not documented** | Not used for measurement |
| RDS Enhanced Monitoring at 1 s, SQL Server | **Confirmed** | Optional DB-host context |
| awslogs non-blocking + buffer | **Supported.** The default mode on Fargate was not found. | Set explicitly. TEL-8. |
| Fargate billing | **Confirmed:** per second, 1-minute minimum (Linux), from image pull to termination; allocated resources | Basis of 04 §8 |
| Same-AZ and cross-AZ transfer rules | **Not confirmed** by the automated check | You verify on the AWS pricing page (T3 #12) |

## 5. Remaining methodological risks

| Risk | Severity | Mitigation | Residual |
|---|---|---|---|
| The narrow scope (no pushdown, fixed internal protocol) limits how far the findings generalise to "GraphQL systems" | High (interpretation) | Scope statement, conclusion qualifier, Threats (01 §1.1, §9) | Inherent in the design. Must be stated. |
| N-RM batched chain plus single-AZ RTT means the multi-call penalty is a lower bound | Medium | Scope statement. RTT recorded (AD-27). | Readers may still expect the N+1 result |
| REST over-fetching level depends on the representation choice | Medium | AD-09 fixed a priori. Lean-weight sensitivity. Decomposition. | Disclosed |
| Run dependence (shared deployment, days) | Medium | AM-3, AM-5, ≥ 3 days | With few days the cluster-level inference is weak |
| `/task/stats` cadence on Fargate is unknown | Medium | TEL-1, 5 s fallback | Coarser CPU peaks if the fallback is used |
| Fargate hides host-level effects (steal, noisy neighbours) | Medium | Randomisation, daily redeploys, host model logging | Disclosed |
| JMeter extraction cost loads the generator for N-RM | Medium | T-TX-4 quantifies it. P-2 plus per-core flag. Split generator. | Part of the N-RM task by definition |
| The cache-hot fixed request | Low–Medium | M2 literal. Disclosed. | Limits generalisation to cold reads |
| The HotChocolate default cost analysis adds GraphQL work | Low | Recorded. The nested query is checked. | Disclosed as library behaviour |
| The dataset licence may restrict redistribution | Low–Medium | Checked before publication | May need a download script instead of the data |

## 6. Protocol amendments requiring sign-off

These are listed in 06 §5. In short:
- **Amendments:**
  - AM-1: analysis window excludes ramp-up and tail;
  - AM-2: `_dbq.csv` per-second aggregate;
  - AM-3: daily redeploy and warm-up.
- **Additions:**
  - AM-4: capacity pilot;
  - AM-5: sensitivity analysis;
  - AM-6: extra provenance data.
- **Clarifications:**
  - CL-1: CRIR/JCS;
  - CL-2: inclusion rule;
  - CL-3: CPU basis;
  - CL-4: N-RM timing;
  - CL-5: dry-run bytes;
  - CL-6: M2 over M1 for the cache condition;
  - CL-7: warm-up duration, CI target and the P-2 extension.

**None changes** the following:
- the five configurations or the 25 cells;
- the workloads or the equivalence rules;
- the metrics' definitions, other than AM-1's window;
- the primary statistical tests or the no-exclusion rule.

## 7. Exact implementation entry criteria (Phase 1 may start when all hold)

1. DECISION files exist, naming you and dated, for **every Tier 1 item** in 06 §1. That means 22 items, including A-6 field lists (or an explicit instruction to code against the placeholders, kept configurable).
2. DECISION files exist for **CL-1** (CRIR) and **AM-2** (`_dbq` format), because both shape code written in Phases 2–6.
3. `git init` has been run at the workspace root (AD-14). The `.gitignore` excludes `existing-source/`, `experiment/raw/` and secrets.
4. `docs/milestone2/baseline_manifest.txt` has been generated from `git ls-tree -r ff9b302`, and `git status --porcelain` in `existing-source/` is empty.
5. Local toolchain:
   - .NET SDK 10.0.x is present;
   - the Docker engine is running;
   - the Python analysis packages may be installed (see §8).
6. A standing instruction that Phases 1–9 must not use AWS credentials. This is already in the project rules, and the local tooling asks for confirmation before any `aws` command.

## 8. Phase 1 implementation readiness assessment

Checked on this machine, 2026-11-01:

| Item | State | Action |
|---|---|---|
| Design completeness for Phase 1 (scaffolding, shared hosting library, compose) | **Ready.** Doc 01 §5 and doc 02 §4 specify it. | — |
| Tier 1 decisions | **Not recorded** (0 of 22) | Researcher |
| .NET 10 SDK | **Present** (10.0.401) | — |
| Docker | **The CLI is present (28.1.1), but the engine is not running.** The Docker Desktop Linux engine pipe was not found. | Start Docker Desktop before Phase 1 local runs |
| SQL Server container image | Not pulled (a download) | It is pulled at the start of Phase 1, after you approve the download |
| Python | 3.10. `numpy` and `pytest` are present. **`scipy`, `pandas`, `statsmodels` and `psutil` are missing.** | Install the pinned versions in Phase 8 (needs your approval) |
| Java / JMeter | Java 23 on PATH (JDK 17 also installed). **JMeter is not installed.** | Phase 7. The download needs your approval, with the version pinned. |
| Git | 2.49. The workspace root is **not** a repository. | `git init` (entry criterion 3) |
| Kaggle dataset | Not downloaded. The licence has not been checked. | Phase 2. You download it, or approve the download. |

**Verdict.** The design is **ready for Phase 1 once the Tier 1 decisions are recorded** and the Docker engine is running. No design gap blocks scaffolding. Phases 1–8 need no AWS spend. The first AWS action is Phase 10, which needs `AUTH-AWS-1`.

## 9. Prioritised approval checklist

1. ☐ **Architecture and scope:** AD-01 (Option A) and AD-25 (pushdown excluded), together with the scope statement in 01 §1.1.
2. ☐ **Nested design:** D-2 (both nested REST variants), AD-07 (batched 3-call chain), AD-10 (embedded composite).
3. ☐ **Required information:** A-6 field lists, AD-34 (CRIR/JCS, CL-1), AD-09 (full-column REST representation).
4. ☐ **Implementation fairness:** AD-32 (serializer policy), AD-05 (HTTP/1.1 wire settings), AD-04 (.NET 10), AD-23 (controllers).
5. ☐ **Data:** AD-08 (dataset), AD-11 (DB per service), AD-36 (data freeze), AD-15 (fixed request).
6. ☐ **Scope of extras:** AD-03 (omit services), D-3 (memory and DB logs), AM-2 (`_dbq` aggregate), D-4a (auth toggle built), D-5 (no writes).
7. ☐ **Workspace:** AD-14 (`benchmark/` + `git init`).
8. ☐ *Later, Tier 2 after local validation:* AD-28/CL-4, AM-1, CL-2, AM-5, AD-20, AD-30a, AD-24, sizes, D-8, P-1, P-4.
9. ☐ *Later, Tier 3, only with AUTH-AWS-1:* AM-3, AM-4, B-items, P-2, P-3, D-7 and days, AD-33, D-4b, prices; then AUTH-AWS-2.
