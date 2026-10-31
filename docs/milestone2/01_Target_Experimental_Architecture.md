# 01 — Target Experimental Architecture

| | |
|---|---|
| Status | **Architecture approved as the baseline (DECISION-002, 2026-11-01).** Nothing has been implemented. Serialization follows DECISION-005 (policy SP-1). Open items are listed in `PROTOCOL_STATUS.md`. |
| Date | 2026-11-01 |
| Governs | Milestone 2 re-run protocol (M2 §2–§4). Where M2 explicitly supersedes M1, M2 applies. |
| Depends on | Decisions in [06_Methodology_Decisions_Required.md](06_Methodology_Decisions_Required.md). Every one marked **AD-nn**, **D-n**, **P-n**, **B-n** or **A-n** is open. |

**Tags used in this document**
- **[REQ M2 §x]**: confirmed requirement from Milestone 2.
- **[REQ M1 §x]**: Milestone 1 requirement that M2 does not supersede.
- **[REVIEW Rn-k]**: answers reviewer *n*, comment *k*.
- **[PROPOSED]**: an architectural choice made here, pending approval.
- **[DECISION AD-nn]**: needs a researcher choice.

---

## 1. Design goal

The experiment must let a reader attribute every measured difference to one of two factors:

1. **The client-facing API style:** REST versus GraphQL.
2. **For nested retrieval only, the client's retrieval regime:** a single composite request versus a chain of dependent requests.

Every other factor is held constant by construction, not by assertion. These factors include data, database work, internal service communication, runtime, serializer, network path, container size and load profile.

This goal answers the central criticism of both reviewers. R2 point 3 asks whether "the observed differences arise solely from the protocol itself". R1-7 says task equivalence "can't be judged". The previous implementation could not support such a claim. Its REST and GraphQL code shared no components, used different HTTP versions, field names and database catalogs, and had no nested implementation at all. See [02](02_Existing_Code_Reuse_Strategy.md) §2.

### 1.1 Scope of the primary study (refinement pass, 2026-11-01)

**The primary study isolates the client-facing API style over identical internal services.** Both stacks call the same domain services with the same parameters, and those services run the same SQL. The benchmark therefore measures what differs **at and above the edge**:
- request parsing and validation, and resolver or controller execution;
- response shaping and serialization;
- transmitted bytes;
- for N-RM, client round trips and dependency extraction.

**GraphQL projection pushdown is excluded by design (AD-25).** The GraphQL edge does not pass its field selection to the database. It receives the same full-row domain responses as the REST edge. The following GraphQL benefits are therefore **not** measured:
- reading fewer columns from the database;
- smaller internal payloads;
- schema-driven query optimisation (selection-aware SQL);
- federation and gateway query planning.

The over-fetching metric measures only bytes transmitted **to the client**.

| Research question (kept as written, M1 Table 3) | What this design can support | What it cannot support |
|---|---|---|
| RQ1: response time, flat vs nested | Differences in client-observed task time caused by the edge protocol (RC vs G), and by the multi-request regime (RM vs RC, RM vs G), under identical back-end work | Claims about end-to-end GraphQL systems in which selection reaches the database or a federated gateway |
| RQ2: CPU as concurrency rises | Edge-service CPU differences (the protocol layer) and stack-total CPU when the domain work is equal | Database-tier CPU savings from projection pushdown |
| RQ3: over-fetching and bandwidth | Client-facing payload size against a protocol-independent canonical requirement (doc 03 §4) | Internal or database-level data-transfer savings |

**The wording of conclusions is fixed accordingly [PROPOSED; paper wording is a Tier 4 decision].**
- Every claim is qualified with "client-facing API style over identical internal services". This qualifier is used instead of "REST vs GraphQL" in general.
- No claim covers every GraphQL benefit.
- Threats to Validity list the excluded mechanisms above under construct validity.

