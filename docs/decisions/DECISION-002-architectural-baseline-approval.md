# DECISION-002: Architectural Baseline Approval

| Field | Value |
|---|---|
| Label | **ARCHITECTURAL BASELINE: APPROVED.** This is **not** a full protocol freeze. |
| Date | 2026-11-01 |
| Decided by | Researcher (workspace owner), in two written instructions on 2026-11-01: "Phase 0 Approval and Implementation Preparation" and "Approval of Tier 1 Decisions" |
| Recorded by | AI-assisted development tooling (transcribed; qualifications kept verbatim in meaning) |
| Supersedes | — (DECISION-001 remains in force) |
| Open protocol items | Tracked separately in `docs/milestone2/PROTOCOL_STATUS.md` |

## Approved direction (researcher items AP-1 … AP-10)

1. **AP-1:** A new experimental system under `benchmark/`. All original source code and research documents are preserved unchanged.
2. **AP-2:** A protocol-neutral domain architecture with separate REST and GraphQL edge services (doc 01, Option A).
3. **AP-3:** .NET 10, consistent HTTP/1.1 configuration, and documented serializer settings.
4. **AP-4:** All five configurations (F-R, F-G, N-RC, N-RM, N-G) across the 25 cells.
5. **AP-5:** The batched three-call REST chain for the primary multi-call workload.
6. **AP-6:** Deterministic datasets with separate, service-owned databases.
7. **AP-7:** Full REST representations and GraphQL required-field selection.
8. **AP-8:** GraphQL database projection pushdown is excluded from the primary controlled experiment, and the limitation is documented explicitly (doc 01 §1.1).
9. **AP-9:** Memory and database instrumentation are included.
10. **AP-10:** Experimental neutrality, reproducibility and reviewable evidence are preserved.

## Tier 1 items closed by this approval (doc 08 §1)

| Doc 06 T1 item | Closed by |
|---|---|
| AD-01 topology | AP-2 |
| AD-25 pushdown excluded and documented | AP-8 |
| D-2 both nested REST variants | AP-4 |
| AD-07 batched 3-call chain | AP-5. Wire format in DECISION-006. |
| AD-09 full REST representation | AP-7. Content defined in DECISION-004. |
| AD-10 composite embeds full representations | AP-7 |
| AD-04 .NET 10 | AP-3 |
| AD-05 HTTP/1.1 | AP-3. Compression and TLS in DECISION-006. |
| AD-11 database per service | AP-6 |
| AD-36 data-freeze verification (implementation detail) | AP-6, AP-10 |
| AD-15 fixed request per configuration | M2 §2.3.1 as written; cache-hot nature disclosed |
| D-3 memory and DB instrumentation | AP-9 |
| AD-14 `benchmark/` | AP-1. Version control in DECISION-008. |

## Related records

- DECISION-003: required information and Required Bytes (O-1, O-2, O-3). Partly **provisional**.
- DECISION-004: representations, dataset and record sets (O-4, O-7, O-8). Partly **provisional**.
- DECISION-005: serializer policy (O-5, approved with modification). Encoder selection **provisional**.
- DECISION-006: wire settings, interface details and scope (O-6, O-9, O-10, O-11, O-13).
- DECISION-007: protocol amendment AM-2, `_dbq.csv` aggregation (O-12).
- DECISION-008: version control (O-14).

## Explicitly not approved by this record

- Every Tier 2, Tier 3 and Tier 4 item in doc 06.
- The amendments AM-1, AM-3, AM-4, AM-5 and AM-6.
- The clarifications CL-2 to CL-7. CL-1 is covered by DECISION-003.
- Any AWS deployment or expenditure (AUTH-AWS-1 and AUTH-AWS-2 do not exist).
