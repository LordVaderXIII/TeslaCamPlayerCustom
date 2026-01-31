# Bolt's Journal

## 2025-01-28 - Optimizing Blazor Client Filtering
**Learning:** LINQ in hot paths (like filtering a large list in Blazor WASM) can introduce unnecessary allocations and iterations. Specifically, `Select().Concat().Distinct().ToHashSet()` is O(3N) and creates multiple intermediate iterators and collections.
**Action:** Replace complex LINQ chains with explicit loops when performance matters, especially in WASM where GC pressure can cause jank.

## 2025-01-29 - Array.IndexOf vs LINQ for Navigation
**Learning:** Using `Array.IndexOf` combined with direct array access is significantly more efficient than LINQ `FirstOrDefault` or manual loops for finding adjacent items in a sorted list. It avoids delegate allocations and utilizes vectorized native search.
**Action:** Prefer `Array.IndexOf` for navigation logic when the item instance is known to be in the collection.

## 2025-01-30 - Unexpected "Single Clip" Logic
**Learning:** The "Recent Clips" grouping logic contained a gap check that always evaluated to true (checking if an older clip starts before a newer clip ends), effectively merging all recent footage into a single giant clip. This meant complex grouping logic (GroupBy/OrderBy) was performing unnecessary work.
**Action:** When analyzing grouping logic, check the conditions carefully—sometimes the complex logic simplifies to "Sort and Dump", allowing O(N log N) + O(N) optimization with minimal allocations.
