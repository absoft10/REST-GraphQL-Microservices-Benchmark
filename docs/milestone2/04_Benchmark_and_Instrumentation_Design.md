# 04 — Benchmark and Instrumentation Design

| | |
|---|---|
| Status | **DRAFT for researcher approval.** Revised after the load-testing and reproducibility reviews. |
| Date | 2026-11-01 |
| Confirmed requirements | M2 §1.3 (Group C outputs), §2.2 (repetitions), §2.4–§2.6 (metrics, formats, run order), §3 (count), §4 (statistics); M1 §6 (bootstrap, effect-size labels) |
| Execution | **The researcher executes all AWS runs** (M1 §7). The plans, the runner and the analysis are prepared in the AI-assisted development workflow, which also validates what they deliver. |

"Verify at implementation" marks behaviour that depends on a JMeter, .NET or AWS version. It is checked against the pinned versions before Phase 7 closes.

---

## 1. Load generation (JMeter)

| Setting | Value | Source |
|---|---|---|
| Model | Closed loop: level *n* = *n* threads, each waits for its response | [REQ M2 §2.6.6] |
| Duration | Duration-based scheduler, identical for every cell = **P-3** | [REQ M2 §2.6.4] |
| Ramp-up | Per level, identical across configurations = **B-9** | [REQ M2 B-9] |
| Think time | None (no timers) unless B-9 sets one | [PROPOSED] B-9 |
| Sampler | HTTP Request, HttpClient4, `Use KeepAlive` on, "Same user on each iteration" on | [PROPOSED] B-9 |
| Connection semantics | Pinned in `user.properties`: `httpclient4.retrycount=0`, `httpclient4.time_to_live`, `httpclient4.validate_after_inactivity`, `httpclient.reset_state_on_thread_group_iteration`. The `Connect` time is kept in the CSV, so that connection reuse can be shown (count samples with connect > 0). Exact defaults: verify at implementation. | [PROPOSED] B-9 |
| HTTP version | HTTP/1.1 on every configuration. HttpClient4 has no h2. The new edges are configured `Http1` explicitly (doc 01 §5); the baseline's HTTP/2-only GraphQL setting is not carried over. | [DECISION AD-05], B-9 |
| Headers | An identical `Accept` and `Accept-Encoding: identity` on every sampler. GraphQL adds `Content-Type: application/json`. | [PROPOSED] |
| Timeouts | Connect and response timeouts identical across configurations = B-9 | [REQ M2 B-9] |
| Client caching | No HTTP Cache Manager and no Cookie Manager | B-7 |
| Assertions | 2xx status; record count through a **JSON Assertion** (`$.length()` or the equivalent path; avoids Groovy cost); GraphQL also asserts no `errors` key; N-RM asserts per call, with a sentinel on the extractor default | [REQ M2 §2.5]; mechanics [PROPOSED] |
| N-RM | Transaction Controller, "Generate parent sample" **off**. "Include duration of timer and pre-post processors in generated sample" **on** (AD-28, recommended; see §1.1). No timers inside the chain. On sample error: Start Next Thread Loop. Task metrics use the transaction rows. | [REQ M2 §2.5, §2.3.4]; settings [PROPOSED] |
| Output | CSV with the Table 5 fields plus `Connect` | Table 5 [REQ M2 §2.5]; `Connect` [PROPOSED] |
| Properties | A pristine, versioned property set passed with `-p` (or a hashed `bin/user.properties`), because `-q` only adds to the installation's defaults. Hashes of `jmeter.properties`, `system.properties`, `user.properties` and the plugin list are recorded. | [PROPOSED] |
| Mode | Non-GUI (`jmeter -n`), no listeners besides `-l`, autoflush off | [PROPOSED] |
| JVM | `-Xms` = `-Xmx` (B-5), `-Xss256k`, GC logging on. `ulimit -n` ≥ 8192. A non-burstable instance type in the same AZ. | [PROPOSED] B-5 |
| Version | JMeter and Java versions pinned (B-5). Every "verify at implementation" item is re-checked against them. | [REQ M2 B-5] |

