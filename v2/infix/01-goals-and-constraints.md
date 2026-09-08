# Goals and constraints

## Goals
- Infix patches that target specific call sites inside an outer method.
- No IL data‑flow analysis. Leverage the call‑site stack contract.
- Reuse Harmony’s existing parameter injection machinery. Extend, do not fork.
- Preserve patch ordering semantics and “skip original” behavior.

## Non‑goals
- Full support for exotic IL patterns in v1 (see limits).
- Large API changes. Keep attributes and metadata minimal.

## Constraints
- Only replace the call (or callvirt/calli/newobj if later supported).
- If the original call is preceded by call‑only prefixes (e.g., constrained. or tail.), absorb and re‑emit them inside the infix block right before the re‑issued call.
- End with the same stack effect as the original call (push result if non‑void, no push if void).