**Neutrality principle [PROPOSED].** Neither stack receives tuning that the other does not. Library defaults are kept unless a setting is shared by both stacks or is required by M2 (for example, caching disabled under B-7). Every non-default setting is recorded in B-4/B-7. No setting may be changed after a pilot because it favours one protocol.

---

## 2. Architecture options considered

The choice of topology determines what the experiment can claim. It is the most important decision in this document: **[DECISION AD-01]**.

| Option | Description | What a REST–GraphQL difference means | Main weakness |
|---|---|---|---|
| **A — Protocol-neutral domain tier, protocol-specific edge** (recommended) | Each stack has one *edge* service that speaks REST or GraphQL to the client. Both edges call identical *domain* services (Catalog, Members, Loans) through the same internal HTTP/JSON client library. The domain tier is built from the same images in both stacks. | The cost of the client-facing API layer: request parsing and validation, resolver or controller execution, response shaping, serialization and over-fetching, plus round trips for N-RM. Database and internal work are identical. | The internal hops are not "GraphQL end-to-end". A reader interested in GraphQL federation does not get that answer. |
| B — Protocol-native end-to-end | REST stack: REST domain services plus a REST composition service. GraphQL stack: GraphQL subgraphs plus a gateway using HotChocolate Fusion or stitching. | API style **plus** internal protocol, gateway product and query planner. These cannot be separated. | Reintroduces the confound that R2 point 3 criticises. Gateway-specific overheads dominate. |
| C — Single service per stack | One REST service and one GraphQL service, each reading all tables from one database. | The API layer only, with no microservice hops. | Drops the "cloud-native microservices" framing in the title and research questions. Weakens continuity with the original paper. |

**Recommendation: Option A.** It keeps the microservice topology that the title and Fig. 1 describe. It also makes the database and internal-call work provably identical across all five configurations (§5). The weakness is acknowledged in Threats to Validity: the study compares *client-facing* API styles over a fixed internal protocol. Option B can be listed as future work.

Option A also has a sub-choice, **[DECISION AD-02]**:
- **A1 (recommended):** two complete stacks, each with its own deployment of the identical domain images. This matches M2's wording "both API stacks on identical task sizes".
- **A2:** one shared domain tier behind two edges. This is cheaper and removes one source of variance, but it departs from the "two stacks" framing.

The rest of this document assumes **A1**.

**Placement confound under A1 [DECISION AD-26]** (from the methodology review). With no restarts, each stack keeps the same Fargate tasks, and therefore the same physical hosts, for the whole campaign. Host quality would then be perfectly aligned with protocol. Randomised run order cannot remove that. The mitigations are:
- (a) A2, a shared domain tier, which removes the alignment for domain services only;
- (b) redeploying both stacks at the start of each day, followed by the extra warm-up that M2 §2.2 already requires;
- (c) logging the host CPU model of every task and disclosing it. The task metadata does not include it, so each sidecar reads `/proc/cpuinfo` (`model name`) once at start. Whether this works on Fargate is verified in the pilot.

**Recommendation:** (b) + (c). Each day becomes an independent placement draw for both stacks.

**What Option A gives up [DECISION AD-25].** Because the domain services return a fixed row shape to both edges, GraphQL cannot push its field selection down to the database (fewer columns read). The GraphQL edge also deserialises full rows, including `description`. Projection pushdown is a real feature of GraphQL. Two options:
- **Fixed full-row domain contract (recommended, retained after the refinement review):** isolates the API layer. Pushdown is excluded by design and stated in scope (§1.1) and in Threats.
- **Projection-aware domain queries:** GraphQL requests only the selected columns. This is more realistic, but the SQL then differs between configurations, and DB time becomes part of the protocol effect.

---

## 3. Logical architecture (Option A1)