**Plans.** There is one parameterised plan per configuration, driven by `-Jlevel -Jrampup -Jduration -Jhost -Jport -Jcell`. Cells differ only in properties. The SHA-256 of each plan and of each GraphQL request body is recorded per run.

**Mandatory dry run** [REQ M2 §2.5; checks extended]:
1. Run N-RM with one thread. Confirm that both the transaction rows and the child rows are present.
2. Compare transaction `bytes` with the sum of the child `bytes`. Whether JMeter sums them is version-dependent; verify.
3. Compare one child's `bytes` with its headers plus wire body (M2 asks for "body length"). `bytes` includes headers, so it never equals the body alone.
4. Compare transaction `elapsed` with the sum of the child `elapsed` (AD-28).

Over-fetching is always computed from bodies, never from `bytes`.

### 1.1 N-RM task timing: definition and validation [AD-28]

**Definition.** The N-RM task time is the wall-clock interval from the start of call 1 to the end of call 3, as M2 §2.3.4 defines it. It includes:
- the network and server time of each call;
- JMeter's response processing and assertions for calls 1–2, verified by the tests below;
- the JSON Extractors that derive the IDs for calls 2 and 3;
- any scheduling gap between samplers in the thread.

The chain is structured as follows:

```
Transaction Controller "N-RM"  (parent sample off, include pre/post-processor duration ON)
 ├─ HTTP GET members      → JSON Assertion (count = M) → JSON Extractor memberIds (default = __NONE__) → JSON Assertion ids ≠ __NONE__
 ├─ HTTP GET loans?memberIds=${memberIds}      → JSON Assertion (count = M·L) → JSON Extractor bookIds → sentinel check
 └─ HTTP GET books?ids=${bookIds}              → JSON Assertion (count = M·L)
```

The extractors use *Match No.* = −1 with "Compute concatenation var" (`_ALL`), so that a comma-joined ID list is built without a script. Exact property names are verified at implementation. Assertions are JSON Assertions, not JSR223/Groovy.

**Validation test plan.** This runs locally, in Phase 7, at no AWS cost. It runs against a **stub server** with fixed, known delays (a small local HTTP server that returns the golden bodies after sleeping d₁, d₂, d₃), so the expected values are known in advance.

| Test | Set-up | Pass criterion |
|---|---|---|
| T-TX-1 Composition | 1 thread, 200 iterations, delays d = 50/50/50 ms | For every transaction: `elapsed_tx ≥ Σ elapsed_child`, and `elapsed_tx − Σ elapsed_child` is ≥ 0 with median < 5 ms |
| T-TX-2 Processor inclusion | As T-TX-1, plus a test-only JSR223 post-processor that sleeps 20 ms on call 1 | `elapsed_tx` rises by 20 ± 2 ms against T-TX-1, while the child `elapsed` values stay the same. This proves the include flag works. |
| T-TX-3 Flag off (control) | As T-TX-2 with the include flag off | `elapsed_tx` does **not** rise by 20 ms. This documents JMeter's default behaviour. |
| T-TX-4 Extraction overhead | The real plan (no test sleeps) with d = 0 ms, run with the flag on and then off | The flag-on minus flag-off difference = the client-side extraction and assertion overhead. Reported as median and p95 per transaction, and at 1 thread and at 50 threads. |
| T-TX-5 Failure propagation | The stub returns 500, or a wrong count, on call 2 | Call 3 is not issued. The transaction is `success=false`. The `failureMessage` names call 2. |
| T-TX-6 Bytes | One transaction | Transaction `bytes` vs Σ child `bytes`, and child `bytes` vs headers + body, are recorded. The rule used for the bandwidth metric is fixed from the result. |
| T-TX-7 Timestamps | One transaction | The transaction `timeStamp` equals the first child's `timeStamp` (within 1 ms), given `sampleresult.timestamp.start=true` |

All results go to `experiment/jmeter/validation/TX-VALIDATION.md` and are pinned to the JMeter version. If T-TX-2 fails, AD-28 cannot be implemented as written, and the issue goes back to you.

