# Phase 1 Implementation Readiness Checklist

| | |
|---|---|
| Date | 2026-11-01 |
| Phase 1 scope | **Schema-independent scaffolding only** (doc 05 Phase 1). No domain models, DDL, API contracts or GraphQL schema: those depend on the provisional book schema (O-7). |
| Start condition | Researcher instruction to begin Phase 1. **Not started.** |

## 1. Entry criteria (doc 05 Phase 0 exit / doc 07 §7)

| # | Criterion | State | Evidence |
|---|---|---|---|
| 1 | Every Tier 1 item has a DECISION record | ✅ Met. Provisional elements are marked. | DECISION-002 … 008; `PROTOCOL_STATUS.md` |
| 2 | CL-1 (CRIR) and AM-2 (`_dbq`) recorded | ✅ Met | DECISION-003, DECISION-007 |
| 3 | Version control initialised safely | ✅ Met: `git init` at the root, with the originals ignored | DECISION-008; `.gitignore` |
| 4 | Baseline and document fingerprints | ✅ Met: 112 blobs at `ff9b302`, tree clean; 4 research documents verify OK | `baseline_manifest.txt`, `research-documents.sha256` |
| 5a | .NET 10 SDK | ✅ 10.0.401 installed | `dotnet --list-sdks` |
| 5b | Docker engine running (Linux containers) | ❌ **Not running.** The CLI 28.1.1 is present; the engine pipe was not found. WSL default version 2. | `docker version` / `docker info` |
| 5c | Network package and image sources | ⏳ Needs your approval: NuGet restore from nuget.org; pulling `mcr.microsoft.com/dotnet/sdk:10.0`, `aspnet:10.0` and `mssql/server:2022-latest`, pinned by digest after the pull | — |
| 6 | No AWS credentials used in Phases 1–9 | ✅ Rule in place. The local tooling asks for confirmation before any `aws` command. | rules, settings |

**Verdict.** Initial .NET scaffolding **can begin safely** once you instruct it.
- Code-only scaffolding and the `dotnet build` of empty projects need only NuGet access (criterion 5c).
- Compose-based local runs also need **criterion 5b**: start Docker Desktop with WSL 2.
- Nothing in Phase 1 depends on the provisional dataset elements.

## 2. Proposed Phase 1 files and projects

```
benchmark/
├── global.json                      SDK pin: 10.0.401, rollForward "latestPatch" (within the 10.0.4xx band); actual version recorded
├── Directory.Build.props            net10.0; Nullable; ImplicitUsings; Deterministic; ContinuousIntegrationBuild; TreatWarningsAsErrors
├── Directory.Packages.props         Central package management; versions pinned at first restore and recorded in BUILD_INFO.md
├── LmsBenchmark.slnx                Solution (.slnx, the .NET 10 default format)
├── src/
│   ├── Lms.Hosting/                 Shared host configuration, AddBenchmarkHost():
│   │                                  • Kestrel Protocols=Http1, limits from config
│   │                                  • logging levels (Microsoft.*, System.Net.Http = Warning)
│   │                                  • SP-1 serialization settings object (DECISION-005): System.Text.Json options;
│   │                                    the HotChocolate formatter mapping is added in Phase 4
│   │                                  • no compression, no HTTPS redirection, no response caching, no Swagger
│   │                                  • /health endpoint, invariant culture
│   ├── Lms.Catalog.Service/         Minimal host: AddBenchmarkHost() + /health only (no domain code)
│   ├── Lms.Members.Service/         as above
│   ├── Lms.Loans.Service/           as above
│   ├── Lms.Edge.Rest/               as above (controllers registration only, no routes)
│   └── Lms.Edge.GraphQL/            as above (HotChocolate is added in Phase 4, with the schema)
├── tests/
│   ├── Lms.Hosting.Tests/           xUnit:
│   │                                  • Http1-only
│   │                                  • log levels
│   │                                  • SP-1 System.Text.Json rules 1–7 on synthetic fixtures (REST side of SER-1 to SER-5)
│   │                                  • no compression or caching middleware registered
│   └── Lms.Hosts.SmokeTests/        WebApplicationFactory: each of the 5 hosts starts and /health returns 200
├── docker/
│   ├── Dockerfile.service           One multi-stage Dockerfile, parameterised by project; base images pinned by digest
│   ├── docker-compose.yml           SQL Server 2022 container + 5 hosts on an isolated network (local only)
│   └── .env.example                 Variable names only. The real .env is generated locally and git-ignored.
├── scripts/
│   ├── build.ps1 / build.sh         restore → build → test; writes BUILD_INFO.md (SDK, runtime, package versions, image digests)
│   └── verify-baseline.ps1/.sh      Re-checks baseline_manifest.txt and research-documents.sha256 (read-only)
└── BUILD_INFO.md                    Generated record (B-4 draft)
docs/milestone2/symmetry_checklist.md   Skeleton listing every row of doc 01 §5; evidence is filled in at Phases 1 and 4
```

**Phase 1 exit criteria (doc 05):**
- `dotnet build` and `dotnet test` pass.
- The images build locally (needs 5b).
- Versions are recorded.
- The baseline verification script reports no change.

**Explicitly deferred out of Phase 1:**
- `Lms.Contracts` and `Lms.DomainClient` (they depend on the schema).
- `Lms.Telemetry` (Phase 3).
- The data generator and DDL (Phase 2, after O-7).
- HotChocolate schema, controllers' routes, JMeter, analysis.

## 3. Environmental blockers and pending inputs

| Blocker | Blocks | Action |
|---|---|---|
| The Docker engine is not running | Phase 1 container build and compose run; Phase 2 onward | Start Docker Desktop (WSL 2 backend) |
| Approval for NuGet restore and container image pulls | Phase 1 | Your go-ahead with the Phase 1 instruction |
| Kaggle dataset not downloaded (needs your account) | **Phase 2 only.** Freezing the dataset-dependent definitions. | Download into `benchmark/data/source/` (git-ignored) |
| JMeter not installed | Phase 7 | Approve a pinned version when Phase 7 starts |
| Python `scipy`, `pandas`, `statsmodels`, `psutil` missing | Phase 8 | Approve pinned installs when Phase 8 starts |
| Tier 2/3 decisions open | Phases 9–11 | Per `PROTOCOL_STATUS.md` |
