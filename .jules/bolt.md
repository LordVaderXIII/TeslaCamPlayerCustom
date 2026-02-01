# Bolt's Journal

## 2025-01-28 - Optimizing Blazor Client Filtering
**Learning:** LINQ in hot paths (like filtering a large list in Blazor WASM) can introduce unnecessary allocations and iterations. Specifically, `Select().Concat().Distinct().ToHashSet()` is O(3N) and creates multiple intermediate iterators and collections.
**Action:** Replace complex LINQ chains with explicit loops when performance matters, especially in WASM where GC pressure can cause jank.

## 2025-01-29 - Array.IndexOf vs LINQ for Navigation
**Learning:** Using `Array.IndexOf` combined with direct array access is significantly more efficient than LINQ `FirstOrDefault` or manual loops for finding adjacent items in a sorted list. It avoids delegate allocations and utilizes vectorized native search.
**Action:** Prefer `Array.IndexOf` for navigation logic when the item instance is known to be in the collection.

## 2026-02-01 - [Parallel.ForEachAsync vs Task.WhenAll]
**Learning:** `Task.WhenAll` with `Select` eagerly creates tasks for the entire collection, consuming memory for state machines even if not running. For large IO-bound collections, `Parallel.ForEachAsync` with `MaxDegreeOfParallelism` provides better resource control and lower memory footprint.
**Action:** Prefer `Parallel.ForEachAsync` over `Task.WhenAll` for batch processing large collections.
