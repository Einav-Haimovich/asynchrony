# Async Programming in C#

A ground-up study of asynchronous programming in C# — from why callbacks fail, through what the compiler actually generates, to the runtime mechanisms (ExecutionContext, SynchronizationContext) that make async reliable in production applications.

---

### Async / Await

This section introduces asynchronous programming in C# by building up from first principles. It starts with the problem that existed before `async`/`await`: operations running on background threads could complete after the calling thread had already moved on, forcing developers to use `.ContinueWith()` callbacks to sequence work. Those callbacks produced code that executed out of reading order — what the instructor calls "callback hell" — making programs hard to reason about and prone to subtle bugs. The section then shows how `async`/`await` restores top-to-bottom, left-to-right readability while preserving the same non-blocking behavior. It closes by distinguishing sequential awaiting (one task at a time) from parallel execution with `Task.WhenAll`, and by defining what a `Task` and a `Thread` actually are in the .NET runtime.

**What I learned:**
- When you `await` a task, control returns to the calling thread immediately — the background work continues, but the calling thread is free. Without accounting for this, a console app can exit before background work finishes, which is exactly what the callback-chaining example exposes.
- Before `async`/`await`, chaining sequential asynchronous steps required `.ContinueWith()` callbacks. This made execution order diverge from reading order, creating "callback hell" — code that is correct but nearly impossible to follow or maintain.
- `async`/`await` replaces callbacks while preserving the same non-blocking mechanics: the calling thread suspends at each `await` point and resumes when the awaited task completes, but the code reads as if it were synchronous, top to bottom.
- `Task.WhenAll` enables parallel execution: all passed tasks start immediately and run concurrently, and the `await` only unblocks once every task in the set has finished. This is the difference between asynchronous (non-blocking sequential) and parallel (concurrent) programming.
- A `Task` is an abstraction over `Thread` — it represents an asynchronous operation without dictating which thread runs it or how threads are managed. The .NET runtime handles thread-pool scheduling transparently when you work at the `Task` level.
- Running tasks in parallel with `Task.WhenAll` introduces the risk of deadlocks if two tasks are mutually dependent on each other to finish — a hazard that does not exist in purely sequential `await` chains.

---

### Compiler-Generated Code

This section goes behind the `async`/`await` syntax to show what the C# compiler actually produces when it processes an async method. Because the content was demonstrated live using SharpLab.io to inspect decompiled IL output, there is no runnable project — this is a conceptual section focused on reading and interpreting compiler-generated code rather than writing any of your own.

**What I learned:**
- The `async` keyword instructs the compiler to transform your method into a state machine struct (in release builds) that implements `IAsyncStateMachine`, containing all your local variables as fields and all your `await` points as numbered cases in a `MoveNext` switch statement.
- The state integer drives resumption: it starts at -1 (not yet started), advances to 0, 1, 2, etc. as each awaited task suspends execution, and returns to -1 each time the thread re-enters so that a crash mid-execution does not leave the machine in a stuck state.
- The .NET runtime avoids unnecessary thread switches: before suspending, the generated code checks whether the awaited task has already completed, and if so continues on the calling thread rather than paying the CPU cost of a context switch.
- All code inside a `MoveNext` method is wrapped in a compiler-generated try/catch block, which means any exception thrown inside an async method is caught and stored on the task builder — but only re-thrown to the caller if the task is awaited. Fire-and-forget calls without `await` silently swallow exceptions and can leave the application in an undefined state.
- In debug builds the compiler emits the state machine as a class with human-readable field names to support breakpoints and debugger inspection; in release builds it uses a struct with mangled names for smaller size and better performance.
- Each async method adds roughly 80 bytes to the compiled output, which is negligible for most applications but worth knowing for memory-constrained environments such as IoT or embedded targets.

---

### Async from Scratch

This section builds a working replacement for `Task` entirely from first principles, without using any built-in `Task` APIs. By implementing `Run`, `ContinueWith`, `Wait`, `Delay`, and a custom awaiter struct, the section forces a concrete understanding of what the .NET runtime is actually doing when you write `await someTask`. The exercise reveals that async/await is not a runtime feature so much as a compiler and pattern-matching system layered on top of ordinary threads, thread pools, timers, and synchronization primitives.

**What I learned:**
- A task is essentially a state machine wrapper around a thread-pool work item: `Run` queues the action via `ThreadPool.QueueUserWorkItem`, catches any exception, and stores it for later re-throw — the same structure the real `Task` uses internally.
- `ContinueWith` works by checking completion at the moment it is called: if the task is already done it queues the continuation immediately; if not, it stores the action and the captured `ExecutionContext` so the continuation runs in the correct security and culture context when the task eventually finishes.
- `Wait` is implemented on top of `ContinueWith` using a `ManualResetEventSlim`, mirroring how .NET itself implements blocking waits. The blocking call must be placed outside any lock to avoid deadlocking the thread that holds the lock and is also waiting for the same signal.
- Exception propagation requires `ExceptionDispatchInfo.Throw` rather than a bare `throw`, because a bare re-throw discards the original stack trace. Preserving the full call chain across thread boundaries is a deliberate design choice, not automatic.
- The `await` keyword uses duck typing: the compiler does not look for a specific interface or base class on the awaited expression. It only requires a `GetAwaiter()` method that returns a type implementing `INotifyCompletion` with `IsCompleted` and `GetResult()`. Adding that single method to a custom type is enough to make `await` compile against it.
- Console applications lack a synchronization context, so after an `await` resumes there is no mechanism to marshal back to the original thread. The continuation runs on whatever thread-pool thread completed the task, which is why post-await code in a console app does not return to thread 1.

---

### Async / Await Best Practices

