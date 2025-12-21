## 2025-05-23 - O(N^2) Grouping Anti-Pattern
**Learning:** Found an inefficient pattern where a list is iterated, and inside the loop, `Where` is used to find items matching the current item's property. This results in O(N^2) complexity.
**Action:** Use `GroupBy` to organize data in O(N) before iterating. This is especially critical for time-series data like video segments where we process thousands of items.

## 2025-12-11 - Repeated Static File I/O
**Learning:** Parsing static JSON files (metadata) for every request creates a significant I/O bottleneck, even with PLINQ. `AsParallel` masks latency but consumes threads.
**Action:** Use `IMemoryCache` with long expiration for immutable file-based metadata to serve subsequent requests instantly.

## 2025-12-12 - Nested Parallelism Overhead
**Learning:** Found nested `AsParallel()` calls where the inner loop processed small collections (< 50 items). The overhead of partitioning and task scheduling outweighed the benefits of parallel execution.
**Action:** Remove inner `AsParallel()` when the outer loop is already parallelized and the inner workload is lightweight/small.

## 2025-12-14 - High Frequency Blazor Events
**Learning:** High-frequency events like `ontimeupdate` (~4-60Hz) cause "Event Storms" in Blazor, triggering excessive `StateHasChanged` calls and JS Interop round-trips.
**Action:** Throttle these events inside the component (e.g., every 200ms) before invoking the `EventCallback` to the parent.

## 2025-12-19 - Conditional Event Binding
**Learning:** Even with throttling, attaching high-frequency event listeners (like `@ontimeupdate`) in Blazor incurs JS Interop overhead for every event fired by the browser, because the event is marshaled to C# before the handler decides to return.
**Action:** Use conditional attribute binding `@ontimeupdate="@(Callback.HasDelegate ? Handler : null)"` to prevent the event listener from being attached at all when the parent component doesn't need it.

## 2025-12-20 - N+1 JS Interop in Synchronization Loops
**Learning:** Polling state from multiple UI components (like video players) individually via JS Interop inside a frequent timer loop creates massive overhead (N calls per tick).
**Action:** Batch these operations into a single JS function call that returns an array of values (e.g., `getVideoTimes`), reducing Interop calls from N to 1 per tick.
