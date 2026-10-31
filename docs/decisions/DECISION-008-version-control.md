# DECISION-008: Version control setup (O-14)

| Field | Value |
|---|---|
| Date | 2026-11-01 |
| Decided by | Researcher, in the "Approval of Tier 1 Decisions" written instruction (approved as recommended in doc 08). The Phase 0 instruction was "Initialize version control safely without changing the original source repository or research documents." |
| Status | **Approved; executed in Phase 0** (see `docs/milestone2/PHASE0_COMPLETION.md`) |

- `git init` at the workspace (repository) root.
- `.gitignore` excludes:
  - `existing-source/`, whose own `.git` is untouched and not converted to a submodule;
  - `research-documents/`, preserved through a SHA-256 manifest because it contains confidential review correspondence;
  - `experiment/raw/`, `experiment/pilot/` and `benchmark/data/source/`;
  - local development-tool configuration (kept untracked);
  - secret patterns.
- No remote is added. Nothing is pushed. Global git configuration is not changed.
- `.gitattributes` normalises text line endings inside this repository only, so that committed file hashes are stable.
