## 2025-05-23 - O(N^2) Grouping Anti-Pattern
**Learning:** Found an inefficient pattern where a list is iterated, and inside the loop, `Where` is used to find items matching the current item's property. This results in O(N^2) complexity.
**Action:** Use `GroupBy` to organize data in O(N) before iterating. This is especially critical for time-series data like video segments where we process thousands of items.

## 2025-12-11 - Repeated Static File I/O
**Learning:** Parsing static JSON files (metadata) for every request creates a significant I/O bottleneck, even with PLINQ. `AsParallel` masks latency but consumes threads.
**Action:** Use `IMemoryCache` with long expiration for immutable file-based metadata to serve subsequent requests instantly.

## 2025-12-12 - Nested Parallelism Overhead
**Learning:** Found nested `AsParallel()` calls where the inner loop processed small collections (< 50 items). The overhead of partitioning and task scheduling outweighed the benefits of parallel execution.
**Action:** Remove inner `AsParallel()` when the outer loop is already parallelized and the inner workload is lightweight/small.