```
                      ┌──────────────────── VPC, one AZ (B-3) ─────────────────────────────┐
 ┌──────────────┐     │                                                                     │
 │ JMeter host  │     │  REST stack                         GraphQL stack                   │
 │ (EC2, B-5)   │─────┼─▶ edge-rest  (Lms.Edge.Rest)         edge-graphql (Lms.Edge.GraphQL)│
 │ runner +     │     │      │  internal HTTP/1.1 JSON           │  same client library     │
 │ host logger  │     │      ▼                                   ▼                          │
 └──────────────┘     │  catalog-r  members-r  loans-r       catalog-g  members-g  loans-g    │
                      │      │          │         │              │          │         │     │
                      │      └──────────┴────┬────┴──────────────┴──────────┴─────────┘     │
                      │                      ▼                                              │
                      │        RDS SQL Server: one instance, three databases                │
                      │        lms_catalog · lms_members · lms_loans (read-only during runs)│
                      └─────────────────────────────────────────────────────────────────────┘
```

| Component | Responsibility | Status |
|---|---|---|
| `Lms.Catalog.Service` | Books: list by page and get by ID set | [PROPOSED] |
| `Lms.Members.Service` | Members: list by page and get by ID set | [PROPOSED] |
| `Lms.Loans.Service` | Loans: get by a set of member IDs | [PROPOSED] |
| `Lms.Edge.Rest` | Public REST API: the flat route, the composite route, and the resource routes used by N-RM | [PROPOSED] |
| `Lms.Edge.GraphQL` | Public GraphQL API (HotChocolate): the `books` and `members { loans { book } }` fields, using DataLoaders | [PROPOSED] |
| `Lms.DomainClient` | Typed internal HTTP client used by **both** edges: one pooled `SocketsHttpHandler` and identical serializer options | [PROPOSED] |
| `Lms.Telemetry` | Shared in-process metrics emitter (CPU-independent runtime counters and DB command timings), identical in every service | [PROPOSED] |
| Stats sidecar | Polls the ECS Task Metadata Endpoint v4 `/task/stats` for per-container CPU and memory. AWS documents this endpoint for Fargate platform 1.4.0+. Its refresh rate is undocumented, so it is acceptance-tested (doc 04 §4.7). | [PROPOSED], see [04](04_Benchmark_and_Instrumentation_Design.md) |

**Notification service and Auth service.** The original Fig. 1 shows both. Neither takes part in read retrieval, and the original Notification service was an empty template. **[DECISION AD-03]**
- Recommended: omit both from the measured system and state this in §3.2 of the paper.
- If D-4 (auth runs) is approved, add JWT *validation* as middleware in both edges instead of a separate Auth service. This keeps the authentication cost symmetric and inside the measured boundary.

---

## 4. Request paths per configuration

| Config | Client → edge | Edge → domain (internal calls per task) | Aggregation happens in |
|---|---|---|---|
| **F-R** | `GET /api/books?offset=O&limit=N` (1 request) | Catalog: 1 | n/a |
| **F-G** | `POST /graphql` with `query FlatBooks { books(offset,limit) { …A-6 fields } }` (1 request) | Catalog: 1 | n/a |
| **N-RC** | `GET /api/members-with-loans?offset=O&limit=M` (1 request) | Members 1, then Loans 1 (batched by member IDs), then Catalog 1 (batched by book IDs) = **3** | `Lms.Edge.Rest` composite handler |
| **N-G** | `POST /graphql` with `members(offset,limit) { … loans { … book { … } } }` (1 request) | Members 1, Loans 1 (DataLoader), Catalog 1 (DataLoader) = **3** | HotChocolate execution engine with DataLoaders |
| **N-RM** | Chain of 3 dependent requests: `GET /api/members?…`, then `GET /api/loans?memberIds=…`, then `GET /api/books?ids=…` | 1 per edge request = **3** | The client (JMeter), with IDs extracted between calls |

**The key property [PROPOSED]:** under chain option (i), the three nested configurations issue the same three internal domain calls with the same parameter values. Identical values are guaranteed because `Lms.DomainClient` sorts and de-duplicates every ID list. The calls run the same SQL. The comparisons then isolate:
- **N-RC vs N-G:** the edge protocol and execution cost.
- **N-RM vs N-RC:** the cost of two extra client↔edge round trips and the client-side ID extraction between them.
- **N-RM vs N-G:** the multi-request regime against GraphQL.