On AWS, the extraction cost also loads the JMeter host CPU. This is why the P-2 rule and the per-core maximum (§4.5) matter more for N-RM.

---

## 2. Run orchestration [PROPOSED]

`experiment/runner/` is a Python tool that runs on the JMeter host. **Only the researcher runs it.**

### 2.1 Schedule

`make_schedule.py --seed S --reps R` uses `numpy.random.Generator(PCG64)` at a pinned NumPy version.
- **Warm-ups first:** the 25 warm-ups come first [REQ M2 §2.2]. Their order is seeded random [PROPOSED].
- **Measured runs:** for each repetition, the order of the five levels is shuffled. Within each level, the order of the configurations is randomised [REQ M2 §2.6.2].
- **Days:** whole repetitions are assigned to days, so every cell runs on every day [PROPOSED].

The fields mean the following:
- `order_index` is the position of the run's **level** within its repetition's level sequence (1–5), as M2 says: "record the sequence in order_index".
- `global_seq` gives the global position, and goes in the provenance file.

### 2.2 Per-run steps (`run_campaign.py --schedule … --day D`)

1. **Pre-flight.**
   - Call `GET /health` on the target stack. This is outside the measurement window.
   - Read the task metadata: task ARN, `StartedAt`, image digest and `ClockDrift`. Also read the host CPU model that the sidecar logged from `/proc/cpuinfo`.
   - If any of these changed since the previous run on that stack, an unnoticed replacement has occurred. In that case, insert one warm-up for the cell [REQ M2 §2.2] and flag the run.
2. **Start logging.** Start the host logger and record the `chronyc tracking` offset.
3. **Run JMeter.** Record `start_utc`, run `jmeter -n …`, then record `end_utc`.
4. **Check output.** Stop the logger. Validate the `.jtl` header, and check that the file is non-empty.
5. **Record.** Append the run-log row (C-4 columns exactly) and the `run_provenance.csv` row.
6. **Pause** for D-8.

### 2.3 Run provenance

`run_provenance.csv` [PROPOSED] is keyed by `run_id`. It extends C-4 without altering it. Its columns are:
- `global_seq` and `day`;
- `stack_commit_sha`;
- the image digest of each service;
- the task-definition revisions and task ARNs;
- the host CPU model of each task;
- `plan_sha256`, `request_body_sha256`, `properties_sha256` and `schedule_sha256`;
- `jmeter_version`, `java_version` and the JVM flags;
- the generator instance type and AZ;
- `clock_offset_ms`;
- `p2_flag` and `replaces_run_id`.

### 2.4 Naming, repeats and inclusion

The full M2 §2.5 grammar applies:
- `<cell>_rep<NN>.jtl` for measured runs and `<cell>_warmup.jtl` for warm-ups.
- A repeated measured run adds `_r2`, `_r3`, and so on.
- A repeated warm-up adds `_2`, `_3`, and so on. Warm-ups after a redeploy are numbered the same way.
- A level re-run under the P-2 rule (M2 §2.6.5) goes into a separate generator campaign directory (`raw/<campaign>/gen2/`), keeping its stems. `replaces_run_id` links each new run to the run it replaces.

**Inclusion rule [DECISION AD-31].** M2 §4.1 says "every measured run enters the analysis". M2 §2.6.5 and §4.5 say that repeated or re-run runs replace the originals, whose files stay archived. The proposed rule reconciles the two:
- A run is *analysed* unless another run's `replaces_run_id` points to it.
- Replacement is allowed only for the M2 causes: an external fault (§4.5) or a P-2 level re-run (§2.6.5). It is never allowed for performance reasons.
- `analysis/` derives the inclusion set mechanically from the run log and the provenance file, and reports it per cell.

### 2.5 Redeploys and days

Nothing is restarted between runs within a day [REQ M2 §2.6.4].

