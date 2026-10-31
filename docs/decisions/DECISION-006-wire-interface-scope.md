# DECISION-006: Wire settings, interface details and scope (O-6, O-9, O-10, O-11, O-13)

| Field | Value |
|---|---|
| Date | 2026-11-01 |
| Decided by | Researcher, in the "Approval of Tier 1 Decisions" written instruction. All approved as recommended in doc 08. |
| Status | **Approved** |

- **O-6: compression and TLS.** No response compression and no TLS inside the VPC, for both stacks. Both are disclosed as out of scope.
- **O-9: ID-list wire format.**
  - One comma-separated query parameter (`?memberIds=1,2,3`, `?ids=…`), used by the N-RM calls and the internal calls alike.
  - Lists are sorted and de-duplicated by `Lms.DomainClient` (and by the plan, for N-RM).
  - GraphQL is sent as `POST` JSON with `variables` and `operationName`.
- **O-10: Notification and Auth services.** Both are omitted from the measured system. The paper's Fig. 1 is to be redrawn (Tier 4).
- **O-11: auth and writes scope.**
  - D-4a: build symmetric JWT-validation middleware in both edges, **off by default**. Whether to run the reduced set (levels 100 and 1000) is a Tier 3 decision (D-4b) and is not approved here.
  - D-5: no write workload. The paper states the exclusion and its reason: writes conflict with the data freeze and the no-restart rule.
- **O-13: REST edge style.** `[ApiController]` controllers, disclosed as the REST implementation style.
