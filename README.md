# Async Programming

Async programming covers a lot of ground — from syntax to thread pools to execution contexts to UI deadlocks. This repo follows the full arc: why synchronous blocking fails, how async/await works at the syntax level, what's happening under the hood at the Task level, and finally the runtime mechanisms (ExecutionContext, SynchronizationContext) that make it all reliable in real applications.

---

## [callback-chaining](async/callback-chaining/)

The original async problem: sequencing operations with `.ContinueWith()`. Same cooking simulation, but every step is a callback.

_Learned: callback chaining works but nests badly — error handling, cancellation, and control flow all become deeply nested and fragile, which is exactly the problem async/await was designed to eliminate._

---

## [async-await-basics](async/async-await-basics/)

Converting the same callback chain to sequential `await` calls. One lesson, two approaches, dramatic difference in readability.

_Learned: `async/await` is syntactic sugar over continuations — the compiler rewrites it into the same state machine callbacks, but you write what reads like synchronous code._

---

## [concurrent-tasks](async/concurrent-tasks/)

Running multiple independent tasks in parallel with `Task.WhenAll`. Starts all tasks simultaneously and waits for all to finish.

_Learned: `await task` is sequential; `await Task.WhenAll(t1, t2)` is parallel — the total time difference equals the duration of the longest task versus the sum of all tasks._

---

## [task-internals](async/task-internals/)

Custom Task implementation from scratch: thread pool dispatch, continuation callbacks, awaiter pattern, exception capture, and the `INotifyCompletion` interface.

_Learned: a Task is a state machine plus a callback list — `await` just registers a continuation that runs when the state flips to "completed", and the compiler generates all the boilerplate state machine code that makes this look synchronous._

---

## [async-best-practices](async/async-best-practices/)

HackerNews reader app with two ViewModels: one full of anti-patterns (`.Wait()`, sequential fetching, no cancellation) and one demonstrating best practices side by side.

_Learned: `.Wait()` on a Task blocks a thread and can deadlock in context-aware environments — "async all the way down" is not just a style preference, it's a correctness requirement._

---

## [thread-local-storage](async/thread-local-storage/)

`[ThreadStatic]` attribute: each thread gets its own invisible copy of the static variable. Shows isolation between threads and the initialization trap.

_Learned: `[ThreadStatic]` is a thread-scoped variable, but thread pool threads are reused — use `AsyncLocal<T>` instead for state that needs to flow through async call chains, not just within a single thread._

---

## [security-context](async/security-context/)

How the current user (`ClaimsPrincipal`) flows through async operations. Shows that security context is tied to ExecutionContext, not to the thread itself.

_Learned: security context flows through ExecutionContext automatically across `await` boundaries — this is why `HttpContext.User` survives async calls in ASP.NET without any special handling._

---

## [execution-context](async/execution-context/)

`ExecutionContext.Capture()`, `Run()`, `SuppressFlow()`, and `AsyncLocal<T>`. Shows exactly what data flows "down" the async chain and what doesn't.

_Learned: ExecutionContext is the ambient-data carrier for async — every `await` captures the current context and restores it on the continuation, so `AsyncLocal<T>` values flow down into child tasks but never back up to callers._

---

## [synchronization-context](async/synchronization-context/)

HackerNews app again, now focused on why `ConfigureAwait(false)` matters in library code and what `SynchronizationContext` means for UI frameworks.

_Learned: `SynchronizationContext` is how UI frameworks marshal continuations back to the UI thread — `ConfigureAwait(false)` tells the runtime to skip this marshalling, which prevents deadlocks when blocking code and async code are mixed._

---

## How to Run

Each console project is a standalone executable:

```bash
cd async/<folder-name>
dotnet run
```

The MAUI apps (`async-best-practices`, `synchronization-context`) require the .NET MAUI workload.

Open `async/async.slnx` in Visual Studio or Rider to browse all projects in one solution.

---

## Certificate

[From Zero to Hero: Asynchronous Programming in C#](certificate.pdf) — completed January 25, 2026 (3h 53m, certificate ID: cPwc8eocwu8zB1)