**Proposed protocol amendment AM-3 (AD-26(b) + AD-29), which needs your sign-off.**
- **Change:** at the start of every campaign day, both stacks are redeployed: new tasks from the same digests. Then one warm-up run is executed per cell (25 runs, named `<cell>_warmup_<K>`) before that day's measured runs.
- **Why:** under A1 without redeploys, host placement stays perfectly aligned with protocol for the whole campaign. A daily redeploy turns each day into an independent placement draw for both stacks.
- **Relation to M2:** this uses M2 §2.2's own rule ("if a cell is redeployed, one further warm-up run precedes its next measured run") and applies it daily. It amends §2.6.4 only by adding planned redeploys **between** days. Restarts within a day remain forbidden.
- **Cost:** 25 extra warm-up runs per additional day.
- **Recorded:** task ARNs, `StartedAt`, host CPU model and `ClockDrift`, for each task and day.

If you reject AM-3, the campaign runs on one deployment, and placement is disclosed as a residual threat.

### 2.6 Log export

After each day, `export_service_logs.py` does the following:
1. **Archives** the **complete** CloudWatch export for the day, with its checksum. This is the true raw source.
2. **Slices** the export by each run's window into `<stem>_cpu.csv`, `_mem.csv` and `_dbq.csv`, using the **payload** `timestamp_utc` (not the ingestion time). Bounds are `[start_utc, end_utc]`, floored to the epoch second. Each run's CSVs carry the same run stem.
3. **Gates** on completeness. The expected sample count is 1 per second per service. A maximum gap of more than 5 s, or a missing service, marks the run `LOG_GAP` in `notes`.

Each line embeds its own timestamp. `awslogs` uses `mode=non-blocking` with an explicit `max-buffer-size`, so that slow log delivery cannot stall the application. Dropped lines show up as gaps.

---

## 3. Time budget (formula only; no values decided)

Total time = 25 · (R + 1) · (duration + pause) [REQ M2 §3]. This excludes repeats, and also any per-day warm-ups under AD-29.

*Illustration only, with hypothetical values:* R = 10, duration = 300 s and pause = 60 s give 275 · 360 s = 27.5 h.

---

## 4. Server-side instrumentation

### 4.1 CPU and memory per container [REQ M2 C-2, B-6]

| Option | Interval | Scope | Assessment |
|---|---|---|---|
| **Stats sidecar polling ECS Task Metadata Endpoint v4 `/task/stats` (recommended)** | 1 s target, which meets the ≤ 5 s requirement. The endpoint's refresh rate needs verifying. | Per container | Precise. Identical in every task. |
| CloudWatch Container Insights (incl. enhanced observability) | AWS documentation found states no resolution below 60 s | Per task/container | Not relied on. Context only. |
| In-process `process.cpu.time` | Configurable | Per process | A cross-check only |
| PerfMon Server Agent (original) | — | Unclear on Fargate | Dropped (R1-4) |

**Definition [PROPOSED, AD-20]:**

```
Δcpu  = cpu_stats.cpu_usage.total_usage − precpu_stats.cpu_usage.total_usage      [ns]
Δt    = read − preread                       (timestamps inside the stats payload, not the sidecar clock)
cpu_percent = Δcpu / (Δt × vCPU_allocated_to_task) × 100        → share of ALLOCATED task vCPU, 0–100 %
mem_mb      = (memory_stats.usage − inactive_file) / 2^20       (inactive_file key chosen by cgroup v1/v2; verify)
```

- Samples with Δt ≤ 0 or Δcpu < 0 (a counter reset) are dropped and counted. The count is reported per run.
- The sidecar reserves a fixed CPU share, identical in every task. Its own usage is reported separately and excluded from service CPU.
- Per run, the pipeline reports mean and peak CPU for each service and for the stack total. The stack total is the sum of CPU-seconds, normalised to the stack's total vCPU. The edge is reported first.
- Formats [REQ M2 C-2]: `timestamp_utc, service, cpu_percent` in `_cpu.csv`, and `timestamp_utc, service, mem_mb` in `_mem.csv`.
- **Clock alignment:** the sidecar, the runtime counters and the DB aggregation all emit on **wall-clock second boundaries**, so their `timestamp_utc` values coincide (M2 C-3: "same timestamps as C-2").

