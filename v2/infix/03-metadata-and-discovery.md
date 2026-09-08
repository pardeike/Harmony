# Metadata, discovery, sorting

## Patch model extensions
- Extend Patch with:
  - InnerMethod: { MethodBase method; int[] positions; }
  - Patch type: InnerPrefix, InnerPostfix.

## Discovery
- In PatchClassProcessor, when a method has [HarmonyInfixPrefix] or [HarmonyInfixPostfix], read [HarmonyInfixTarget] to resolve:
  - method (inner target),
  - positions (indices to apply at).
- Attach InnerMethod to the created Patch.

## Grouping and sorting
- At emission time, for each inner MethodBase:
  - Count total occurrences in the outer IL.
  - For occurrence k in 1..total, filter patches where positions match (-1 means all).
  - Sort with existing PatchSorter by priority and before/after relations.

No new global ordering rules.