This section applies the mechanics of async/await to a real .NET MAUI application — a Hacker News client — by working through a ViewModel that contains deliberate mistakes and refactoring it into a correct, performant version. Rather than introducing new language features, the lessons focus on the judgment calls developers face when wiring async code together: where it is safe to fire off work without awaiting it, when blocking synchronously is genuinely harmful, how thread context affects correctness and performance, and which newer APIs (`IAsyncEnumerable`, `ValueTask`, `ConfigureAwaitOptions`) solve problems that simpler patterns leave unsolved.

**What I learned:**
- `async void` is not merely a style smell — it makes exceptions uncatchable from the call site and silently introduces race conditions when the calling thread continues past an unawaited method. The `SafeFireAndForget` extension method from the `AsyncAwaitBestPractices` library wraps this pattern safely and makes fire-and-forget intent explicit to future readers.
- Constructors cannot use `async/await` because their purpose is synchronous object initialization; any long-running work triggered from a constructor should be launched as fire-and-forget with proper exception handling, not forced into an `async void` wrapper by hand.
- `.Wait()` and `.Result` are blocking calls that hold the calling thread hostage until a background task finishes — on the UI thread this freezes the app, and everywhere it wastes a thread from a finite pool, risking thread pool exhaustion under load. The fix is simply `await`, which is valid inside `try/finally` blocks.
- `ConfigureAwait(false)` prevents continuation on the captured (UI) thread and should be the default in any layer that never touches UI — view models, services, data access. The newer `ConfigureAwaitOptions` enum makes the intent more explicit and adds `ForceYielding` and `SuppressThrowing` as first-class options.
- Running independent API calls sequentially in a `foreach` is a common performance mistake; `Task.WhenEach` combined with `IAsyncEnumerable` and `yield return` lets results stream to the UI as each one arrives instead of forcing the user to wait for the full batch.
- `ValueTask` is appropriate when the hot path of a method does not reach an `await` — for example, when returning cached data — because `ValueTask` is a stack-allocated value type whereas `Task` allocates on the heap. A `ValueTask` must be awaited once and discarded; it cannot be reused or passed around.
- Returning a task directly (omitting `async`/`await` from a pass-through method) eliminates a superfluous context switch, but must not be done when the `return` statement is inside a `try/catch` or a `using` block — doing so exits those blocks before the task completes, swallowing exceptions or disposing resources prematurely.

---

### .NET Internals

This section pulls back the curtain on the runtime machinery that makes async/await work in .NET. Rather than covering everyday patterns, it examines the four layered mechanisms that live beneath every asynchronous operation: `[ThreadStatic]` for isolating data per thread, `IPrincipal` (the security context) for carrying user identity, `ExecutionContext` for propagating ambient state across thread boundaries, and `SynchronizationContext` for controlling which thread a continuation resumes on. Understanding how these components interact explains behaviors that are otherwise mysterious — why user identity survives an `await`, why `ConfigureAwait(false)` affects which thread continues execution, and why certain bugs appear only under thread-pool reuse.

**What I learned:**
- `[ThreadStatic]` makes a static field local to the thread that wrote it, but because the thread pool reuses threads and tasks can resume on a different thread after an `await`, relying on it to carry per-request state is unsafe. The execution-context demo makes this concrete by showing values diverging across manually created threads.
- `ExecutionContext` is the container that async/await automatically captures and restores across thread switches. It carries the current culture, the current `IPrincipal`, and any `AsyncLocal<T>` values — so code running on a background thread after an `await` sees the same ambient state as the originating thread.
- `AsyncLocal<T>` is the correct alternative to `[ThreadStatic]` when values need to flow through async continuations. The execution-context demo stores a string in an `AsyncLocal<string>` and shows that its value is preserved on a `Task.Run` continuation but disappears when `ExecutionContext.SuppressFlow()` is called first.
- `ExecutionContext.SuppressFlow()` is an escape hatch that prevents ambient state from propagating to new work items — the realistic use case being security-sensitive scenarios where leaking caller context to background work is undesirable.
- `SynchronizationContext` is the mechanism by which an awaited task knows which thread to resume on. In UI frameworks (.NET MAUI, WinForms, WPF), the framework installs a `SynchronizationContext` on the UI thread so continuations marshal back automatically. Using `ConfigureAwait(false)` sets the captured context to `null`, so the runtime instead picks any available thread-pool thread.
- ASP.NET Core deliberately has no `SynchronizationContext`, because there is no UI thread to return to. The role that `SynchronizationContext` plays in desktop apps is instead filled by `HttpContext`, which is scoped per request and flows via `ExecutionContext` to remain accessible across thread switches.

---

## How to Run

Each console project is a standalone executable:

```bash
cd async/<section>/<project>
dotnet run
```

The MAUI apps (`best-practices/async-best-practices`, `dotnet-internals/synchronization-context`) require the .NET MAUI workload:

```bash
dotnet workload install maui
```

Open `async/async.slnx` in Visual Studio or Rider to browse all projects in one solution.

---

## Folder Structure

```
async/
├── async-await/
│   ├── callback-chaining/
│   ├── async-await-basics/
│   └── concurrent-tasks/
├── compiler-generated-code/
├── async-from-scratch/
│   └── task-internals/
├── best-practices/
│   └── async-best-practices/
└── dotnet-internals/
    ├── thread-local-storage/
    ├── security-context/
    ├── execution-context/
    └── synchronization-context/
```

---

Thanks to Brandon Minnick for the course — [From Zero to Hero: Asynchronous Programming in C#](https://dometrain.com/course/from-zero-to-hero-asynchronous-programming-in-csharp/).

[Certificate of completion](<certificate/Asynchronous Programming in C# - Einav Haimovich.pdf>)