### 4.2 .NET runtime counters [D-3, R2-5]

`Lms.Telemetry` uses a `MeterListener` over the built-in `System.Runtime` metrics, emitting on second boundaries. It covers:
- GC collections per generation, GC pause time and heap size;
- thread-pool thread count and queue length;
- lock contention;
- **SqlClient and HttpClient pool waits** (AD-33).

Exact metric names depend on the runtime (AD-04) and are verified during implementation. The DATAS and GC settings are recorded.

### 4.3 Database time [D-3, REQ M2 C-3]

- A `DbCommandInterceptor` in each domain service times every command, tagged with `TagWith("q:<query_id>")`.
- **[AD-21]** Recommended: aggregate per second and query into `timestamp_utc, service, query_id, count, mean_ms, p95_ms, max_ms`. This deviates from the literal M2 C-3 `duration_ms`, so it needs a protocol amendment if accepted. The alternative is per-command rows, which match the spec but add heavy logging I/O at level 1000.
- Optional cross-check: SQL Server Query Store snapshots per day.
- **RDS Enhanced Monitoring at 1 s** (supported for SQL Server, per AWS docs; delivered through CloudWatch Logs) gives the DB host's OS CPU and memory. It is optional (cost) and gives context only.
- RDS CloudWatch metrics are logged for context.

### 4.4 Internal-call counters [PROPOSED]

`Lms.DomainClient` counts internal requests per edge per second and logs the URL template and parameter hash. This verifies that N-RC, N-G and N-RM issue the same calls with the same ID lists (doc 01 §4).

### 4.5 Load-generator host [REQ M2 C-2, §2.6.5]

The host logger (`psutil`, 1 s) writes `<stem>_jmeterhost.csv`. It keeps the M2 C-2 columns `timestamp_utc, service, cpu_percent`, with `service = jmeter-host`, and appends `cpu_max_core_percent`, `mem_mb`, `net_tx_bytes`, `net_rx_bytes` and `jvm_gc_pause_ms`. The appended columns are additions [PROPOSED]. A plain host average hides single-core saturation and GC stalls.

A run is flagged against **P-2** using the host average. The per-core maximum and GC pauses are reported alongside, and whether they also trigger the flag is part of P-2. The level re-run rule follows M2 §2.6.5 and §2.4 above.

### 4.7 Telemetry feasibility on Fargate and acceptance tests

**What AWS documentation confirms** (checked 2026-11-01 on docs.aws.amazon.com):
- The Task Metadata Endpoint v4 `/task/stats` exists on Fargate platform version **1.4.0+**. It "returns Docker stats for all of the containers associated with the task". A container must have run for about 1 s before stats are returned.
- The metadata response includes `ClockDrift` (`ClockErrorBound`, `ReferenceTime`, `ClockSynchronizationStatus`), measured with Amazon Time Sync.
- RDS Enhanced Monitoring supports 1 s granularity for SQL Server.
- The awslogs driver supports `mode=non-blocking` with `max-buffer-size`.

**Not documented, therefore treated as unverified:**
- the refresh rate of `/task/stats`, and any rate limit;
- whether `system_cpu_usage` is populated;
- the host CPU model in the metadata (not present, so it is taken from `/proc/cpuinfo`);
- the default awslogs mode on Fargate (it is set explicitly);
- any Container Insights resolution below 60 s.

**Resolution feasibility:**

| Signal | Required | Planned | Feasible? |
|---|---|---|---|
| Service CPU per container | ≤ 5 s [M2 C-2] | 1 s sidecar poll of `/task/stats` | Likely. The endpoint exists, but its refresh rate is undocumented, so acceptance test TEL-1 decides. Fallback: 5 s polling, which still meets M2. |
| Memory per container | ≤ 5 s, same timestamps [C-3] | Same payload | As for CPU |
| GC and thread-pool | Same timestamps | In-process `MeterListener`, 1 s | Yes. In-process, independent of AWS. |
| DB query time | Same timestamps [C-3] | In-process interceptor, aggregated per second | Yes. In-process, independent of AWS. |
| JMeter host | ≤ 5 s | `psutil` at 1 s on EC2 | Yes |
| DB host OS (context) | — | Enhanced Monitoring at 1 s | Yes (documented) |

