# Symmetry Checklist (doc 01 §5; Phase 4 exit criterion)

| | |
|---|---|
| Date | 2026-11-01 |
| Code | `benchmark/` (uncommitted working tree at the time of writing) |
| State | Evidence is complete for every row of doc 01 §5. **Researcher approval is pending** (Phase 4 gate). |

| doc 01 §5 row | Mechanism | Evidence (file:line) | Verified by |
|---|---|---|---|
| Data | One dataset, deterministic generator; same databases for both stacks | `src/Lms.Data/Dataset/DatasetGenerator.cs`; `DataFreezeVerifier.cs` | `DatabaseTests`, `lms-bench db verify` (21/21 PASS) |
| SQL | Same domain-service code for both edges; tagged queries; fixed projections; `AsNoTracking` (pooled context) | `src/Lms.Catalog.Service/Program.cs:20-21`, `src/Lms.Members.Service/Program.cs:17`, `src/Lms.Loans.Service/Program.cs:17`, `src/Lms.Data/Persistence.cs:105-110` | `CapturedSqlTests` → `docs/api-contracts/captured-sql.md` |
| Internal calls | One shared client; IDs sorted and de-duplicated | `src/Lms.DomainClient/DomainClient.cs:85,91` | `FiveConfigurationTests.Nested_configurations_make_the_same_three_internal_calls` (identical URL lists for N-RC, N-G and N-RM), `Flat_configurations_make_exactly_one_identical_internal_call` |
| Records | Same offset and limit, PK ordering, deterministic data | `errors-and-pagination.md §1–2`, the service code above | `FiveConfigurationTests` (equivalence with the oracle at offset 0 and 3) |
| Fields | GraphQL selects exactly A-6; REST full representation | `GraphQLDocuments` in `src/Lms.RequiredBytes/Equivalence.cs`; DTOs in `src/Lms.Contracts/Dtos.cs` | `GraphQLEdgeTests.FlatBooks_returns_exactly_required_fields`, `RestEdgeTests.*full_representation*` |
| Runtime | .NET 10 for all; Server GC + concurrent, pinned | `global.json`; `Directory.Build.props:12-13` | Build. `artifacts/BUILD_INFO.md` |
| Serialization | Shared policy SP-1. System.Text.Json and HotChocolate use the same encoder; camelCase; nulls written | `src/Lms.Hosting/BenchmarkJson.cs:19,33-37`; `src/Lms.Edge.Rest/Program.cs:11`; `src/Lms.Edge.GraphQL/Program.cs:19,29` | `SerializationPolicyTests` (unit), `SerializationParityTests` (cross-stack token equality over 300 fixture books), identical formatting delta in `bytes_summary.csv` |
| HTTP | Kestrel Http1 only, no `Server` header (both edges and all domain services); internal client HTTP/1.1 exact, `identity` encoding, no decompression | `src/Lms.Hosting/BenchmarkHost.cs:22-23`; `src/Lms.DomainClient/DomainClient.cs:138,140,147` | `RestEdgeTests.Response_headers_follow_contract`, `GraphQLEdgeTests.Response_headers_follow_contract` |
| Caching | No caching middleware or headers on either stack. GraphQL GET and batching disabled. | `src/Lms.Hosting/BenchmarkHost.cs` (none registered); `src/Lms.Edge.GraphQL/Program.cs:42-43` | Header tests; `Get_requests_are_not_served`; `Request_batching_is_rejected` |
| Pools and limits | SqlClient pool defaults (100) and `DbContext` pooling in every domain service; one `SocketsHttpHandler` per domain service per edge, with identical settings | `src/Lms.Data/Persistence.cs:105`; `src/Lms.DomainClient/DomainClient.cs:23-24,148,151` | Config review. Values are Tier 3 (AD-33). |
| Logging | `Microsoft.*` and `System.Net.Http` at Warning in all five | `src/Lms.Hosting/BenchmarkHost.cs:26-27` | Config review |
| Error handling | Same problem+json writer in all services; no exception detail | `src/Lms.Hosting/ApiProblems.cs`, `BenchmarkHost.cs:36,45`; `src/Lms.Edge.GraphQL/Schema.cs` (`ContractErrorFilter`) | `RestEdgeTests`, `GraphQLEdgeTests`, `DomainServiceTests` |

## Known asymmetries (intended or open)

| Asymmetry | Status |
|---|---|
| GraphQL parses, validates and executes resolvers; REST uses MVC routing and model binding | **Intended**: the protocol cost under study |
| HotChocolate cost analysis (default on) | Recorded; AD-24 (Tier 2) |
| ⚠ HotChocolate `MaxConcurrentExecutions` = 64 by default; REST has no limiter | **Open: AD-37** (CHANGELOG C-1) |
| `Vary: Accept` header on GraphQL responses only | Recorded (C-2) |
| Chunked framing (REST) vs Content-Length (GraphQL) | Recorded (C-3) |
