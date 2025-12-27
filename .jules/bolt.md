# Bolt's Journal

## 2025-01-28 - Optimizing Blazor Client Filtering
**Learning:** LINQ in hot paths (like filtering a large list in Blazor WASM) can introduce unnecessary allocations and iterations. Specifically, `Select().Concat().Distinct().ToHashSet()` is O(3N) and creates multiple intermediate iterators and collections.
**Action:** Replace complex LINQ chains with explicit loops when performance matters, especially in WASM where GC pressure can cause jank.