**Acceptance tests.** "Local" means Docker on the workstation. "Pilot" means the authorised AWS pilot.

| Test | Where | Pass criterion |
|---|---|---|
| TEL-1 Sampling cadence | Local + pilot | Over 10 min: ≥ 99% of inter-sample Δ(read) are in [0.5 s, 1.5 s]. Max gap ≤ 5 s. Duplicate payloads (Δcpu = 0 with Δread = 0) < 1%. If this fails on Fargate, polling switches to 5 s and the test is repeated. |
| TEL-2 CPU calibration | Local + pilot | A test container burns a fixed fraction (25%, 50%, 75%) of the task's vCPU. The measured `cpu_percent` is within ±5 points of the target. |
| TEL-3 Memory calibration | Local + pilot | The test allocates and touches 256 MB. `mem_mb` rises by 256 ± 10%. |
| TEL-4 Counter reset | Local | The container restarts. The negative Δ is dropped and counted, with no spike. |
| TEL-5 Overhead | Local + pilot | The sidecar's own CPU, and the service-CPU difference with the sidecar on versus off at a fixed load, are both below the AD-30 threshold |
| TEL-6 Clock alignment | Pilot | A marker request at a known JMeter time appears in the service log within the AD-30 skew tolerance. `ClockErrorBound` is recorded. |
| TEL-7 DB timing completeness | Local | The interceptor's per-second `count` sums to the number of domain calls × queries per call, as counted by the internal-call counter |
| TEL-8 Log delivery | Pilot | Expected lines per run (1 per second per service) are received with no gaps. A drop under the non-blocking mode would be detected. |
| TEL-9 Host CPU model | Pilot | `/proc/cpuinfo` yields a model string inside a Fargate task. If it does not, the AD-26(c) record is marked unavailable and disclosed. |

**Measurement limitations, to disclose:**
- Fargate exposes container-level, not host-level, CPU. Steal time is not visible.
- `/task/stats` cadence is bounded by an undocumented refresh rate.
- The sidecar shares the task's CPU allocation.
- In-process counters perturb the process slightly, but they are identical in every service.
- Container Insights is not used for measurement.

---

## 5. Metric computation [REQ M2 §2.4, §4]

**Analysis window [DECISION AD-17].** M2 defines throughput "per second of run time" and does not exclude ramp-up. Excluding it is therefore a **protocol amendment**, which needs approval. The proposal:
- **Anchor:** t0 = the first sample `timeStamp` in the `.jtl`, not the runner's `start_utc`, which is taken before the JVM starts.
- **Window:** `[t0 + ramp-up, t0 + duration − tail]`. The tail (a few seconds) excludes samples cut off when the scheduler stops.
- **Membership:** a task belongs to the window if its **start** timestamp falls inside it (`sampleresult.timestamp.start` pinned).
- **Stationarity check:** an exploratory, reported check of the throughput trend within the window. The end of ramp-up does not prove a steady state.

The alternative is to keep M2's literal definition and use the whole run.

| Metric (per run) | Computation |
|---|---|
| Median and p95 task time | Successful tasks in the window. N-RM uses transaction rows only [REQ M2 §2.5]. |
| Throughput | Successful tasks ÷ window length (or ÷ run time, if AD-17 is rejected) |
| Error rate | Failed tasks ÷ all tasks in the window. Failures are classified by `failureMessage`. |
| Mean bytes per task | `bytes` of the task rows: the **transaction row** for N-RM [REQ M2 §2.5], checked against the child sum in the dry run. Bandwidth only. |
| Over-fetching | Per-configuration constant from sample bodies (doc 03 §4) |
| Mean and peak service CPU | From `_cpu.csv` within the window, aligned to t0. Per service, edge, and stack total. |
| Peak JMeter-host CPU | From `_jmeterhost.csv` |
| Memory, GC, pools, DB time | If D-3 is approved |

