# DECISION-007: Protocol amendment AM-2, `_dbq.csv` per-second aggregation (O-12)

| Field | Value |
|---|---|
| Date | 2026-11-01 |
| Decided by | Researcher, in the "Approval of Tier 1 Decisions" written instruction (approved as recommended) |
| Status | **Approved: amendment to M2 C-3** |
| M2 text amended | C-3: "Query time with query_id and duration_ms" |

**Amended format:** `<stem>_dbq.csv` with columns `timestamp_utc, service, query_id, count, mean_ms, p95_ms, max_ms`. There is one row per second per service per `query_id`. Timestamps fall on wall-clock second boundaries, aligned with `_cpu.csv` and `_mem.csv` (C-2/C-3 "same timestamps").

**Reason:** per-command rows at level 1000 would generate logging I/O that perturbs the system under test (doc 04 §4.3).

**Verification:** test TEL-7 checks that the per-second `count` sums to the number of executed domain queries.
