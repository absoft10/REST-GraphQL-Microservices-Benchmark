# 02 — Existing Code Reuse Strategy

| | |
|---|---|
| Status | **DRAFT for researcher approval** |
| Date | 2026-11-01 |
| Baseline | `existing-source/MicoserviceRestvsGraphql/`, commit `ff9b302cf85941eef30524af30e459a4a281fc7a` (112 tracked files, one commit) |
| Principle | The baseline is **reference material, not the source of truth**. The research requirements (RQs, reviewer criticisms, M1, M2) define the implementation. The baseline is never modified or deleted. |

## 1. Preservation of original assets [researcher instruction 2026-11-01, recorded in `docs/decisions/DECISION-001-baseline-reference-status.md`]

| Asset | Location | Preservation measure |
|---|---|---|
| Original source | `existing-source/MicoserviceRestvsGraphql/` | Read-only. Not edited, rebuilt in place, reformatted or re-committed. No new tags or branches are created in it. |
| Baseline fingerprint | `docs/milestone2/baseline_manifest.txt` (Phase 0) | Output of `git ls-tree -r ff9b302` (blob hashes), plus a check that `git status --porcelain` is empty. Blob hashes are used because the repo has `core.autocrlf=true`, so hashes of the working tree would not be stable. Proves the baseline was not altered. [PROPOSED] |
| Original manuscript, review and milestone documents | `research-documents/` | Read-only (write access denied by local tooling permissions) |
| Original results | Only inside the manuscript PDF (figures and text). No raw data is in the workspace. | Cited only as historical context. Never mixed with new data [REQ M2 §2.6.7, M1 §7]. |

**Unknown:** whether raw data, the original `.jmx` plans (M2 A-5) or the original Required Bytes method (M2 A-6, first part) exist anywhere.
- If they do, the researcher archives them read-only under `research-documents/original-experiment/`. They explain the old 3–12% GraphQL over-fetching figure (R1-5) historically, and they do not enter the new analysis.
- If they do not, the paper states that the original method cannot be recovered and is replaced by the M2 §2.3 definition.

## 2. Assessment of the baseline

| # | Observation | Evidence | Consequence for the redesign |
|---|---|---|---|
| 1 | No nested workload in either stack | REST controllers and GraphQL resolvers | Must be designed from scratch ([03](03_Workload_and_Data_Model_Design.md)) |
| 2 | GraphQL User, Loan and Notification services return `hello` | `LMS_GRAPHQL/*/GraphQL/Query.cs` | The "four GraphQL microservices" in Fig. 1 did not exist in this code |
| 3 | Separate databases with no relationships. Seed loans reference books that do not exist. | `*DbContext.cs` | A new data model and generator are needed |
| 4 | Unpaginated `ToListAsync()` over the whole table | `BookService.cs`, `BookRepository.cs` | This cannot match the payloads reported in the paper, so the repo is probably not the benchmarked version |
| 5 | GraphQL HTTP/2-only versus REST defaults with HTTPS redirection | `appsettings.json` / `Program.cs` | An HTTP-version confound. It must be unified. |
| 6 | `ISBN` vs `Isbn`, catalog `Lms.BookService` vs `Lms.BookServiceGrpc`, different validation attributes | Both `Book.cs` | Field and byte asymmetry. Use one shared model. |
| 7 | Plaintext credentials committed | `appsettings.json` | Secrets Manager or env vars. The researcher should rotate the existing credentials. |
| 8 | Template projects (WeatherForecast), gRPC leftovers, an unrelated Salesforce `User` model | Several | Dead code. Do not carry it forward. |
| 9 | No tests, JMeter plans, Dockerfiles or IaC | Inventory | Everything must be built new |
| 10 | The services use EF Core 9.0.0 on `net8.0`. EF Core 8.0.10 appears only in the template project, which is dropped. Only `LMS_GRAPHQL/global.json` pins an SDK (8.0.403, not installed locally); the REST stack has no pin. | `*.csproj`, `global.json` | One framework and SDK pin for everything (AD-04) |

**Conclusion:** the baseline is a partial prototype. Its value is conceptual: the domain (books, users, loans), the technology choices (.NET, EF Core, SQL Server, HotChocolate), and some entity fields. It offers continuity with the paper. Little of its code is reusable as-is.

## 3. Component-by-component decision

Legend: **Reuse** = copy concept or code with minimal change. **Refactor** = keep the idea and rewrite it to the new standards. **Replace** = new implementation. **Drop** = not carried forward. Each choice is [PROPOSED] unless marked otherwise.

