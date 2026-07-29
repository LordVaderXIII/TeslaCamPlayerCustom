# Bolt's Journal

## 2025-01-28 - Optimizing Blazor Client Filtering
**Learning:** LINQ in hot paths (like filtering a large list in Blazor WASM) can introduce unnecessary allocations and iterations. Specifically, `Select().Concat().Distinct().ToHashSet()` is O(3N) and creates multiple intermediate iterators and collections.
**Action:** Replace complex LINQ chains with explicit loops when performance matters, especially in WASM where GC pressure can cause jank.

## 2025-01-29 - Array.IndexOf vs LINQ for Navigation
**Learning:** Using `Array.IndexOf` combined with direct array access is significantly more efficient than LINQ `FirstOrDefault` or manual loops for finding adjacent items in a sorted list. It avoids delegate allocations and utilizes vectorized native search.
**Action:** Prefer `Array.IndexOf` for navigation logic when the item instance is known to be in the collection.

## 2025-01-29 - Optimize Date Picker Scroll Logic
**Learning:** In Blazor WASM, iterating large collections on frequent events like `mousewheel` can cause UI jank. Even simple LINQ operations like `Any()` are O(N).
**Action:** When the collection sort order is known (e.g., sorted descending by date), replace O(N) `Any()` checks with O(1) array access (checking first/last elements) to determine range bounds.
