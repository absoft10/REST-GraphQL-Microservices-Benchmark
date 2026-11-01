# 06 — Methodology Decisions Required

| | |
|---|---|
| Status | **Open: awaiting researcher decisions.** Consolidated in the refinement pass of 2026-11-01. |
| Recorded so far | Only **DECISION-001** (the baseline is reference only; the redesign is permitted; assets preserved). Your acceptance of the principal architecture "as a promising basis" (2026-11-01) is **not** recorded as approval of any item below. |
| How to answer | Per item, reply "approve recommended", give a different value, or "defer". Each answer becomes `docs/decisions/DECISION-<NNN>-<slug>.md` with your name and the date. **Nothing below is approved until that file exists.** |

> **Phase 0 update (2026-11-01).** All 22 Tier 1 items are now resolved: approved, or approved with provisional elements.
> - Records: DECISION-002 … DECISION-008.
> - Item-by-item mapping: doc 08 §1.
> - The O-5 value in row 9 was **replaced** by policy SP-1 in DECISION-005 (UnsafeRelaxedJsonEscaping was withdrawn).
> - Live status of everything still open or provisional: **`PROTOCOL_STATUS.md`**. The tables below are kept as the decision basis.

**M2 column legend:**
- **No:** within M2 as written.
- **Clarif.:** fixes an ambiguity in M2. Needs sign-off as a clarification.
- **Amend.:** changes M2. Needs sign-off as an amendment.
- **Add.:** an addition beside the M2 protocol that changes nothing in it.

Detailed options and alternatives for each item are in the referenced document section.

---

## 1. Tier 1: must be approved before writing code (gate for Phase 0 → 1)

Listed in priority order (impact on what the code must be).