**Scope of the N-RM claim.** In a single AZ the client↔edge round-trip time is small. JMeter also only extracts IDs; it does not assemble a nested object. A 3-call batched chain therefore measures the *minimum* multi-call penalty. It is **not** the "classic" N+1 under-fetching that R2 point 3 describes.

The chain design and whether to emulate client network latency are researcher decisions: **[DECISION AD-07]** and **[DECISION AD-27]** in [03](03_Workload_and_Data_Model_Design.md) §3.5. The options are:
- (i) a batched 3-call chain;
- (ii) a per-resource chain of 1 + M + M·L calls;
- (iii) a hybrid chain of 1 + M + 1 calls.

Flat retrieval also has identical internal work: one Catalog call in both configurations.

---

## 5. Equivalence by construction

| Layer | How sameness is guaranteed | Verified by |
|---|---|---|
| Data | One RDS instance and one dataset, loaded once from a seeded generator. Read-only during runs. | Data-freeze verification (doc 03 §2.4): SHA-256 content hashes of canonical table exports, row counts, and referential-integrity checks, run after load, before each campaign day, and after the campaign |
| SQL | The domain services are the same image in both stacks, so the same EF Core queries run (`AsNoTracking`, explicit projections, keyset or offset paging ordered by primary key) | Captured SQL text per workload, logged once and compared |
| Internal calls | Both edges use `Lms.DomainClient`. The internal call count per task is fixed (§4). | Per-run internal request counters from `Lms.Telemetry` |
| Records | Same offset and limit, ordering by primary key, deterministic data | C-6 key lists [REQ M2 §2.3.5] plus JMeter count assertions [REQ M2 §2.5] |
| Fields | GraphQL selects exactly the A-6 list. REST returns the full documented resource representation, which is a superset. | the equivalence validation procedure |
| Runtime | Same .NET version **[DECISION AD-04]**, same base image digest, same `DOTNET_*` environment, and the same GC mode. Server GC is the ASP.NET Core default. Since .NET 9, Server GC also has DATAS on by default; it is pinned explicitly and recorded either way. Same Kestrel limits. | B-4 record and image digests |
| Serialization | The REST edge and the internal client use System.Text.Json. The GraphQL edge uses HotChocolate's own writer. HotChocolate does **not** read MVC `JsonOptions`, so `Lms.Hosting` configures each serializer explicitly. **[DECISION AD-32]**: both use the same `JavaScriptEncoder` policy, because ASP.NET Core's default escapes non-ASCII characters and `' < &`. `DATE` columns map to `DateOnly`, and the GraphQL scalar mappings for dates and decimals are pinned. Output is compact with no compression. | A test compares shared field values byte-for-byte across stacks (C-6 values, not only keys) |
| HTTP | HTTP/1.1 with keep-alive on every hop for both stacks. Kestrel is set explicitly to `Protocols=Http1`, which replaces the baseline's HTTP/2-only GraphQL setting. No TLS inside the VPC. Compression off. Identical `Accept` headers. **[DECISION AD-05]** | Sample response headers (framing, `Content-Type`) recorded for every configuration under B-7/B-9 |
| Pools and limits | SqlClient `Max Pool Size` (default 100), `DbContext` pooling, `SocketsHttpHandler.MaxConnectionsPerServer` and `PooledConnectionLifetime` are set identically in all services and recorded. Pool waits are logged. **[DECISION AD-33]** | A saturated pool would hide the protocol effect. Pool-wait telemetry shows whether it happened. |
| Logging | `Microsoft.*` and `System.Net.Http` at Warning in every service (through `Lms.Hosting`). N-RM would otherwise triple the edge request logs. | Config review |
| Internal call parameters | `Lms.DomainClient` sorts and de-duplicates ID lists. Domain queries `ORDER BY` the primary key. The EF Core `Contains` translation mode and DB compatibility level are pinned. GraphQL `[UseProjection]` and filtering pushdown are not used (they would change the domain calls). | Logged internal URLs compared across N-RC, N-G and N-RM. Captured SQL. |
| Caching | No `Cache-Control`/`ETag` headers, no response-caching middleware, no output cache, no persisted queries, no CDN or proxy [REQ M2 B-7] | Header capture, plus a reproducibility QA config review |