---

## 6. Statistical analysis [REQ M2 §4; M1 §6]

The pipeline in `analysis/` is implemented and tested on **labelled synthetic data** before any real data exists. Synthetic outputs never leave the tests directory.

1. **Unit:** the run, so n = R per cell over the inclusion set (AD-31).
2. **Per cell:** n, median, IQR, mean, SD, and a percentile bootstrap 95% CI of the median (10,000 resamples, recorded seed). Whether to also report CIs on differences is decided under AD-19. Coverage is limited at R = 10 [M1 §6].
3. **Tests:** two-sided Mann–Whitney U with Cliff's δ (negligible < 0.147 < small < 0.33 < medium < 0.474 < large).
   - Flat: 5 tests per metric.
   - Nested: 15 tests per metric.
   - Holm–Bonferroni within each workload × metric.
4. **Error rate:** tested the same way, except that all-zero ties are reported descriptively.
5. **Saturation label:** P-1 / P-4 rule, with level 100 exempt from the throughput criterion. Nothing is excluded [REQ M2 §4.1, §4.4].
6. **Figures:** median and IQR. No 1–5 scoring (D-1).
7. **Independence caveat [AD-35; formerly AD-22]:** runs are grouped by repetition and day and share deployments. The primary analysis above is **unchanged**. The grouping is examined by the sensitivity analysis in §6.1.

### 6.1 Block-aware sensitivity analysis [DECISION AD-35; an addition, not a replacement]

**Purpose.** To test whether the conclusions of the primary analysis survive when the repetition and day structure is taken into account. The analysis is **pre-registered**: `analysis/SENSITIVITY_PLAN.md` is committed, with its hash recorded in the run log README, before the first measured run. It is labelled "sensitivity" everywhere and never replaces a primary result.

The design makes this possible: every repetition contains exactly one run of every cell. Within a repetition, configurations at the same level therefore form natural **matched pairs** (or triples for nested).

| ID | Analysis | Block structure used | Output |
|---|---|---|---|
| S1 | Wilcoxon signed-rank on the R within-repetition paired differences, for the same comparison families as the primary tests (5 flat, 15 nested per metric), Holm within workload × metric | Repetition (pairing) | p_adj, Hodges–Lehmann paired median difference, matched-pairs rank-biserial r |
| S2 | Van Elteren test (a Mann–Whitney U stratified by day), Holm as above | Day (strata) | p_adj per comparison |
| S3 | Two-stage cluster bootstrap: resample days, then repetitions within each day; 10,000 resamples with a recorded seed | Day → repetition | 95% CI of the difference in cell medians |
| S4 | Descriptive: per-day cell medians, and the share of variance due to day from a random-intercept model on the log metric (`metric ~ configuration + (1 | day)`, per workload × level) | Day | A table and figure. Exploratory, with no test. |

**Robustness rule (pre-registered).** A primary conclusion is reported as **robust to blocking** if S1 and S2 agree with the primary test in direction and in significance at the Holm-adjusted α = 0.05. Otherwise it is reported as **sensitive to blocking**, with all results shown. No primary result is removed or altered.

**Constraint.** S3 and S4 need enough days to be informative. With the M2 minimum of 2 days, S3 has two clusters and is reported as descriptive only. **Recommendation (Tier 3, campaign schedule):** spread repetitions over **≥ 3 days**. This stays within M2's "at least two days". Packages are pinned (SciPy for S1; the van Elteren statistic is implemented and unit-tested against a published worked example; statsmodels for S4).

**Analysis reproducibility [PROPOSED]:**
- The Python version and a lock file are pinned (`requirements.lock` with hashes).
- `SOURCE_DATE_EPOCH` is set, and figure metadata is stripped.
- Tables and figures are hashed separately. Two consecutive runs must give identical table hashes.
- `run_all.py` starts with a **fetch-and-verify** step. It downloads the raw archive from its permanent location (for example a DOI deposit) and checks it against the committed `SHA256SUMS`, so that a clean checkout can regenerate everything.