| # | ID | Decision | Recommended value | Rationale | M2 | Ref |
|---|---|---|---|---|---|---|
| 1 | AD-01 | System topology | **Option A:** protocol-neutral domain tier + REST/GraphQL edge services | Identical DB and internal work, so differences are attributable to the client-facing API style (R2-3, R1-7) | No | 01 §2 |
| 2 | AD-25 | Projection pushdown | **Excluded by design.** A fixed full-row domain contract. Scope statement as in 01 §1.1. | Keeps internal work identical. The narrower claim is stated explicitly. | No | 01 §1.1, §2 |
| 3 | D-2 | Both nested REST variants | **Yes** (25 cells) | Required by the design and requested by R2 point 3 | No (M2 proposal) | M2 §6 |
| 4 | AD-07 | N-RM chain | **(i) batched 3-call chain** for all primary N-RM cells, with the scope statement "minimum multi-call penalty, not classic N+1" | Same internal calls as RC and G. (ii) multiplies DB load. | No (sets B-8) | 03 §3.5 |
| 5 | A-6 | Required field lists | **Researcher supplies them.** Placeholders: flat `bookId, title, authors, category, price, publishYear`; nested `memberId, firstName, lastName / loanId, loanDate, returnDate / bookId, title, authors`. Primary keys must be included. | They define the GraphQL queries, the CRIR and the golden files | No | 03 §4.1 |
| 6 | AD-34 | Canonical required information (CRIR) | **CRIR + RFC 8785 (JCS) + an oracle cross-check.** Required Bytes = `len(JCS(CRIR))`. Actual Bytes = raw transmitted bodies. | Protocol-, envelope- and call-count-independent. Keeps the genuine payload differences. | **Clarif.** of §2.3.3 | 03 §4.2–4.3 |
| 7 | AD-09 | REST representation | **(a) all stored columns**, plus (c) an offline sensitivity analysis at a lean weight | Conventional REST. Fixed before any data is seen. The design-artefact nature of the result is disclosed. | No | 03 §3.1 |
| 8 | AD-10 | Composite content | **Embedded full representations** | Consistent with AD-09 | No | 03 §3.4 |
| 9 | AD-32 | Serializer policy | **One shared `JavaScriptEncoder` policy. `DateOnly` for DATE. Pinned decimal and date scalars.** A byte-equality test. | The default encoders differ in ways unrelated to the protocol | No | 01 §5 |
| 10 | AD-04 | Runtime | **.NET 10 LTS** (SDK 10.0.401 installed). GC and DATAS settings pinned and recorded. | .NET 8 support ends 10 Nov 2026 | No | 01 §5 |
| 11 | AD-05 | Wire settings | **HTTP/1.1 keep-alive, Kestrel `Http1` explicit, no TLS in the VPC, no compression, GraphQL POST JSON, identical `Accept`, `Accept-Encoding: identity`** | One configuration on every hop. JMeter HttpClient4 has no h2. | No (sets B-9 values) | 01 §5, 04 §1 |
| 12 | AD-08 | Dataset | **Kaggle books [33] + seeded synthetic members and loans.** The licence is checked before publication. | Continuity with the paper. Deterministic. | No | 03 §2.3 |
| 13 | AD-11 | Storage | **One database per service on one RDS instance** | Microservice ownership. Integrity is verified by tooling. | No | 03 §2.1 |
| 14 | AD-36 | Data-freeze verification | **SHA-256 content hashes of canonical ordered exports, matched to the generator hash, plus row counts, orphan counts, target-set invariants and a schema fingerprint.** Not `CHECKSUM_AGG`. | Reproducible proof of unchanged *and* correct data | No | 03 §2.4 |
| 15 | AD-15 | Fixed request | **Fixed request per configuration, as M2 specifies.** Disclose that the read path is cache-hot. | Literal M2 | No | 03 §1.2 |
| 16 | AD-03 | Notification and Auth services | **Omit both from the measured system** | No role in reads. The old Notification service was empty. | No | 01 §3 |
| 17 | D-4a | Build auth support | **Yes:** symmetric JWT-validation middleware in both edges, **off by default** | Cheap to build now. Running it is decided in T3 (D-4b). | No | 01 §3 |
| 18 | D-5 | Mixed read-write | **No.** State the exclusion with its reason: writes conflict with the data freeze and "no restart" (W-13). | Needs a separate design. Scope. | No (M2 "your call") | 06 W-13 |
| 19 | D-3 | Memory and DB-time logs | **Yes** | Separates resolver cost from DB cost (R2-5) | No (M2 proposal) | 04 §4 |
| 20 | AD-21 | `_dbq.csv` granularity | **Per-second aggregate per `query_id`:** `count, mean_ms, p95_ms, max_ms` | Per-command logging at level 1000 would perturb the system | **Amend.** C-3 (AM-2) | 04 §4.3 |
| 21 | AD-23 | REST style | **`[ApiController]` controllers** | Continuity and common practice. Disclosed. | No | 02 §3 |
| 22 | AD-14 | Location and version control | **`benchmark/`, plus `git init` at the workspace root.** `existing-source/` and `research-documents/` stay untouched. | Provenance (commit SHAs per run) | No | 02 §4 |

## 2. Tier 2: can be finalised after local validation (gate before Phase 9)

All local, at no AWS cost. Each needs the named local evidence first.

