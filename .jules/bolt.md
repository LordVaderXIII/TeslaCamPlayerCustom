# Bolt's Journal

## 2025-01-28 - Optimizing Blazor Client Filtering
**Learning:** LINQ in hot paths (like filtering a large list in Blazor WASM) can introduce unnecessary allocations and iterations. Specifically, `Select().Concat().Distinct().ToHashSet()` is O(3N) and creates multiple intermediate iterators and collections.
**Action:** Replace complex LINQ chains with explicit loops when performance matters, especially in WASM where GC pressure can cause jank.

## 2025-01-29 - Caching Allocations in Virtualized Lists
**Learning:** In Blazor `Virtualize` components, helper methods called in the rendering loop (like `GetClipIcons`) run frequently. Returning `new T[]` from these methods creates significant GC pressure, causing UI jank during scrolling.
**Action:** Cache common array results in `static readonly` fields to eliminate allocations for the majority of rows.
