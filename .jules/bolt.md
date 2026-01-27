# Bolt's Journal

## 2025-01-28 - Optimizing Blazor Client Filtering
**Learning:** LINQ in hot paths (like filtering a large list in Blazor WASM) can introduce unnecessary allocations and iterations. Specifically, `Select().Concat().Distinct().ToHashSet()` is O(3N) and creates multiple intermediate iterators and collections.
**Action:** Replace complex LINQ chains with explicit loops when performance matters, especially in WASM where GC pressure can cause jank.

## 2025-01-29 - Array.IndexOf vs LINQ for Navigation
**Learning:** Using `Array.IndexOf` combined with direct array access is significantly more efficient than LINQ `FirstOrDefault` or manual loops for finding adjacent items in a sorted list. It avoids delegate allocations and utilizes vectorized native search.
**Action:** Prefer `Array.IndexOf` for navigation logic when the item instance is known to be in the collection.

## 2025-01-29 - O(1) Bounds Checking on Sorted Arrays
**Learning:** `Any()` on a sorted collection to check range boundaries is O(N) in the worst case (failure). Replacing it with direct access to the first/last elements is O(1). This is critical for high-frequency events like `OnMouseWheel`.
**Action:** Always leverage sort order to perform O(1) boundary checks instead of LINQ scanning.
