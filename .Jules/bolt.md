## 2025-05-23 - O(N^2) Grouping Anti-Pattern
**Learning:** Found an inefficient pattern where a list is iterated, and inside the loop, `Where` is used to find items matching the current item's property. This results in O(N^2) complexity.
**Action:** Use `GroupBy` to organize data in O(N) before iterating. This is especially critical for time-series data like video segments where we process thousands of items.