| # | ID | Decision | Recommended value | Evidence needed | M2 | Ref |
|---|---|---|---|---|---|---|
| 1 | AD-28 | Extraction time in the N-RM task | **Include** (Transaction Controller "include pre/post-processor duration" **on**) | T-TX-1 to T-TX-7 | **Clarif.** (the literal §2.3.4 interval) | 03 §3.5, 04 §1.1 |
| 2 | AD-17 | Analysis window | **t0 = first sample timestamp. Window `[t0 + ramp-up, t0 + duration − tail]`. Tasks assigned by start time.** Plus a stationarity check. | Smoke runs show ramp-up and tail effects | **Amend.** §2.4 (AM-1) | 04 §5 |
| 3 | AD-31 | Inclusion and replacement rule | **Analyse every run unless it is replaced for an M2 cause** (§4.5 external fault, §2.6.5 P-2 re-run). Derived mechanically. | — | **Clarif.** §4.1 vs §2.6.5 | 04 §2.4 |
| 4 | AD-35 | Block-aware sensitivity analysis | **Pre-register S1 to S4.** The primary analysis is unchanged. Robustness rule as stated. | Pipeline tests on synthetic data | **Add.** (AM-5) | 04 §6.1 |
| 5 | AD-20 | CPU % basis | **% of allocated task vCPU from payload timestamps.** Δ ≤ 0 dropped and counted. | TEL-2, TEL-4 locally | **Clarif.** B-6 | 04 §4.1 |
| 6 | AD-30a | Instrumentation overhead threshold | **Proposed:** sidecar mean ≤ 1% of task vCPU, and service-CPU delta (sidecar on vs off) ≤ 2 percentage points at fixed load | TEL-5 locally | No | 04 §4.7 |
| 7 | AD-24 | GraphQL server settings | **HotChocolate defaults, including cost analysis**, after checking that the nested query passes the limits. All settings recorded. Persisted queries off. | A local run of the nested query | No | 01 §5 |
| 8 | AD-18 | Warm-up duration | **= P-3** | — | **Clarif.** §2.2 | 04 §2 |
| 9 | AD-19 | Bootstrap CI target | **The cell median.** CIs on differences come only through S3. | — | **Clarif.** | 04 §6 |
| 10 | AD-27 | Client RTT | **Record and disclose. No emulation.** | Local RTT measurement method | No | 03 §3.5 |
| 11 | — | Sizes N, O, M, L, dataset volumes | **Placeholders N = 100, O = 0, M = 10, L = 5; 10,000 members; about 100,000 loans.** Confirm after local payload sizes are seen. **Do not** look at REST/GraphQL timing when choosing them. | Local golden bodies (sizes only) | No | 03 §1 |
| 12 | D-8 | Pause between runs | **60 s** (M2 proposal) | — | No | M2 §6 |
| 13 | P-1 / P-4 | Saturation thresholds | **5% / 5%** (M2 proposals) | — | No | M2 §6 |
| 15 | **AD-37** (new, 2026-11-01) | HotChocolate `MaxConcurrentExecutions` (library default **64**; REST has no limiter) | **Proposed: 0 (unlimited)**, so that neither stack has a server-side admission limit. Alternative: keep 64 and disclose it. | Found during implementation (api-contracts CHANGELOG C-1). The value directly affects GraphQL queueing at levels ≥ 100. | **Clarif.** of neutrality (doc 01 §1) | `src/Lms.Edge.GraphQL/Program.cs` |
| 14 | AD-02 / AD-06 / AD-13 | Deployment shape for the IaC drafts | **A1 two stacks; direct ingress through Cloud Map; Terraform** | — | No | 01 §2, §6 |

## 3. Tier 3: can be finalised during an authorised AWS capacity pilot

Nothing in this tier runs without **AUTH-AWS-1**. The campaign needs **AUTH-AWS-2**.

