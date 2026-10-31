# Protocol Status Tracker

| | |
|---|---|
| Last updated | 2026-11-01 (Phase 0) |
| **Overall state** | **ARCHITECTURAL BASELINE APPROVED (DECISION-002). The experimental protocol is NOT frozen.** Dataset-dependent definitions are provisional, and the Tier 2/3/4 parameters are open. |
| Rule | An item moves to "approved" only when a DECISION file records it. The protocol is declared frozen only in a future `PROTOCOL_FREEZE.md`, created after every item in §2 and §3 is closed. |

## 1. Approved (recorded)

| Item | Record |
|---|---|
| Baseline code is reference only; redesign permitted; assets preserved | DECISION-001 |
| AP-1 … AP-10 architecture; Tier 1: AD-01, AD-25, D-2, AD-07, AD-09, AD-10, AD-04, AD-05 (HTTP/1.1), AD-11, AD-36, AD-15, D-3, AD-14 | DECISION-002 |
| O-1, O-2 required fields (meaning); O-3 CRIR and Required Bytes (CL-1) | DECISION-003 |
| O-4 member and loan representations; O-8 record-set semantics | DECISION-004 |
| O-5 serialization rules 1–6 | DECISION-005 |
| O-6, O-9, O-10, O-11 (D-4a build toggle, D-5 no writes), O-13 | DECISION-006 |
| O-12 / AM-2 `_dbq.csv` aggregation | DECISION-007 |
| O-14 version control | DECISION-008 |

## 2. Provisional (approved in principle; pending verification)

| Item | Pending on | Unblocks |
|---|---|---|
| Book column set and cleaning rules (O-4 books) | O-7 dataset download and inspection | Phase 2 data |
| Book field names in O-1, O-2 and the CRIR | O-7 | Phase 2 golden files, Phase 4 contracts |
| `book` DDL, dataset volumes, dataset licence status | O-7 | Phase 2 |
| Golden files and CRIR oracle | O-7 + Phase 2 | Phase 5 |
| Rule 7 string-escaping encoder `Create(UnicodeRanges.All)` | SER-1 to SER-6 + researcher acknowledgement | Phase 4 exit |
| Decimal and date GraphQL scalar mapping (rules 5–6) | Verification against the pinned HotChocolate version | Phase 4 exit |

## 3. Open decisions (not approved)

- **Tier 2 (after local validation, before Phase 9):**
  - AD-28/CL-4: extraction inside the N-RM task;
  - AD-17/AM-1: analysis window;
  - AD-31/CL-2: inclusion rule;
  - AD-35/AM-5: sensitivity analysis;
  - AD-20/CL-3: CPU basis;
  - AD-30a: overhead threshold;
  - AD-24: HotChocolate settings;
  - AD-18 and AD-19 (CL-7): warm-up duration, CI target;
  - AD-27: client RTT;
  - N, O, M, L;
  - D-8, P-1, P-4;
  - AD-02, AD-06, AD-13: deployment shape and IaC tool.
- **Tier 3 (authorised AWS pilot only):**
  - AUTH-AWS-1 and AD-12/AM-4: deployment and pilot;
  - AD-26/AD-29/AM-3: daily redeploy;
  - B-1, B-2, B-3, B-5, B-9;
  - P-2, P-3;
  - D-7 and the number of days;
  - AD-33: pool limits;
  - AD-30b: clock-skew tolerance;
  - D-4b: running the auth set;
  - D-6: prices;
  - AUTH-AWS-2: the campaign.
- **Tier 4 (paper, Milestone 3):** doc 06 §4, items 1–12.
- **Amendments and clarifications not yet approved:** AM-1, AM-3, AM-4, AM-5, AM-6; CL-2 … CL-7.