## 7. Raw data custody [PROPOSED]

- `SHA256SUMS` covers the uncompressed original files, including the daily CloudWatch exports. It is committed to git; the raw files are not, because of their size.
- The archive is stored write-once (S3 Object Lock, or a DOI repository deposit) before analysis starts.
- The data freeze is proven by the content-hash procedure in doc 03 §2.4. Its JSON outputs (after load, each day, after the campaign) are part of the archive.
- B-records in `experiment/config/` are the single source of truth. DECISION files link to them. The B-record git commit time must be earlier than the first measured `start_utc`, which proves the configuration was frozen first.

## 8. Cost model [D-6, R2-9]

There are two different quantities, and they are never mixed or presented as one another.

### 8.1 Modelled cost per successful task (reported per cell)

**Billing basis (per the AWS Fargate pricing page, checked 2026-11-01):**
- Fargate bills **allocated** vCPU and memory per second, with a 1-minute minimum (Linux).
- Billing runs from the start of the image pull until the task terminates.
- Ephemeral storage above the 20 GB default is billed extra. None is planned.
- Utilisation is **not** billed. CPU% therefore affects cost only through throughput.

```
rate_stack   = Σ_{tasks i in stack} ( vCPU_i × p_vCPU_hour + GB_i × p_GB_hour ) / 3600        [$ / s]
cost_task    = rate_stack / throughput_run                                                 [$ / successful task]
cost_1k      = 1000 × cost_task          → summarised per cell like any per-run metric (median, IQR)
```

| Component | Treatment |
|---|---|
| Fargate compute (edge + domain + sidecars, as allocated) | Included. This is the primary modelled cost. |
| RDS instance and storage | Shared and identical across configurations. Reported as a separate "with database" variant: `rate_db / throughput`. |
| Data transfer | JMeter → edge is in one AZ over private IPs. The applicable rate is verified on the AWS EC2 data-transfer pricing page by the researcher; the automated check could not confirm the rule. Any internet egress is a **scenario** only: `bytes_per_task × p_internet_egress_GB`, labelled "if clients were on the internet". |
| Billable duration | The steady-state rate model uses allocation × time ÷ tasks. The 1-minute minimum and the image-pull time affect actual bills only (§8.2), not the steady-state per-task model. Both are stated. |
| Excluded harness costs | JMeter EC2, CloudWatch Logs, ECR, Enhanced Monitoring, NAT. These are test infrastructure, not the system under test. |

**Prices** are recorded in `experiment/config/prices_<region>_<YYYY-MM-DD>.json`: on-demand Linux, with the CPU architecture stated, the region, the source URLs and the retrieval date. The researcher supplies or confirms them. They are never invented. Savings Plans, Spot and free tiers are not applied.

### 8.2 Actual AWS billing (reported once per campaign)

- All campaign resources carry a `campaign=<id>` cost-allocation tag.
- After the campaign, the researcher exports the tagged total from Cost Explorer or the Cost and Usage Report.
- It is reported as **"actual campaign cost"**. It includes harness, idle time, warm-ups, pilot (if tagged), minimums and logs.
- It is used only as a reconciliation check on the order of magnitude of the model. It is **never** divided into per-task figures or compared between protocols.

## 9. Pilot calibration [DECISION AD-12]

The pilot is a paid run, executed by the researcher, and it is not part of M2. It calibrates B-1, B-5, B-9 and P-3.
- **Neutrality rule:** the pilot looks only at generator capacity, error behaviour, pool saturation and whether a steady state is reached. It must **never** compare REST with GraphQL, and no setting is chosen per protocol.
- Pilot data goes to `experiment/pilot/` and is never analysed.

## 10. Local verification (free)

A Docker Compose environment with SQL Server and the full dataset is used for:
- functional tests and the equivalence checks (keys and values);
- sample bodies and headers;
- JMeter smoke tests at 1–5 threads;
- the N-RM dry-run checks (§1);
- measuring the overhead of each sidecar and logger, with the acceptance threshold set under AD-30.

**Local numbers are never reported as results.**