| # | ID | Decision | Recommended value | Rationale | M2 | Ref |
|---|---|---|---|---|---|---|
| 1 | AUTH-AWS-1 + AD-12 | Authorise deployment and a capacity-only pilot | **Approve when T1 and T2 are complete.** The pilot examines generator capacity, error behaviour, pool saturation, steady state and TEL tests, and **never** a REST/GraphQL comparison. | Sets B-values before measurement without protocol bias | **Add.** (AM-4) | 04 §9 |
| 2 | AD-26 + AD-29 | Daily redeploy + per-day warm-up per cell | **Approve AM-3** (may be decided earlier) | Breaks the alignment between placement and protocol | **Amend.** §2.6.4 (AM-3) | 04 §2.5 |
| 3 | B-1 | Task sizes | **Pilot-informed. Identical per role across stacks. 1 task per service.** | Avoid saturation at every level, or none | No | 01 §6 |
| 4 | B-2 / B-3 | RDS, region, AZ | **Single-AZ, shared by both stacks. Region: confirm `ap-southeast-1` (baseline region) or another. One AZ for everything.** Edition chosen with its limits stated. | Continuity and variance control | No | 01 §6 |
| 5 | B-5 / B-9 | Generator and load profile | **Non-burstable instance in the same AZ. Ramp-up and timeouts from the pilot.** Keep-alive on, no timers, HTTP/1.1. | Generator must not be the bottleneck | No | 04 §1 |
| 6 | P-3 | Run duration | **Pilot-informed.** It must give a steady-state window of several minutes after ramp-up. | M2 §6 condition | No | 04 §5 |
| 7 | P-2 | Generator CPU threshold | **70% host average (M2), with per-core max ≥ 90% or GC pause > 1 s per minute also flagged** | Host average hides single-core saturation | Clarif. (extends the flag) | 04 §4.5 |
| 8 | D-7 + days | Repetitions and spread | **R = 30 if the pilot-based time budget allows, otherwise R ≥ 10. Spread over ≥ 3 days.** | R2 asked for 30. S3 needs ≥ 3 days. Both are within M2. | No | M2 §3, 04 §6.1 |
| 9 | AD-33 | Pool and connection limits | **Pilot-informed explicit values, identical in all services, with pool waits logged** | Avoid hidden pool saturation | No | 01 §5 |
| 10 | AD-30b | Clock-skew tolerance | **≤ 10 ms** between the generator offset and the tasks' `ClockErrorBound` (proposed). Confirm against TEL-6. | Log slicing at 1 s resolution | No | 04 §4.7 |
| 11 | D-4b | Run the auth-enabled reduced set | **Only if budget allows: levels 100 and 1000, all five configurations** (M2 allows this). Otherwise state the exclusion. | R2-4 | No | M2 §6 |
| 12 | D-6 | Price table | **Researcher supplies dated on-demand prices for the region.** The model is as in 04 §8. | Cost RQ | Clarif. (definitions) | 04 §8 |
| 13 | AUTH-AWS-2 | Authorise the measurement campaign | **After the pilot review and a QA pre-flight PASS** | Explicit spend authorisation | — | 05 Ph11 |

## 4. Tier 4: paper-only decisions (Milestone 3)

| # | Item | Recommended handling |
|---|---|---|
| 1 | Scope qualifier in RQs and conclusions | Every claim reads "client-facing API style over identical internal services". The RQ text stays (M1 Table 3). Threats list the excluded GraphQL mechanisms (01 §1.1). |
| 2 | D-1 1–5 scoring | **Remove** (M1 default) |
| 3 | Cost reporting | Report the modelled cost per successful task per cell, and the actual campaign bill separately as a reconciliation only (04 §8) |
| 4 | Over-fetching reporting | Report the decomposition (03 §4.3) and the AD-09(c) lean-weight sensitivity |
| 5 | R1-8 HTTP caching | A qualitative paragraph. State that caching was disabled and verified (B-7). |
| 6 | R1-9, R2-8 Prior studies | A critical comparison in §2.4 and §5. Depends on replacing references [25] and [29]. |
| 7 | R1-10 Table 5 wording | Restrict it to the tested conditions and link it to Threats |
| 8 | R1-11 Repository URL | Publish a correctly spelled replication repository |
| 9 | R1-12 References | Needs **A-1**, the reviewer's audit workbook, which is not in the workspace |
| 10 | A-5/A-6 historical | If the original plans or method exist, explain the old 3–12% figure. Otherwise state that it cannot be recovered. |
| 11 | W-12 Fig. 1 | Redraw for the new architecture |
| 12 | Threats structure | Mitigated by design versus residual (01 §9) |