**One intended, documented asymmetry:** the GraphQL edge parses and validates a query document and then executes resolvers, while the REST edge runs routing and model binding. This is the protocol cost under study. HotChocolate's document and operation caches are left at their defaults (neutrality principle). After the first request they make repeated identical queries cheaper, which is the normal production behaviour. This is disclosed.

HotChocolate 14+ turns **cost analysis and enforcement on by default**. This adds per-request work, and it can reject nested list queries that lack list-size hints. It is made an explicit choice under **[DECISION AD-24]**: keep the default, check that the nested query passes, and disclose; or disable it. Either way the setting is recorded (B-4).

---

## 6. Deployment architecture (AWS, drafted only; applied only by the researcher)

| Element | Proposal | Rationale | Relevant to |
|---|---|---|---|
| Compute | ECS on Fargate, platform version pinned, autoscaling disabled [REQ M2 §2.6.1]. **1 task per service** [PROPOSED]. | A fixed task count removes scheduling and load-balancing variance. Per-container CPU becomes unambiguous. | R1-2, R1-4 |
| Task sizes | Identical per role across stacks. Edge X vCPU / Y GB; domain services X′ / Y′ (values under B-1) | Symmetry [REQ M2 §2.6.1]. Values come from the researcher, possibly informed by a pilot (AD-12). | R1-2 |
| Database | One RDS for SQL Server instance shared by both stacks (runs never overlap); Single-AZ; edition, class and storage under B-2 | A single instance removes instance-to-instance variance. A read-only workload means no state drift. | R1-2 |
| Network | Edge, domain services, RDS and the JMeter host in one VPC and **one AZ** | Removes cross-AZ latency and its variance | B-3 |
| Service discovery | AWS Cloud Map private DNS **without** the Service Connect/Envoy proxy | A sidecar proxy would add per-hop CPU and latency that is neither protocol | Confounder control |
| Ingress | **[DECISION AD-06]** (a) JMeter → edge task through Cloud Map DNS, with no load balancer (recommended for control); or (b) an internal ALB in front of each edge (more realistic, but adds a hop with its own scaling behaviour) | With 1 task per edge, an ALB adds no distribution benefit, only noise | B-9 |
| Images | Built once and pushed to ECR. Task definitions reference **digests**, not tags | Reproducibility | C-5 |
| Secrets | DB credentials in AWS Secrets Manager and injected as env vars. None in source. | Fixes the baseline's committed credentials | — |
| IaC | Terraform (recommended) or AWS CDK for C#: **[DECISION AD-13]**. Every file headed `DRAFT — not applied` until the researcher applies it. | Infrastructure becomes part of the replication package | R2-10 (design-level mitigation) |
| Load generator | One EC2 instance in the same AZ, size under B-5. Host CPU logged (P-2 rule). | [REQ M2 §2.6.5] | — |
| Time | chrony with Amazon Time Sync Service on the JMeter host, with the `chronyc tracking` offset logged per run. On Fargate, the task metadata `ClockDrift` fields (`ClockErrorBound`, `ClockSynchronizationStatus`; Time Sync based, per AWS docs) are logged per run for every task. Skew tolerance is set under AD-30. All timestamps in UTC. | NTP sync [REQ M2 §2.6.1]; offset logging [PROPOSED] | Log slicing ([04](04_Benchmark_and_Instrumentation_Design.md) §2) |

---

## 7. Measurement boundaries

