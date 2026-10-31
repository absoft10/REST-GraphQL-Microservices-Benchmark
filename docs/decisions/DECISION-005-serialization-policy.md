# DECISION-005: Shared JSON serialization policy (O-5, approved with modification)

| Field | Value |
|---|---|
| Date | 2026-11-01 |
| Decided by | Researcher. **Approved with modification:** "Do not mandate UnsafeRelaxedJsonEscaping without evaluating its suitability and security implications. Choose a documented shared JSON serialization policy that ensures equivalent scalar values, date formats, number formats, naming and null handling. Test serialization differences explicitly. Do not artificially equalize complete protocol response sizes." |
| Status | Policy rules 1–6 **approved**, as a direct consequence of the modification. The string-escaping encoder (rule 7) is **PROVISIONAL**: it was selected with AI-assisted development tooling at the researcher's request, and it is subject to the SER tests and researcher acknowledgement. |
| Replaces | The O-5 recommendation in doc 08 (UnsafeRelaxedJsonEscaping), which is withdrawn |

## Policy SP-1 (applies identically to `Lms.Edge.Rest`, `Lms.Edge.GraphQL` and `Lms.DomainClient`)

1. **Encoding:** UTF-8. No BOM, no indentation, no trailing newline.
2. **Naming:** the camelCase names defined by the API contract (doc 03 §3, DECISION-003/004). They are identical in both stacks and checked against the contract list in tests.
3. **Nulls:** every declared field is written, including when its value is null (`DefaultIgnoreCondition = Never` in System.Text.Json; GraphQL writes selected nullable fields as `null` by specification). "Absent" never stands in for null.
4. **Integers:** JSON integers.
5. **Decimals** (`price`): JSON numbers in invariant culture at the stored scale (`DECIMAL(10,2)`, so `12.50`). The GraphQL scalar mapping is pinned so that it emits the same token. Verify at implementation.
6. **Dates:** `DATE` columns map to `DateOnly` and are serialised as `"YYYY-MM-DD"` in both stacks. The GraphQL scalar mapping is pinned. Verify at implementation.
7. **String escaping (PROVISIONAL):** `JavaScriptEncoder.Create(UnicodeRanges.All)` in both stacks.

## Evaluation of the escaping options (rule 7)

| Option | Behaviour (per .NET documentation; verified against the pinned runtime in SER-1) | Security | Bytes | Assessment |
|---|---|---|---|---|
| `JavaScriptEncoder.Default` (the ASP.NET Core default) | Escapes all non-ASCII as `\uXXXX`, and escapes HTML-sensitive characters (`< > & ' " +` and backtick) | Safest for HTML embedding | Inflates non-ASCII text up to 6× per character | Acceptable, but book titles with non-ASCII characters inflate REST and GraphQL equally only if HotChocolate uses the same encoder |
| **`JavaScriptEncoder.Create(UnicodeRanges.All)`** | Emits non-ASCII letters as UTF-8. **Still escapes the HTML-sensitive characters** and characters on the encoder's global block list. | **Keeps the HTML-sensitive escaping of the default.** No relaxation for script/HTML-context injection compared with `Default`. | No non-ASCII inflation | **Selected (provisional):** security equal to the default for HTML contexts, and realistic production output |
| `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` | Does **not** escape HTML-sensitive characters | Microsoft documents it as unsafe if the output is embedded in HTML or `<script>` without further encoding (an XSS risk for clients that do so) | Smallest | **Rejected.** It lowers the security posture, and minimal bytes are not a goal. |

Benchmark clients are JMeter only, with no browser. The choice therefore does not affect the measurement's security exposure. It is made for realism and for symmetry.

## What this policy does **not** do (researcher qualification)

It does **not** equalise complete response sizes. The following remain as they are and are reported as genuine differences:
- the GraphQL `data` envelope;
- the extra REST fields;
- the N-RM repeated keys and per-call wrappers;
- HTTP headers and framing;
- residual encoder differences that cannot be configured away (each is recorded).

## Fallback (if HotChocolate's formatter cannot apply rules 5–7 identically)

Choose the setting HotChocolate supports, configure System.Text.Json to match it, and record the deviation in B-4. If no common setting exists for a rule, keep each stack's behaviour, record the difference, and include it as a reported, genuine formatting delta. Either way it goes back to the researcher before Phase 5 closes.

## Serialization tests (required; Phase 4 exit criteria)

| Test | Content | Pass |
|---|---|---|
| SER-1 Scalar parity | A synthetic fixture with values for every field type:<br>• strings: ASCII, Latin-1 accents, CJK, emoji and surrogate pairs, `< > & ' " +` and backtick, control characters, backslash, empty string;<br>• decimals: 0.00, 12.50, 99999999.99;<br>• dates: min, max, leap day;<br>• ints: 0 and max;<br>• nulls.<br>Each is serialised through both edges' configured writers. | Each field's JSON value token is byte-identical across stacks, or the difference is listed under the fallback rule |
| SER-2 Null semantics | Nullable fields set to null | Explicit `null` in both stacks. The key is present. |
| SER-3 Naming | All fields | Names equal the contract list |
| SER-4 Compactness | Full bodies | No insignificant whitespace |
| SER-5 Escaping policy | HTML-sensitive characters | Escaped in both stacks, as the policy requires |
| SER-6 Size reporting | Full bodies of every configuration | Size differences are **reported** (decomposition, DECISION-003) and never adjusted |

Fixture strings are synthetic test values, not dataset content. The test code and expected outputs are committed.
