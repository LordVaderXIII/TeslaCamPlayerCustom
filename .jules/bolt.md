OK
## 2024-06-25 - [Memory Optimization in File Sync]
**Learning:** In scenarios with massive file counts (like TeslaCam archives), calling  creates a massive in-memory collection that can cause memory spikes and potential OOMs.
**Action:** Always prefer streaming iteration (foreach) over  when checking against a known dataset, to keep memory usage flat regardless of file count.

## 2024-06-25 - [Concurrency Limits in Batch Processing]
**Learning:** Unbounded  on file operations (like ) can saturate the thread pool and disk I/O, leading to perceived hangs or timeouts.
**Action:** Use  to strictly limit concurrency when processing batch I/O operations.
## 2024-06-25 - [Memory Optimization in File Sync]
**Learning:** In scenarios with massive file counts (like TeslaCam archives), calling `Directory.EnumerateFiles(...).ToHashSet()` creates a massive in-memory collection that can cause memory spikes and potential OOMs.
**Action:** Always prefer streaming iteration (foreach) over `EnumerateFiles` when checking against a known dataset, to keep memory usage flat regardless of file count.

## 2024-06-25 - [Concurrency Limits in Batch Processing]
**Learning:** Unbounded `Task.WhenAll` on file operations (like `ParseClipAsync`) can saturate the thread pool and disk I/O, leading to perceived hangs or timeouts.
**Action:** Use `SemaphoreSlim` to strictly limit concurrency when processing batch I/O operations.