| Quantity | Boundary | Source |
|---|---|---|
| Task time | From the client sending the first byte of the (first) request to the last byte of the (last) response, measured at JMeter. Includes the network between JMeter and the edge. | JMeter `elapsed`. For N-RM, the Transaction Controller row [REQ M2 §2.3.4] |
| Over-fetching | Response **body** only, compact JSON, headers excluded [REQ M2 §2.3.3] | Sample responses (offline) |
| Bandwidth | The JMeter `bytes` field, headers included [REQ M2 §2.4] | JMeter |
| Service CPU and memory | **Per container**, as a percentage of that task's allocated vCPU (formula in [04](04_Benchmark_and_Instrumentation_Design.md) §4). Reported as edge CPU and stack total. | Stats sidecar via the Task Metadata Endpoint |
| Runtime (GC, thread pool) | Per service process | `Lms.Telemetry` |
| DB time | Per DB command, measured in the domain services, aggregated per interval | EF Core `DbCommandInterceptor` (D-3) |

Reporting **edge CPU** separately answers R1-4 directly: "one container, an average, or a host aggregate?" The answer becomes "every container is reported, and the edge is where the protocols differ".

---

## 8. How the architecture answers the review

| Criticism | Architectural answer |
|---|---|
| R1-1, R2-1: single run, no statistics | The orchestrator runs 25 × (R + 1) runs from a seeded schedule. The analysis pipeline is fixed in advance ([04](04_Benchmark_and_Instrumentation_Design.md)). |
| R1-2, R1-3: sizing, JMeter configuration undisclosed | IaC and `experiment/config/` B-records are part of the package. Closed-loop, duration-based plans. |
| R1-4: CPU scope | Per-container CPU with a defined formula. Edge reported separately. |
| R1-5, R2-2, INT-1: Required Bytes | One field list per workload. Compact-JSON rule computed by script from sample responses. |
| R1-7, R2-2, R2-3: equivalence, nested REST | Five configurations with identical internal work (§4–§5). C-6 key lists. Query documents and REST samples published. |
| R2-5: memory, GC, DB time | Sidecar memory, runtime counters and DB command timings (if D-3 is approved) |
| R2-4: auth, writes | Optional, symmetric JWT validation (D-4). Optional mutation workload (D-5). Neither is included by default. |
| R2-9: cost | vCPU-seconds, GB-seconds and egress per task can be derived from the measured resource logs (D-6) |
| R1-8: HTTP caching | Disabled and verified (B-7). Discussed qualitatively in the paper. Not measured. |
| R2-10: threats mitigated by design | §5 equivalence table, plus Threats rewritten as mitigated versus residual |

## 9. Residual threats this architecture does not remove

1. **One application, one schema, one cloud, one runtime.** This is external validity (R2 point 7). It is stated, not solved.
2. **The internal protocol is fixed (HTTP/JSON).** Results describe client-facing API style over that protocol (Option A).
3. **HotChocolate is one GraphQL server implementation.** Other servers, for example in Node.js or Java, may differ.
4. **Closed-loop load hides queueing delay (coordinated omission).** Latency at saturated levels reflects queueing. The analysis labels saturation [REQ M2 §4.4].
5. **Shared, multi-tenant Fargate hardware.** Randomised order and ≥ 2 days, plus daily redeploys if AD-26 is approved, mitigate this but do not eliminate it.
6. **Fixed, low client RTT inside one AZ (R2 point 7).** The multi-call penalty measured here is a lower bound for geo-distributed clients, unless AD-27 adds latency emulation.
7. **No projection pushdown for GraphQL** under AD-25 (fixed domain contract).

**Reviewer points outside this architecture** are paper-level work for Milestone 3. They are listed in [06](06_Methodology_Decisions_Required.md) §F: R1-8 (caching paragraph), R1-9 and R2-8 (prior-study comparison), R1-10 (Table 5 wording), R1-11 (repository URL), R1-12 (references and A-1 workbook), and R1-6/R2-6 (scoring removal, D-1).