| Baseline component | Decision | Target | Rationale | Reviewer relevance |
|---|---|---|---|---|
| Domain concept (Book, User, Loan) | **Reuse** | Catalog, Members, Loans services | Continuity with the paper's system description | — |
| `Book` entity (`Id, Title, Author, ISBN, PublishedYear, Genre, IsAvailable, CreatedAt, UpdatedAt`) | **Refactor** | `catalog.book`. Columns aligned to the Kaggle dataset the paper cites [33] (AD-08). | One model shared by both stacks removes the `ISBN`/`Isbn` asymmetry | R1-5, R1-7 |
| `User` entity (`UserId, FirstName, LastName, Email, PhoneNumber, Address`) | **Refactor** | `members.member` | Plausible fields. Renamed to avoid a clash with ASP.NET identity terminology (optional). | — |
| `Loan` entity (`LoanId, BookId, UserId, LoanDate, ReturnDate, IsReturned`) | **Refactor** | `loans.loan`, indexed on `member_id` | Needed for the nested chain. Logical references must be valid. | R2-3 |
| EF Core + SQL Server provider | **Reuse** (technology) | Domain services only | The paper's stack. Because the same domain images run in both stacks, the ORM choice cannot bias the comparison. | R2-5 (ORM efficiency) |
| Repository/service layering | **Refactor** | Thin query handlers using `AsNoTracking`, with a fixed column projection per domain contract (AD-25) | Avoid tracking overhead. One fixed query per endpoint. | R2-5 |
| `AddDbContext` (not pooled), default SqlClient pool | **Replace** | `AddDbContextPool` and explicit pool settings, identical in all services (AD-33) | Defaults would saturate at 1000 threads and hide the protocol effect | R2-5 |
| EF migrations and `HasData` seeds | **Replace** | SQL DDL plus a seeded data generator | Seeds with `DateTime.UtcNow` are non-deterministic. The dataset must be reproducible. | A-4 |
| HotChocolate GraphQL server (13.9.15) | **Replace** (same library, new major version) | `Lms.Edge.GraphQL` on a pinned current HotChocolate release, using DataLoaders | Keeps the library choice for continuity. Moving from 13 to 14/15 is a rewrite because of breaking changes and new defaults such as cost analysis (AD-24). | R1-7 |
| `BookQueries` / `BookMutations` | **Replace** | New schema (`books`, `members` → `loans` → `book`). Mutations only if D-5 is approved. | The old schema has no relationships | R1-7, R2-4 |
| REST controllers (`BooksController`, `UserController`, `LoanController`) | **Refactor** | `Lms.Edge.Rest` controllers: flat, composite and resource routes | `[ApiController]` style is kept for continuity. Routes are redesigned. | R1-7, R2-3 |
| `Lms.APIGateway` (template) | **Replace** | The edge services take this role | It had no routing | R1-4 |
| `Lms.NotificationService` (both stacks) | **Drop** (AD-03) | — | Plays no role in read workloads. Was empty. | R1-4 (CPU scope) |
| Auth service in Fig. 1 (no code) | **Replace if D-4** | JWT validation middleware in both edges | Symmetric security overhead | R2-4 |
| gRPC `.proto` files and commented gRPC host | **Drop** | — | Not part of the study | — |
| `Lms.BookService/Controllers/UserController.cs`, the Salesforce `User` model, and its `Newtonsoft.Json` package reference | **Drop** | — | Unrelated code. A second serializer in the REST stack would be a confound. | — |
| `LibraryManagementSystem` root template project | **Drop** | — | WeatherForecast template | — |
| `appsettings.json` connection strings | **Replace** | Env vars from Secrets Manager | Security. Avoids leaking the endpoint. | — |
| Kestrel `Http2` config (GraphQL) and `UseHttpsRedirection` (REST) | **Replace** | One shared host configuration (AD-05) | Removes the protocol confound | B-9 |
| Swagger (REST, always on in BookService) | **Drop from measured builds** | Optional in Development only | Removes asymmetric middleware | Confounder |
| `global.json` (SDK 8.0.403) | **Replace** | A pin matching AD-04 | The local machine has 10.0.401 | — |

**Reuse ratio:** almost no code is reused verbatim. The domain model, the technology stack and the naming are kept. This is deliberate: the requirements, not compatibility, drive the design.

## 4. Location of the new implementation [DECISION AD-14]

```
<workspace root>/
├── existing-source/          (unchanged, read-only reference)
├── research-documents/       (unchanged, read-only)
├── benchmark/                (NEW: system under test)
│   ├── global.json, Directory.Build.props, Directory.Packages.props   (central version pinning)
│   ├── src/  Lms.Catalog.Service · Lms.Members.Service · Lms.Loans.Service
│   │         Lms.Edge.Rest · Lms.Edge.GraphQL · Lms.DomainClient · Lms.Telemetry · Lms.Hosting
│   ├── tests/  unit, integration (Testcontainers SQL Server), equivalence
│   ├── data/   DDL, generator, dataset provenance
│   ├── docker/ Dockerfiles, docker-compose for local runs
│   └── infra/  Terraform or CDK (DRAFT — not applied)
├── experiment/  jmeter/ · runner/ · config/ (B-records) · equivalence/ · raw/ (immutable)
├── analysis/    Python pipeline
└── docs/        milestone2/, decisions/, audits/, results/, reproducibility/
```

`Lms.Hosting` is a shared host-configuration library. Both edges and all domain services call the same `AddBenchmarkHost()`, which sets Kestrel limits and protocols, logging levels, pools and telemetry.

HotChocolate does not use MVC `JsonOptions`. `Lms.Hosting` therefore configures the System.Text.Json options and HotChocolate's result-formatter options **separately but from one shared settings object** (encoder, date and decimal handling; AD-32). A test asserts that the two produce identical bytes for the shared fields. Configuration symmetry is then auditable in one place, and the result is listed in `docs/milestone2/symmetry_checklist.md` (Phase 4 gate).

**Version control [DECISION AD-14]:** the workspace root is not a git repository.
- Recommended: `git init` at the workspace root. Add `existing-source/` as an ignored, untouched directory (it keeps its own `.git`). Commit `benchmark/`, `experiment/` (excluding `raw/`), `analysis/` and `docs/`. Each commit SHA used in a campaign goes into the C-5 README.
- Alternative: a new GitHub repository for the replication package. R1-11 already flagged that the published repository name is misspelled.
