# DECISION-001: Status of the existing codebase

| Field | Value |
|---|---|
| Date | 2026-11-01 |
| Decided by | Researcher. Given as the written "Milestone 2 — Experimental Architecture Redesign" instruction. |
| Recorded by | AI-assisted development tooling (transcribed; no interpretation added) |
| Affects | Project working rules, `docs/milestone2/01`–`06` |

## Decision

1. The application in `existing-source/MicoserviceRestvsGraphql/` is **not the source of truth** for the redesigned experiment. It is reference material and a potential source of reusable components.
2. The implementation is based on these sources:
   - the original research questions and objectives;
   - the IEEE Access reviewer feedback;
   - the Milestone 1 revision plan;
   - the Milestone 2 specification, which takes precedence where it explicitly supersedes Milestone 1.
3. The architecture may be substantially redesigned. Outdated design decisions are not preserved for compatibility.
4. The original source and results are retained as historical evidence and are never overwritten or destroyed.
5. Scientific protocol decisions require researcher approval. Neither protocol may be optimised to force a conclusion.

## Not decided by this record

- Every item in `docs/milestone2/06_Methodology_Decisions_Required.md`.
- The location of the new code (AD-14).