## 5. Protocol amendments and clarifications requiring sign-off

| Code | Item | Kind | M2 text affected |
|---|---|---|---|
| AM-1 | Analysis window excludes ramp-up and tail (AD-17) | Amendment | §2.4 "per second of run time" |
| AM-2 | `_dbq.csv` per-second aggregate (AD-21) | Amendment | C-3 "query_id and duration_ms" |
| AM-3 | Daily redeploy + per-cell warm-up per day (AD-26, AD-29) | Amendment | §2.6.4 "no restart"; extends the §2.2 redeploy rule |
| AM-4 | Capacity-only pilot (AD-12) | Addition | — |
| AM-5 | Block-aware sensitivity analysis S1–S4 (AD-35) | Addition (non-primary) | — |
| AM-6 | Extra recorded data: `run_provenance.csv`, `Connect` column, extra `_jmeterhost` columns, data-freeze records, cost tags | Addition | C-files unchanged |
| CL-1 | CRIR + JCS for Required Bytes; raw bodies for Actual Bytes (AD-34) | Clarification | §2.3.3 |
| CL-2 | Inclusion and replacement rule (AD-31) | Clarification | §4.1 vs §2.6.5 / §4.5 |
| CL-3 | CPU % of allocated vCPU (AD-20) | Clarification | B-6, C-2 |
| CL-4 | N-RM task time includes extraction (AD-28) | Clarification | §2.3.4 |
| CL-5 | Dry-run `bytes` compared with headers + body (W-15) | Clarification | §2.5 |
| CL-6 | M2 "no restart" governs over M1 "same cache condition" (W-4) | Clarification | M1 §7 vs M2 §2.6.4 |
| CL-7 | Warm-up = P-3; CI target = median; P-2 per-core extension | Clarification | §2.2, §4, P-2 |

## 6. Weaknesses and ambiguities found (status)

| # | Issue | Resolved by |
|---|---|---|
| W-1 | Ramp-up and throughput denominator | AM-1 |
| W-2 | `_dbq.csv` format | AM-2 |
| W-3 | CPU % basis | CL-3 |
| W-4 | M1 vs M2 cache condition | CL-6 |
| W-5 | Warm-up duration | CL-7 |
| W-6 | Bootstrap CI target | CL-7 |
| W-7 | Primary keys in A-6 | T1 #5 |
| W-8 | The multi-call design dominates N-RM | AD-07 + scope statement |
| W-9 | The REST representation's weight | AD-09 |
| W-10 | Non-independent runs | AM-3, AM-5 |
| W-11 | Cache-hot DB path | AD-15 disclosure |
| W-12 | Fig. 1 | Tier 4 |
| W-13 | Writes vs data freeze | D-5 = No |
| W-14 | M2 inclusion contradiction | CL-2 |
| W-15 | `bytes` vs body length | CL-5 |
| W-16 | Transaction timing default | CL-4 |
| W-17 | Placement aligned with protocol | AM-3 |
| W-18 | Serializer defaults differ | AD-32 |
| W-19 | Required Bytes depended on the response shape and call count | CL-1 |
| W-20 | `CHECKSUM_AGG` is not a proof of an unchanged dataset | AD-36 |

**Superseded IDs** (kept so that older references resolve):
- AD-16 (byte-calculation details) → AD-34.
- AD-22 (run independence) → AD-35 and AM-3.
- AD-29 (per-day warm-up) → merged into AM-3.
- AD-30 split into AD-30a (overhead threshold) and AD-30b (clock-skew tolerance).

## 7. Already confirmed (no action needed)

- The M2 core:
  - 25 cells, warm-up scheme, R ≥ 10;
  - equivalence rules, metrics and Table 5 fields;
  - run order, ≥ 2 days, closed loop, P-2 rule;
  - MWU + Cliff's δ + Holm, no performance exclusion, saturation label, bootstrap with 10,000 resamples (M1 §6).
- DECISION-001.
