// deck-401.js - "Design Patterns with C#" - 401: Concurrency Patterns
// Red Hat-branded 16:9 deck. Build:  node deck-401.js  (from presentation/)

"use strict";

const H = require("./deck-helpers.js");
const {
  COLOR, FONT, W, ASSETS, PNG,
  newDeck, addFooter, addContentTitle, addBullets, addTwoColBullets,
  addStatusTable, addCaption, addSectionDivider, addNotes,
  addCodeSlide, addDiagramSlide,
} = H;

const OUT = "csharp-design-patterns-401-concurrency-patterns.pptx";

const pres = newDeck();
pres.title = "Design Patterns with C# - 401: Concurrency Patterns";
let pageNum = 0;

function S() {
  const s = pres.addSlide(); pageNum += 1; addFooter(s, pageNum); return s;
}
function divider(code, title, subtitle, notes) {
  const s = pres.addSlide(); pageNum += 1;
  addSectionDivider(s, code, title, subtitle);
  addNotes(s, notes);
}

// A pattern slide: title + 2-4 bullets, optional figure on the right.
function patternSlide(eyebrow, title, bullets, fig, caption, notes, opts = {}) {
  const s = S();
  addContentTitle(s, eyebrow, title);
  const hasFig = !!fig;
  addBullets(s, bullets, {
    x: 0.62, y: 1.85, w: hasFig ? 5.55 : 12.09, h: 4.55,
    fontSize: opts.fontSize ?? (hasFig ? 15.5 : 17),
  });
  if (hasFig) {
    H.addFigure(s, `${PNG}/${fig}.png`, 6.45, 1.75, 6.25, 4.35);
  }
  if (caption) addCaption(s, caption);
  addNotes(s, notes);
  return s;
}

// A reference table slide, wide first column so principle names don't wrap.
function refTable(eyebrow, title, rows, notes, opts = {}) {
  const s = S();
  addContentTitle(s, eyebrow, title);
  addStatusTable(s, rows, { colW: opts.colW || [3.0, 1.6, 7.49], rowH: opts.rowH || 0.42 });
  addNotes(s, notes);
}

// ===========================================================================
// Cover
// ===========================================================================
{
  const s = pres.addSlide();
  pageNum += 1;
  s.background = { color: COLOR.white };
  try { s.addImage({ path: `${ASSETS}/cover-panel.png`, x: 0, y: 0, w: W, h: 7.5 }); } catch (e) {}
  s.addText("DESIGN PATTERNS WITH C#", {
    x: 6.00, y: 1.98, w: 6.90, h: 0.34,
    fontFace: FONT.title, fontSize: 14, bold: true, color: COLOR.red, charSpacing: 6,
    align: "left", valign: "middle",
  });
  s.addText([
    { text: "Design Patterns", options: { breakLine: true } },
    { text: "with C#" },
  ], {
    x: 5.95, y: 2.42, w: 6.95, h: 2.00,
    fontFace: FONT.title, fontSize: 48, bold: true, color: COLOR.ink,
    align: "left", valign: "top",
  });
  s.addText("401: Concurrency Patterns", {
    x: 6.00, y: 4.65, w: 6.70, h: 0.90,
    fontFace: FONT.body, fontSize: 18, italic: true, color: COLOR.caption,
    align: "left", valign: "top",
  });
  try { s.addImage({ path: `${ASSETS}/logo-candidate-2.png`, x: 11.10, y: 6.80, w: 1.55, h: 0.37 }); } catch (e) {}
  addNotes(s, "Design Patterns with C#, the 401 deck: concurrency patterns. Where the idiomatic C# deck asked what a rate calculator looks like written the modern way, and the testing and legacy deck asked how a team proves a shipping engine behaves correctly, this deck goes underneath async itself: what an await actually suspends, how a value gets built once and shared safely, how a shared servant gets coordinated as pressure on it grows, and how the classic Gang of Four catalogue holds up once more than one thread can touch an object at a time. Every example lives in the same shipping domain as the rest of the series: consignments, carriers, tariffs, stowage plans, gateway connections, and the handling events that move through them.");
}

// ===========================================================================
// Agenda
// ===========================================================================
{
  const s = S();
  addContentTitle(s, "AGENDA", "What is ahead");
  addTwoColBullets(s, [
    "Suspension without a thread: what an await actually does to a running method, and when the compiler skips building a state machine at all.",
    "Build once, share everywhere: three ways to publish one shared value the first time it is needed, and prove each one survives sixteen readers racing for it at once.",
    "Coordinating one shared servant: the same plain stowage logic wrapped first in a lock, then in an async-aware gate, then rebuilt as an Active Object on a queue.",
  ], [
    "Handing work across a queue: producers and consumers decoupled by one bounded channel, with backpressure and a deterministic drain instead of a wall-clock wait.",
    "Rationing a scarce resource: a fixed-size connection pool and a reusable buffer, both built so a caller waits instead of the system growing without bound.",
    "Classic patterns under concurrency: Observer, Iterator, Decorator, and Command, each made safe by an immutable snapshot or a channel rather than a lock.",
  ], { fontSize: 15.5 });
  addNotes(s, "Six stops, one underlying question: once more than one caller can touch a shared piece of state or a shared servant at the same time, what actually keeps the result correct. The first two stops work underneath a single object, a suspended method and a lazily built value. The middle two stops work across a boundary between callers, a coordinated servant and a queue connecting producers to consumers. The last two stops ration a resource that cannot grow to meet demand and then revisit the familiar Gang of Four catalogue once concurrency is the new force acting on it. Every pattern in this deck is demonstrated against running shipping-domain code with a test that proves the concurrency property, not just the happy path.");
}

// ===========================================================================
// Section 01: Suspension Without a Thread
// ===========================================================================
divider("01", "Suspension Without a Thread", "An await is an object, not a parked thread",
  "This section opens on the mechanics an await actually runs on, the layer underneath the Task-based surface a C# programmer writes against every day. SailingBookingDesk.AcceptAsync compiles into a small heap-allocated state machine that captures exactly the locals a single booking needs, suspends at the ledger's await point, and resumes on completion, with no dedicated thread parked for the duration. A thousand bookings in flight at once cost a thousand small state machines rather than a thousand threads, a claim a test proves directly by checking every booking suspends from the same calling thread. The section closes on ValueTask<bool>, the escape hatch the compiler reaches for when an answer is already known and building a state machine at all would be wasted work.");

{
  patternSlide(
    "SUSPENSION WITHOUT A THREAD",
    "An await is an object, not a thread",
    [
      "A suspended await is a small heap-allocated state machine holding exactly the locals one in-flight call needs, not a parked operating-system thread.",
      "In 2022 the .NET team opened and prototyped its own stackful green-threads model under dotnet/runtimelab, built to compare against the stackless model already shipping since C# 5.",
      "The team wound the prototype down and kept the stackless model: a thousand suspended state machines are cheap, a thousand growable call stacks are not.",
      "Every pattern later in this deck, a coordinated servant, a queue, a pool, assumes this same stackless foundation under every await it uses.",
    ],
    "33-stackless-vs-stackful",
    "A stackful green thread carries a full growable call stack across every suspension; a stackless async method carries only the small state machine its own locals need.",
    "The .NET team's own 2022 green-threads prototype, tracked in the dotnet/runtimelab repository under the working name green threads, tried stackful coroutines: lightweight, cooperatively scheduled threads each carrying a full, growable call stack that could suspend in the middle of a deeply nested call without rewriting it into await form. The team wound that prototype down the following year and published its own writeup of why, concluding the stackless model it already shipped scaled further under load than a stackful one would have. This comparison is .NET examining its own abandoned prototype against the model it kept, not a comparison against any other platform or runtime. The diagram contrasts the two shapes directly: a stackful green thread's full call stack on one side, the stackless model's small per-await state machine on the other, the same contrast the booking desk code makes concrete on the next few slides."
  );
}

{
  const s = S();
  addCodeSlide(s, "SUSPENSION WITHOUT A THREAD", "A method that compiles into a state machine", "C#", [
    "public sealed class SailingBookingDesk(IBookingLedger ledger, int sailingCapacity)",
    "{",
    "    private int _accepted;",
    "",
    "    public async Task<BookingReceipt> AcceptAsync(",
    "        BookingRequest request, CancellationToken ct = default)",
    "    {",
    "        var seq = await ledger.ConfirmAsync(request.ConsignmentId, ct);",
    "        Interlocked.Increment(ref _accepted);",
    "        return new BookingReceipt(request.ConsignmentId, seq);",
    "    }",
    "}",
  ], "request and the local seq become fields on the compiler-generated state machine that outlives the single await inside AcceptAsync.");
  addNotes(s, "AcceptAsync looks like an ordinary method, but the compiler rewrites its body into a state machine type with its own fields for request, seq, and the point the method suspended at; nothing about this rewriting is visible in source, only in the generated code a disassembler would show. The single await on ledger.ConfirmAsync is the one suspension point in this method: when the ledger's confirmation is not yet complete, the state machine's MoveNext method returns control to the caller, and the calling thread is free to do other work. Interlocked.Increment after the await runs on whatever thread the continuation happens to resume on, which is why the counter update uses an atomic increment rather than a plain field write. Every concurrency pattern deeper in this deck sits on top of exactly this mechanism: a method suspending without blocking the thread that called it.");
}

{
  const s = S();
  addCodeSlide(s, "SUSPENSION WITHOUT A THREAD", "A thousand bookings, one calling thread", "C#", [
    "public async Task<IReadOnlyList<BookingReceipt>> AcceptAllAsync(",
    "    IReadOnlyCollection<BookingRequest> requests, CancellationToken ct = default)",
    "{",
    "    var tasks = requests.Select(r => AcceptAsync(r, ct)).ToList();",
    "    return await Task.WhenAll(tasks);",
    "}",
    "",
    "[Fact]",
    "public async Task A_thousand_bookings_suspend_concurrently_without_a_thread_each()",
    "{",
    "    var tasks = requests.Select(r => desk.AcceptAsync(r, ct)).ToList();",
    "    await gated.AllArrived;",
    "",
    "    Assert.Equal(1, gated.DistinctPreAwaitThreadCount);",
    "    Assert.All(tasks, t => Assert.False(t.IsCompleted));",
    "}",
  ], "DistinctPreAwaitThreadCount == 1 proves a thousand suspended bookings started from the same calling thread, not a thousand parked threads.", { fontSize: 10 });
  addNotes(s, "Select followed by ToList enumerates the thousand booking requests synchronously on the calling thread, invoking AcceptAsync, and through it the ledger's ConfirmAsync, once per request before any of them has a chance to resume; only Task.WhenAll actually suspends, waiting for every one of the thousand tasks to complete. The test drives this with a gated ledger that records every thread that called its ConfirmAsync method before releasing any of them, and GatedBookingLedger.DistinctPreAwaitThreadCount equal to one is the structural proof that all thousand bookings suspended from the single thread that started them rather than from a thousand worker threads. Assert.All checking that none of the tasks has completed yet confirms the thousand bookings really are suspended in flight, not finished early. This is the practical payoff of a stackless model: the cost of a thousand in-flight awaits is a thousand small state machines sitting on the heap, not a thousand parked threads consuming stack space and scheduler attention.");
}

{
  const s = S();
  addCodeSlide(s, "SUSPENSION WITHOUT A THREAD", "Skip the machine when the answer is known", "C#", [
    "public ValueTask<bool> HasCapacityAsync(CancellationToken ct = default) =>",
    "    new(Volatile.Read(ref _accepted) < sailingCapacity);",
  ], "No await, no state machine: ValueTask<bool> wraps an already-known result the moment HasCapacityAsync is called.");
  addNotes(s, "HasCapacityAsync returns a ValueTask<bool> constructed directly from a value already known at the point of the call, Volatile.Read against the accepted count compared to the sailing's capacity, with no await anywhere in the method body and therefore no compiler-generated state machine behind it at all. A caller awaiting this method still sees ordinary await syntax, but the awaiter's IsCompletedSuccessfully property is true the instant the ValueTask is constructed, so the async machinery completes synchronously without allocating anything beyond the struct itself. This is the targeted alternative to Task<bool> for a method that is only sometimes asynchronous in practice: a hot, frequently polled check like this one avoids a heap allocation on every call where the answer is already sitting in a field. Reaching for ValueTask<T> here is not a general replacement for Task<T>; it pays off specifically when a method completes synchronously often enough in practice for the allocation it skips to matter.");
}

// ===========================================================================
// Section 02: Build It Once, Share It Everywhere
// ===========================================================================
divider("02", "Build It Once, Share It Everywhere", "Thread-safe creation without a hand-rolled lock",
  "Three different mechanisms publish a single shared TariffTable the first time any caller needs it, and this section proves all three really do build exactly once even when sixteen readers race for the value at the same moment. Lazy<T> in ExecutionAndPublication mode lets the framework itself guarantee the underlying builder runs at most once, and caches a build failure so every later caller rethrows the same exception rather than retrying silently. A hand-written alternative over the modern System.Threading.Lock type reaches the identical guarantee with an explicit null-coalescing assignment inside a synchronous gate, and a static field initializer reaches the simplest version of the same guarantee with no lock at all, relying on the runtime's own promise that a type's static initializers run at most once before any static member is first touched.");

{
  const s = S();
  addDiagramSlide(s, "BUILD ONCE, SHARE EVERYWHERE", "Sixteen readers, one build", "34-thread-safe-creation",
    "A Barrier releases sixteen concurrent readers at once against three strategies; each one still builds its shared TariffTable exactly once.");
  addNotes(s, "The test harness behind this diagram uses a Barrier sized to sixteen concurrent readers, each one started on its own Task.Run and held at the barrier until every one of the sixteen has arrived, then released together so they race for the shared value as close to simultaneously as the scheduler allows. A CountingTariffBuilder wraps the real StandardTariffBuilder and records how many times its Build method actually ran, so the assertion that matters, exactly one build across sixteen simultaneous first-use readers, is a hard count rather than an inference from timing. The same harness runs against all three strategies shown in this section: a Lazy<T>-backed provider, a hand-rolled lock-based provider, and a static field initializer, and every one of them passes with a build count of exactly one and sixteen readers holding the identical reference. The assertions hold regardless of whether real contention happens to manifest on a given run, which is what keeps this proof deterministic rather than timing-dependent.");
}

{
  const s = S();
  addCodeSlide(s, "BUILD ONCE, SHARE EVERYWHERE", "Lazy<T> with ExecutionAndPublication", "C#", [
    "public sealed class LazyTariffProvider(ITariffBuilder builder)",
    "{",
    "    private readonly Lazy<TariffTable> _lazy =",
    "        new(builder.Build, LazyThreadSafetyMode.ExecutionAndPublication);",
    "",
    "    public TariffTable Value => _lazy.Value;",
    "",
    "    public bool IsValueCreated => _lazy.IsValueCreated;",
    "}",
  ], "ExecutionAndPublication is Lazy<T>'s default mode: the framework guarantees builder.Build runs at most once, even under a race.");
  addNotes(s, "LazyTariffProvider wraps an ITariffBuilder inside a Lazy<TariffTable> constructed in LazyThreadSafetyMode.ExecutionAndPublication, which is Lazy<T>'s default mode and the one that matters here: the framework itself guarantees builder.Build executes at most once even when several callers touch Value for the first time concurrently, with every caller but one blocking briefly until the single build completes and then all of them observing the identical published instance. IsValueCreated exposes whether the build has happened yet without forcing it, useful for diagnostics or a test that wants to assert nothing built before the value was actually touched. The same type, under this mode, also caches a thrown exception from the builder and rethrows the identical exception instance to every later caller rather than attempting the build again, a behavior the exception-caching diagram covers directly. This is the least code of the three strategies in this section for the guarantee it buys: no lock statement, no null check, no hand-written double-checked pattern.");
}

{
  const s = S();
  addCodeSlide(s, "BUILD ONCE, SHARE EVERYWHERE", "By hand, or from the runtime", "C#", [
    "public sealed class LockTariffProvider(ITariffBuilder builder)",
    "{",
    "    private readonly Lock _gate = new();",
    "    private TariffTable? _table;",
    "",
    "    public TariffTable Value",
    "    {",
    "        get",
    "        {",
    "            lock (_gate)",
    "            {",
    "                return _table ??= builder.Build();",
    "            }",
    "        }",
    "    }",
    "}",
    "",
    "// A static field initializer: the runtime guarantees it runs at",
    "// most once, before the first access to any static member.",
    "public static class TariffRegistry",
    "{",
    "    private static readonly TariffTable _instance = new StandardTariffBuilder().Build();",
    "",
    "    public static TariffTable Instance => _instance;",
    "}",
  ], "Both reach the identical guarantee as Lazy<T>: build exactly once, publish the same instance to every caller.", { fontSize: 9 });
  addNotes(s, "LockTariffProvider reaches the same guarantee as the Lazy<T>-backed provider by hand: the modern System.Threading.Lock type guards a null-coalescing assignment, so the gate is held only long enough to check whether the table already exists and, on first use, to build and cache it, with no await anywhere near the lock since this is a purely synchronous creation path. TariffRegistry reaches the simplest version of the same guarantee with no lock statement at all, relying entirely on the runtime's own promise that a type's static field initializers run at most once, before the first access to any of that type's static members, so the table is built eagerly the moment the type is first touched. This pattern, checking a cached field inside a lock before building, has a well-known name in the older design-patterns literature, double-checked locking, worth recognizing by that name as history rather than as something this book is introducing as a new idiom; the modern Lock type and the compiler's own memory-model guarantees are what make the single-check form shown here both correct and current. A team choosing among the three strategies in this section is choosing how much of the guarantee to write by hand versus lean on the runtime for, not choosing among different correctness guarantees.");
}

{
  const s = S();
  addDiagramSlide(s, "BUILD ONCE, SHARE EVERYWHERE", "Lazy caches the failure too", "34-exception-caching",
    "Lazy_provider_caches_a_build_failure_and_rethrows_it: Assert.Same proves the exact same exception instance reaches every caller.");
  addNotes(s, "When the builder behind a LazyTariffProvider throws, ExecutionAndPublication mode does not simply let the next caller retry the build; it caches the thrown exception and rethrows that identical exception instance on every subsequent access to Value, which the test proves with Assert.Same comparing the exception object from a first failing call against the exception object from a second failing call rather than merely comparing their messages. This matters operationally: a transient failure during the one build attempt becomes a permanent, consistent failure for the lifetime of that provider instance rather than a flaky one that sometimes succeeds on retry and sometimes does not, which keeps a system's behavior predictable after a bad build rather than introducing a second source of nondeterminism on top of whatever caused the original failure. A caller that wants retry behavior after a failed build needs a new LazyTariffProvider instance entirely, since the failure is permanently cached on the one that failed. This same-instance guarantee on both the success and failure paths is what lets a reader reason about LazyTariffProvider as publishing exactly one outcome, built or failed, for its entire lifetime.");
}

// ===========================================================================
// Section 03: Coordinating One Shared Servant
// ===========================================================================
divider("03", "Coordinating One Shared Servant", "From a lock to an Active Object, one stage at a time",
  "StowagePlan is a plain, not thread-safe servant by design: TryReserve is a non-atomic read-modify-write over its internal slot dictionary, so handing it directly to concurrent callers can double-allocate a slot. This section wraps that same servant in four successive coordination strategies rather than ever touching the servant's own logic: a synchronous Monitor Object gate over a plain lock, an async-aware gate built on SemaphoreSlim once a real await needs to happen inside the critical section, and finally a full Active Object that removes the gate entirely in favor of a bounded channel and a single dedicated consumer. The section closes by mapping POSA2's six Active Object participants onto the C# types and language features that implement them, two of which never appear as a named type a programmer writes by hand.");

{
  const s = S();
  addDiagramSlide(s, "COORDINATING ONE SHARED SERVANT", "One servant, four strategies", "35-monitor-object-and-active-object",
    "StowagePlan is the bare, not-thread-safe servant; SynchronizedStowagePlan, SemaphoreStowagePlan, and StowageActiveObject each wrap it with a different coordination strategy.");
  addNotes(s, "StowagePlan itself never changes across this section: it stays the same plain, single-threaded class whose TryReserve method is not safe to call from more than one caller at a time. Three separate wrapper types coordinate access to that one servant in turn, each replacing the one before it rather than composing with it: SynchronizedStowagePlan serializes every call behind a plain lock, SemaphoreStowagePlan swaps that lock for an awaitable SemaphoreSlim once a real asynchronous ledger write needs to happen inside the critical section, and StowageActiveObject removes the gate concept entirely, routing every reservation request through a bounded channel to a single dedicated consumer that is the only caller ever allowed to touch the servant directly. Keeping the servant itself unchanged across all three stages is deliberate: the diagram and the chapters built on it are about where coordination lives, not about rewriting the underlying stowage logic each time the strategy around it changes.");
}

{
  const s = S();
  addCodeSlide(s, "COORDINATING ONE SHARED SERVANT", "A synchronous gate, and the wall past it", "C#", [
    "public sealed class SynchronizedStowagePlan",
    "{",
    "    private readonly StowagePlan _plan;",
    "    private readonly Lock _gate = new();",
    "",
    "    public SynchronizedStowagePlan(StowagePlan plan) => _plan = plan;",
    "",
    "    public SlotAssignment? Reserve(ConsignmentId id, SlotRequirements req)",
    "    {",
    "        lock (_gate)",
    "        {",
    "            return _plan.TryReserve(id, req);",
    "        }",
    "    }",
    "}",
    "",
    "// Illustrative only, does not compile (CS1996): a lock body",
    "// cannot contain an await.",
    "lock (_gate)",
    "{",
    "    await _ledger.AppendAsync(assignment, ct);",
    "}",
  ], "CS1996 is the compiler's own wall: once a reservation needs an awaited ledger write, a plain lock statement cannot hold it.", { fontSize: 9 });
  addNotes(s, "SynchronizedStowagePlan is the Monitor Object stage: a single Lock-backed gate serializes every call into the servant, and because the body of Reserve is entirely synchronous, the gate is held for the shortest possible time with no await anywhere near it. The second snippet is illustrative only and does not compile; it shows the wall this stage runs into the moment a reservation needs to await an asynchronous ledger write before it can be considered confirmed, since the C# compiler rejects an await inside a lock statement's body with error CS1996. That restriction exists because a lock statement compiles to a monitor acquire and release around the block, and an await can suspend the method entirely, potentially resuming on a different thread than the one that acquired the monitor, which the runtime cannot support safely. This is not a workaround to find; it is the specific, concrete reason the next stage swaps the lock statement for an awaitable gate instead of trying to make the synchronous one work.");
}

{
  const s = S();
  addCodeSlide(s, "COORDINATING ONE SHARED SERVANT", "An async-aware gate, and what it still won't tell you", "C#", [
    "public sealed class SemaphoreStowagePlan : IDisposable",
    "{",
    "    private readonly StowagePlan _servant;",
    "    private readonly IStowageLedger _ledger;",
    "    private readonly SemaphoreSlim _gate = new(1, 1);",
    "",
    "    public async Task<SlotAssignment?> ReserveAsync(",
    "        ConsignmentId id, SlotRequirements req, CancellationToken ct = default)",
    "    {",
    "        await _gate.WaitAsync(ct);",
    "        try",
    "        {",
    "            var assignment = _servant.TryReserve(id, req);",
    "            if (assignment is not null)",
    "                await _ledger.AppendAsync(assignment, ct);",
    "            return assignment;",
    "        }",
    "        finally",
    "        {",
    "            _gate.Release();",
    "        }",
    "    }",
    "",
    "    public void Dispose() => _gate.Dispose();",
    "}",
  ], "A SemaphoreSlim sized to one replaces the lock statement; WaitAsync and Release bracket the servant call and the awaited ledger write together.", { fontSize: 9 });
  addNotes(s, "SemaphoreStowagePlan is the async-aware gate stage: a SemaphoreSlim constructed with both its initial and maximum count set to one behaves as a mutual-exclusion gate, exactly like the previous stage's lock, except that WaitAsync is itself awaitable, so a caller suspends rather than blocking a thread while waiting for the gate, and Release in a finally block guarantees the gate opens again even if the servant call or the ledger write throws. A reservation here is not considered confirmed until its IStowageLedger.AppendAsync write actually lands, and that write is now an actual await sitting inside the gated region, which is exactly the shape the previous stage's plain lock could not hold. What this gate still does not give a caller is any visibility into queue depth or fairness: SemaphoreSlim does not guarantee first-in-first-out ordering among waiters, so a caller cannot predict which of several waiting reservations will be let through next, and there is no built-in way to inspect how many callers are currently queued behind the gate. That gap, no FIFO ordering and no visible queue depth, is exactly what the next stage's Active Object replaces with an explicit, inspectable channel.");
}

{
  const s = S();
  addCodeSlide(s, "COORDINATING ONE SHARED SERVANT", "An Active Object on a bounded channel", "C#", [
    "private readonly Channel<ReserveRequest> _channel = Channel.CreateBounded<ReserveRequest>(",
    "    new BoundedChannelOptions(capacity) { FullMode = BoundedChannelFullMode.Wait });",
    "",
    "public async Task<SlotAssignment?> ReserveAsync(",
    "    ConsignmentId id, SlotRequirements req, CancellationToken ct = default) =>",
    "    await await EnqueueAsync(id, req, ct);",
    "",
    "internal async Task<Task<SlotAssignment?>> EnqueueAsync(",
    "    ConsignmentId id, SlotRequirements req, CancellationToken ct = default)",
    "{",
    "    var tcs = new TaskCompletionSource<SlotAssignment?>(",
    "        TaskCreationOptions.RunContinuationsAsynchronously);",
    "    await _channel.Writer.WriteAsync(new ReserveRequest(id, req, tcs, ct), ct);",
    "    return tcs.Task;",
    "}",
    "",
    "private async Task RunAsync()",
    "{",
    "    await foreach (var req in _channel.Reader.ReadAllAsync())",
    "    {",
    "        var assignment = _servant.TryReserve(req.Consignment, req.Requirements);",
    "        if (assignment is not null) await _ledger.AppendAsync(assignment, req.Ct);",
    "        req.Completion.TrySetResult(assignment);",
    "    }",
    "}",
  ], "This channel is sized as the inbox a single consumer drains, CreateBounded with FullMode.Wait; it is not the pipeline queue or the pool's wait-queue shown later.", { fontSize: 9 });
  addNotes(s, "StowageActiveObject's channel is created with Channel.CreateBounded and FullMode.Wait, so writing a reservation request to a full inbox suspends the caller rather than throwing or dropping the request; that suspension is the backpressure this stage relies on. ReserveAsync is the double-await proxy: the outer await waits for EnqueueAsync to finish writing the request into the channel, and the inner await then waits for the TaskCompletionSource's own task, the one RunAsync eventually completes, to produce the actual reservation result. RunAsync is the single dedicated consumer draining the channel with await foreach, which is the whole reason the servant inside it can stay exactly as plain and single-threaded as it always was: only this one loop ever calls TryReserve, so there is nothing left to coordinate at the servant level. This bounded channel plays a distinct role from the other three Channel<T> uses elsewhere in this deck: here it is one Active Object's private inbox, sized to the backlog a single consumer is expected to absorb, not a multi-consumer work queue and not a pool's collection of idle resources.");
}

{
  const s = S();
  addDiagramSlide(s, "COORDINATING ONE SHARED SERVANT", "POSA2's six participants, two dissolved into the language", "35-active-object-participants",
    "Proxy is ReserveAsync, Servant is StowagePlan; Scheduler surfaces as an await foreach loop and Future as Task and TaskCompletionSource, not as named types.");
  addNotes(s, "POSA2 names six participants in the Active Object pattern, and this diagram maps every one of them onto a concrete piece of StowageActiveObject: Proxy is the public ReserveAsync method, Method Request is the private ReserveRequest record carrying a consignment, its requirements, and a completion source, Activation List is the bounded channel itself, and Servant is the unchanged StowagePlan being protected. The remaining two participants, Scheduler and Future, are the interesting ones, because neither appears in this code as a type a programmer wrote by hand: Scheduler is the await foreach loop inside RunAsync, built entirely from language and library features rather than a hand-rolled dispatch loop, and Future is the Task<SlotAssignment?> handed back to a caller, produced and completed through a TaskCompletionSource rather than a custom future type. This is the pattern's general lesson for a modern language runtime: some participants in a 1990s-era pattern catalogue survive as named types a programmer still writes, while others survive as a concept fully absorbed into the language's own control-flow and task machinery, invisible as a separate type but still doing exactly the job the pattern describes.");
}

// ===========================================================================
// Section 04: Handing Work Across a Queue
// ===========================================================================
divider("04", "Handing Work Across a Queue", "Producers and consumers, decoupled by one bounded channel",
  "HandlingEventPipeline is a Producer-Consumer pipeline for handling-event ingest, built over a bounded Channel<T> with a fixed pool of consumer tasks draining it; a full channel makes SubmitAsync wait rather than growing an in-memory buffer without limit, which is the backpressure this stage exists to demonstrate directly against a producer. CompleteAndDrainAsync completes the channel's writer and awaits every consumer task before reporting a result, which is the deterministic alternative to a wall-clock wait: the pipeline is known to be finished exactly when every consumer task completes, not after some elapsed delay that might or might not have been long enough. The section closes on the properties this shape buys together: exactly-once processing, visible backpressure on a slow consumer, and a shutdown sequence a caller can actually wait on rather than guess at.");

{
  const s = S();
  addDiagramSlide(s, "HANDING WORK ACROSS A QUEUE", "Producers and consumers, decoupled by one queue", "36-producer-consumer-and-pipelines",
    "A fixed pool of consumer tasks drains one bounded channel; a full channel suspends a producer's SubmitAsync instead of growing an unbounded buffer.");
  addNotes(s, "HandlingEventPipeline starts a fixed number of consumer tasks against one shared channel as soon as it is constructed, and every one of those consumer tasks runs the identical ConsumeAsync loop, so the number of consumers draining the queue is a constructor parameter rather than something that grows or shrinks at runtime. The diagram's key property is the bound: this channel is created with a fixed capacity, and once it fills, a producer calling SubmitAsync receives a pending ValueTask rather than an exception or a silently dropped event, suspended until a consumer reads something out and makes room. An unbounded alternative would absorb a slow consumer by growing indefinitely, which looks like success until memory pressure turns an invisible slow-consumer problem into an outage with no earlier warning; the bounded shape in this diagram trades that invisible failure mode for a producer that is visibly waiting, a signal a team can notice and act on while it is still a performance question rather than an incident.");
}

{
  const s = S();
  addCodeSlide(s, "HANDING WORK ACROSS A QUEUE", "One bounded channel, many producers and consumers", "C#", [
    "private readonly Channel<HandlingEvent> _channel = Channel.CreateBounded<HandlingEvent>(",
    "    new BoundedChannelOptions(capacity) { FullMode = BoundedChannelFullMode.Wait });",
    "",
    "public ValueTask SubmitAsync(HandlingEvent e, CancellationToken ct)",
    "{",
    "    ArgumentNullException.ThrowIfNull(e);",
    "    return _channel.Writer.WriteAsync(e, ct);",
    "}",
    "",
    "private async Task ConsumeAsync()",
    "{",
    "    await foreach (var e in _channel.Reader.ReadAllAsync())",
    "    {",
    "        await _onEvent(e, CancellationToken.None);",
    "        _observed.Enqueue(e);",
    "        Interlocked.Increment(ref _processed);",
    "    }",
    "}",
  ], "This channel is CreateBounded with FullMode.Wait, the ingest queue between producers and consumers, distinct from the Active Object inbox and the pool's wait-queue.", { fontSize: 9 });
  addNotes(s, "This channel is created with Channel.CreateBounded and BoundedChannelFullMode.Wait, the same full-mode setting as the Active Object stage's inbox but serving a different role here: a shared work queue that any number of producer callers write into and a fixed pool of consumer tasks reads from concurrently, rather than one object's private inbox drained by a single dedicated consumer. SubmitAsync forwards directly to the channel writer's WriteAsync, so a caller that submits into a full channel gets a pending ValueTask that only completes once a consumer has made room; honoring the cancellation token means a token cancelled during that wait makes the submission throw rather than silently hang. Every ConsumeAsync instance runs the identical await foreach loop against the shared channel reader, so the runtime's own channel implementation is what safely distributes items across however many consumer tasks are running, not any hand-written coordination in this class. _observed and _processed being updated with a ConcurrentQueue and Interlocked.Increment respectively is what lets several consumer tasks record their own results concurrently without corrupting shared state, the same discipline a shared counter needs whenever more than one task can touch it at once.");
}

{
  patternSlide(
    "HANDING WORK ACROSS A QUEUE",
    "Exactly once, backpressure, and a deterministic drain",
    [
      "FullMode.Wait on a full channel is the backpressure itself: a producer's SubmitAsync stays pending until a consumer reads something out, rather than growing a buffer or dropping an event.",
      "Exactly-once processing comes from the channel transferring ownership of each item to exactly one consumer, recorded through a ConcurrentQueue and an Interlocked counter rather than a lock.",
      "Writer.Complete() followed by Task.WhenAll over every consumer task is what makes shutdown deterministic: the pipeline is done exactly when every consumer finishes, never after a guessed wall-clock delay.",
      "Chaining a second bounded stage after the first propagates the same backpressure upstream: a slow downstream consumer fills its own channel, which in turn makes the upstream producer wait too.",
    ],
    "36-exactly-once-drain",
    "CompleteAndDrainAsync completes the writer, awaits every consumer task, then reports a result; the pipeline is known finished by task completion, not elapsed time.",
    "These four properties come from the same small set of channel features working together, not from four separate mechanisms bolted on. FullMode.Wait is what makes backpressure visible rather than silent, turning a slow consumer into a producer that is observably waiting instead of a buffer that silently grows. Exactly-once processing falls out of the channel's own ownership transfer: once an item is read by one consumer, no other consumer can read that same item, so the ConcurrentQueue and Interlocked counter on the consuming side only need to be safe against concurrent consumers writing to them, not against double-processing the same event. The deterministic drain in CompleteAndDrainAsync, completing the writer and then awaiting every consumer task, is the direct alternative to a wall-clock wait, and it generalizes cleanly: a second pipeline stage chained after the first experiences the same backpressure the first producer does, because its own bounded channel fills exactly the same way under a slow final consumer, and that pressure propagates back through every stage connected to it.",
    { fontSize: 13 }
  );
}

// ===========================================================================
// Section 05: Rationing a Scarce Resource
// ===========================================================================
divider("05", "Rationing a Scarce Resource", "Pools: hand the resource back instead of rebuilding it",
  "GatewayConnectionPool is a fixed-size connection pool over IGatewayConnection, built entirely on top of an unbounded Channel<T> that does double duty as both the storage for idle connections and the async-aware wait queue for a caller that arrives when the pool is exhausted. Reading from an empty channel suspends a renter until Return writes a connection back, with no polling and no wall-clock timeout, while GatewayLease wraps a rented connection in a disposable handle so a using block returns it automatically even if the caller throws. The section closes on ManifestWriter, a second kind of rationing over a byte buffer rather than a connection: ArrayPool<byte> lets repeated calls reuse the same scratch buffer instead of allocating a fresh one every time, clearing it on return so EDI manifest content never lingers for the next caller to see.");

{
  const s = S();
  addCodeSlide(s, "RATIONING A SCARCE RESOURCE", "A fixed-size pool where the channel is the wait queue", "C#", [
    "public sealed class GatewayConnectionPool : IAsyncDisposable",
    "{",
    "    private readonly Channel<IGatewayConnection> _available =",
    "        Channel.CreateUnbounded<IGatewayConnection>();",
    "",
    "    public GatewayConnectionPool(int size, Func<int, IGatewayConnection> connectionFactory)",
    "    {",
    "        for (var i = 0; i < size; i++)",
    "            _available.Writer.TryWrite(connectionFactory(i));",
    "    }",
    "",
    "    public ValueTask<IGatewayConnection> RentAsync(CancellationToken ct = default) =>",
    "        _available.Reader.ReadAsync(ct);",
    "",
    "    public void Return(IGatewayConnection connection) =>",
    "        _available.Writer.TryWrite(connection);",
    "",
    "    public async ValueTask<GatewayLease> RentLeaseAsync(CancellationToken ct = default) =>",
    "        new(this, await RentAsync(ct));",
    "}",
  ], "GatewayLease is a using-disposable RAII handle: disposing it calls Return automatically, so a caller that throws never leaks the connection.", { fontSize: 9 });
  addNotes(s, "GatewayConnectionPool's channel is created with Channel.CreateUnbounded, which sounds like an odd choice for a fixed-size pool until the role becomes clear: the pool's own size is fixed by how many connections the constructor writes into the channel up front, and the channel itself just needs to hold at most that many idle connections at a time, so an unbounded channel used this way behaves exactly like a bounded one in practice without needing a configured capacity. RentAsync is nothing more than the channel reader's own ReadAsync, so borrowing a connection when the pool is exhausted suspends the caller the same way reading from any empty channel does, with no polling loop and no manual wait handle; Return is the channel writer's TryWrite, so returning a connection is what wakes the oldest waiting renter. GatewayLease is a readonly record struct implementing IDisposable, an RAII-style handle whose Dispose method calls Pool.Return on the connection it holds, so a using block around RentLeaseAsync guarantees the connection returns to the pool even if the code that borrowed it throws partway through. This channel's unbounded role, idle-connection storage plus an async wait queue for an exhausted pool, is distinct from the Active Object's private inbox and the ingest pipeline's shared work queue covered earlier in this deck.");
}

{
  const s = S();
  addDiagramSlide(s, "RATIONING A SCARCE RESOURCE", "Exhaustion is backpressure", "37-connection-pool-exhaustion",
    "A renter arriving when every connection is checked out suspends on RentAsync until Return wakes it, the same backpressure shape as a full queue.");
  addNotes(s, "Pool exhaustion and channel backpressure are the same underlying mechanism viewed from opposite ends: a full bounded channel suspends a writer until a reader makes room, while an empty pool's channel suspends a reader, the renter calling RentAsync, until a writer, some other caller calling Return, makes a connection available again. A caller that cannot get a connection is not failing or retrying in a loop; it is suspended exactly the way a suspended await always is in this deck, a small state machine waiting on a channel operation rather than a thread blocked on a wait handle or spinning on a poll. This diagram's point is that exhaustion is not a special failure path a pool needs separate code to detect and react to; it falls directly out of using a channel as the pool's storage, since an empty channel's ReadAsync already suspends without any extra logic. A system under load that exhausts this pool experiences the same visible, observable waiting this deck has already shown for a full ingest channel and a full Active Object inbox, rather than a silent failure or an unbounded retry storm.");
}

{
  const s = S();
  addCodeSlide(s, "RATIONING A SCARCE RESOURCE", "Reuse a buffer, cleared on return", "C#", [
    "public sealed class ManifestWriter",
    "{",
    "    private readonly ArrayPool<byte> _pool;",
    "",
    "    public byte[] Serialize(IReadOnlyList<ManifestLine> lines)",
    "    {",
    "        var upperBound = UpperBoundBytes(lines);",
    "        var scratch = _pool.Rent(upperBound);",
    "        try",
    "        {",
    "            var written = 0;",
    "            foreach (var line in lines)",
    "            {",
    "                written += Encoding.UTF8.GetBytes(line.Code, scratch.AsSpan(written));",
    "                scratch[written++] = (byte)'=';",
    "                written += Encoding.UTF8.GetBytes(line.Value, scratch.AsSpan(written));",
    "                scratch[written++] = (byte)'\\n';",
    "            }",
    "",
    "            return scratch.AsSpan(0, written).ToArray();",
    "        }",
    "        finally",
    "        {",
    "            _pool.Return(scratch, clearArray: true);",
    "        }",
    "    }",
    "}",
  ], "UpperBoundBytes computes a worst-case UTF-8 length first, so the rented scratch buffer is sized once and reused across repeated calls.", { fontSize: 9 });
  addNotes(s, "ManifestWriter rations a byte buffer rather than a connection: UpperBoundBytes computes a deterministic worst-case UTF-8 byte length for the manifest lines being encoded before anything is rented, so the same input always requests the same size from the pool and repeated calls can observe the pool handing back the same underlying array rather than allocating a fresh one each time. The encoding itself writes directly into the rented scratch buffer's span, and only the final, exact-length trimmed copy is allocated and returned to the caller, which is the real payoff of pooling here: the large worst-case scratch buffer is reused across calls, while only the small, exactly-sized result ever becomes a fresh heap allocation. The finally block returns the scratch buffer with clearArray set to true, which zeroes its contents before the pool makes it available to the next renter; that matters specifically for EDI manifest data, consignment codes and declared values that another caller's Rent call could otherwise read back from a reused buffer if the previous contents were left in place. This is the same rationing idea as the connection pool, handing a scarce, reusable resource back instead of discarding and rebuilding it, applied to memory instead of a network connection.");
}

// ===========================================================================
// Section 06: Classic Patterns Under Concurrency
// ===========================================================================
divider("06", "Classic Patterns Under Concurrency", "The GoF catalogue, made safe by immutability and channels",
  "This closing section revisits four familiar names, Observer, Iterator, Decorator, and Command, against the same HandlingEvent built up across this deck, asking what changes once more than one thread can call into each pattern at the same time. HandlingEventHub keeps its observer list in an ImmutableArray swapped with ImmutableInterlocked.Update, so a subscribe or unsubscribe never mutates a list a concurrent Raise is iterating. HandlingEventFeed pairs an unbounded channel with IAsyncEnumerable so a reader can traverse a stream that is still being written, one producer and one consumer sharing one feed with every event delivered exactly once. CountingValidatorDecorator forwards an awaited call while updating an Interlocked counter, and CommandInvoker runs a batch of commands concurrently with Task.WhenAll rather than one at a time, closing on the deck's own thesis: immutable data has nothing left to coordinate on at all.");

{
  const s = S();
  addDiagramSlide(s, "CLASSIC PATTERNS UNDER CONCURRENCY", "Four classic patterns, one immutable event", "38-classic-gof-under-concurrency",
    "Observer, Iterator, Decorator, and Command each coordinate around the same immutable HandlingEvent, using a snapshot array or a channel rather than a lock.");
  addNotes(s, "All four patterns in this diagram share one dependency: HandlingEvent is a sealed record and TrackingId is a readonly record struct, so once a handling event is constructed, nothing about it can change underneath any thread reading it, and a caller that wants a modified copy produces one with a non-destructive with expression rather than mutating the shared instance. HandlingEventHub is Observer, publishing to subscribers held in an immutable snapshot array; HandlingEventFeed is Iterator, streaming a still-open sequence of these same immutable events through an async channel; CountingValidatorDecorator is Decorator, wrapping an async validator call with an atomic counter; and CommandInvoker is Command, running a batch of commands concurrently and gathering their results. None of the four needed a new concurrency primitive invented for this deck; each one reaches for either an immutable snapshot, an atomic counter, or a channel, the same small toolkit this whole deck has been building up one stage at a time.");
}

{
  const s = S();
  addCodeSlide(s, "CLASSIC PATTERNS UNDER CONCURRENCY", "Observer on an immutable snapshot", "C#", [
    "public sealed class HandlingEventHub",
    "{",
    "    private ImmutableArray<IHandlingObserver> _observers = [];",
    "",
    "    public IDisposable Subscribe(IHandlingObserver observer)",
    "    {",
    "        ImmutableInterlocked.Update(ref _observers, static (list, o) => list.Add(o), observer);",
    "        return new Subscription(this, observer);",
    "    }",
    "",
    "    public void Raise(HandlingEvent e)",
    "    {",
    "        var snapshot = _observers;",
    "        foreach (var o in snapshot)",
    "        {",
    "            o.OnHandling(e);",
    "        }",
    "    }",
    "",
    "    private sealed class Subscription(HandlingEventHub hub, IHandlingObserver observer) : IDisposable",
    "    {",
    "        public void Dispose() =>",
    "            ImmutableInterlocked.Update(ref hub._observers, static (list, o) => list.Remove(o), observer);",
    "    }",
    "}",
  ], "IHandlingObserver is an explicit interface, not the event keyword: a multicast delegate's mutable invocation list is not safe under a concurrent subscribe.", { fontSize: 9 });
  addNotes(s, "HandlingEventHub holds its subscribers in an ImmutableArray<IHandlingObserver>, and both Subscribe and the Subscription's Dispose method update it through ImmutableInterlocked.Update, which atomically swaps the field to a new array produced by Add or Remove rather than mutating the array in place; Raise captures one reference to the current array into a local snapshot before looping, and that snapshot cannot change underneath the loop no matter what another thread does to the field afterward, because Add and Remove on an immutable array always return a new array. IHandlingObserver is an explicit interface with an OnHandling method, chosen over the C# event keyword as a design choice that sidesteps the analyzer rule CA1003 about exposing an interface instead of a generic delegate type and, more importantly here, sidesteps a real concurrency hazard: a multicast delegate field's invocation list is itself mutable, and reading it to invoke subscribers while another thread subscribes or unsubscribes is not guaranteed safe the way this immutable-array swap is. The result is a subscribe or unsubscribe that can happen at any moment relative to a concurrent Raise without ever corrupting the list the raise is iterating or missing a currently subscribed observer.");
}

{
  const s = S();
  addCodeSlide(s, "CLASSIC PATTERNS UNDER CONCURRENCY", "Iterator as a live async stream", "C#", [
    "public sealed class HandlingEventFeed",
    "{",
    "    private readonly Channel<HandlingEvent> _channel = Channel.CreateUnbounded<HandlingEvent>();",
    "",
    "    public ValueTask PublishAsync(HandlingEvent e, CancellationToken ct) =>",
    "        _channel.Writer.WriteAsync(e, ct);",
    "",
    "    public void Complete() => _channel.Writer.Complete();",
    "",
    "    public async IAsyncEnumerable<HandlingEvent> StreamAsync(",
    "        [EnumeratorCancellation] CancellationToken ct = default)",
    "    {",
    "        await foreach (var e in _channel.Reader.ReadAllAsync(ct))",
    "        {",
    "            yield return e;",
    "        }",
    "    }",
    "}",
  ], "One producer publishing, one consumer streaming, the same unbounded channel; every event is read by exactly one caller, exactly once.", { fontSize: 10 });
  addNotes(s, "HandlingEventFeed's concurrency property is the point of this slide: a producer task can call PublishAsync while a separate consumer task is already running await foreach over StreamAsync, both operating on the same unbounded channel at the same time, and every published event reaches the consumer exactly once because the channel transfers ownership of each item to whichever read call consumes it rather than letting the item be shared or duplicated across readers. The classic Iterator pattern walks a fixed, already-complete aggregate; this feed instead exposes the identical foreach-shaped traversal over an aggregate that is still being written, suspending between items rather than returning early, and resuming the instant the next event is published or Complete is called to close the stream. The EnumeratorCancellation attribute on the cancellation token parameter is what wires a caller's own token into the compiler-generated enumerator so cancelling mid-stream actually stops the traversal; its exact wiring is not the point here, only that the concurrency guarantee, one feed, written and read concurrently, with exactly-once delivery, holds regardless of how the enumerator happens to be built underneath. A producer and a consumer running against this same feed on separate tasks is exactly the scenario the test suite for this type exercises directly.");
}

{
  const s = S();
  addCodeSlide(s, "CLASSIC PATTERNS UNDER CONCURRENCY", "Decorator over async, Command as Task", "C#", [
    "public sealed class CountingValidatorDecorator(IManifestValidator inner) : IManifestValidator",
    "{",
    "    private int _calls;",
    "",
    "    public int Calls => Volatile.Read(ref _calls);",
    "",
    "    public async Task<bool> ValidateAsync(HandlingEvent e, CancellationToken ct)",
    "    {",
    "        Interlocked.Increment(ref _calls);",
    "        return await inner.ValidateAsync(e, ct);",
    "    }",
    "}",
    "",
    "public sealed class CommandInvoker",
    "{",
    "    private int _totalCommandsRun;",
    "",
    "    public async Task<IReadOnlyList<HandlingReceipt>> RunAllAsync(",
    "        IReadOnlyCollection<IHandlingCommand> commands, CancellationToken ct)",
    "    {",
    "        var pending = commands.Select(c => c.ExecuteAsync(ct)).ToList();",
    "        var receipts = await Task.WhenAll(pending);",
    "        Interlocked.Add(ref _totalCommandsRun, receipts.Length);",
    "        return receipts;",
    "    }",
    "}",
  ], "Decorator forwards an awaited call while counting atomically; Command runs every ExecuteAsync concurrently and gathers results with Task.WhenAll.", { fontSize: 9 });
  addNotes(s, "CountingValidatorDecorator wraps an IManifestValidator the same way any Decorator wraps the component it decorates, implementing the identical interface and forwarding every call to the inner instance, adding behavior, here a call counter, before or after the delegation; because many callers can be awaiting ValidateAsync concurrently, the counter updates with Interlocked.Increment rather than a plain field increment that a concurrent call could race against. CommandInvoker is the Command pattern's invoker running a batch of commands concurrently instead of one at a time: each command's ExecuteAsync starts immediately when Select materializes the list, and Task.WhenAll gathers every receipt, or propagates the first exception, once every command has finished, with TotalCommandsRun kept as a running count across every call to RunAllAsync using Interlocked.Add. RecordHandlingCommand is the concrete command behind this invoker, recording one HandlingEvent against a ledger, the Command pattern's receiver, and returning the resulting HandlingReceipt as its result. Both patterns reach for the same small toolkit this deck keeps returning to, an atomic counter for shared state and Task.WhenAll for gathering concurrent work, rather than inventing a new primitive for either one.");
}

{
  patternSlide(
    "CLASSIC PATTERNS UNDER CONCURRENCY",
    "Immutability as the strategy",
    [
      "HandlingEvent is a sealed record and TrackingId is a readonly record struct, so once either is constructed, no thread holding a reference to one can change it out from under another.",
      "A thread that wants a changed copy produces one with a non-destructive with expression; the original instance every other thread already holds stays exactly as it was.",
      "Immutable data has nothing left to coordinate on: no lock, no atomic counter, and no channel is needed to protect a value that cannot change once built.",
      "Every coordination mechanism earlier in this deck, a gate, a queue, a pool, exists specifically because the state behind it is mutable; immutability removes the need for the mechanism entirely.",
    ],
    "38-observer-immutable-snapshot",
    "HandlingEventHub's Raise iterates a captured snapshot of immutable observers over an immutable event: nothing in that loop can change while it runs.",
    "HandlingEvent being a sealed record and TrackingId being a readonly record struct is not an incidental detail in this deck; it is the reason none of the four classic patterns revisited in this section needed a lock protecting the event data itself. A record's generated equality and its by-value with expression mean a thread that wants a modified version of an event produces an entirely new instance, leaving the original exactly as every other thread currently holding a reference to it last saw it, so there is no shared mutable field for two threads to race over in the first place. This is the deck's closing thesis stated directly: every coordination mechanism covered earlier, a lock-based gate, a semaphore, an Active Object's channel, a producer-consumer queue, a connection pool, exists because some piece of state behind it can change while more than one caller might touch it. Immutable data removes that condition outright rather than coordinating around it more cleverly, which is why HandlingEventHub's Raise method needs nothing more than an immutable snapshot of its observer list and an immutable event to iterate safely under any amount of concurrent subscribing, unsubscribing, or raising happening around it.",
    { fontSize: 13 }
  );
}

// ===========================================================================
// Closing
// ===========================================================================
{
  const s = S();
  addContentTitle(s, "CLOSING", "The coordination ladder, one rung at a time");
  addTwoColBullets(s, [
    "Suspension is cheap: an await is a small state machine, not a parked thread, and ValueTask skips building one at all when the answer is already known.",
    "Build once, share everywhere: Lazy<T>, a hand-rolled lock, and a static initializer all reach the identical guarantee, build exactly once, cache a failure, publish the same instance.",
    "Coordinate a shared servant on the smallest mechanism that actually works: a lock when the body is synchronous, a semaphore once an await has to live inside the gate, an Active Object once the gate itself becomes the bottleneck.",
  ], [
    "Hand work across a boundary with a bounded channel: backpressure and a deterministic drain replace a wall-clock guess about whether a pipeline has finished.",
    "Ration a scarce resource, a connection or a buffer, by handing it back instead of rebuilding it; a channel makes the wait queue and the storage the same structure.",
    "The classic Gang of Four catalogue still holds under concurrency: Observer, Iterator, Decorator, and Command each reach for an immutable snapshot, an atomic counter, or a channel rather than a new primitive.",
  ], { fontSize: 15 });
  addNotes(s, "Six stops, one ladder: start from the cheapest possible unit of suspension, a state machine rather than a thread, then build upward through sharing a value once, coordinating a single shared servant, handing work across a queue, rationing a resource that cannot grow, and finally revisiting a familiar pattern catalogue under the same lens. Each stop reached for the smallest mechanism that actually solved its problem rather than the most powerful one available: a static initializer when no lock was needed at all, a semaphore only once an await had to live inside a gate, a channel once a gate's own limitations, no fairness, no visible depth, became the actual bottleneck. The throughline connecting every stop is immutability and ownership transfer doing as much of the coordination work as possible before reaching for an explicit lock or counter, which is exactly the move the closing classic-patterns section makes explicit: HandlingEvent being immutable is what lets Observer, Iterator, Decorator, and Command all avoid a lock entirely. A team applying any one of these six patterns to its own shipping codebase can reach for the matching stage directly, the reach-for ladder that follows names the six choices side by side.");
}

refTable(
  "REFERENCE",
  "The reach-for ladder",
  [
    { code: "lock / System.Threading.Lock", name: "Mechanism", purpose: "A synchronous critical section: the body must be entirely synchronous, no await can live inside it." },
    { code: "Lazy<T> ExecutionAndPublication", name: "Mechanism", purpose: "Build a shared value once on first use; the framework guarantees a single build and caches a thrown failure for every later caller." },
    { code: "SemaphoreSlim(1, 1)", name: "Mechanism", purpose: "An async-aware gate once an await has to live inside the critical section; no FIFO ordering and no visible queue depth." },
    { code: "Channel<T> Active Object", name: "Mechanism", purpose: "Serialize every call through one consumer on a bounded inbox; backpressure replaces the gate, and the servant stays untouched." },
    { code: "Producer-Consumer pipeline", name: "Mechanism", purpose: "Decouple a pace mismatch between many producers and many consumers; FullMode.Wait makes backpressure visible instead of growing a buffer." },
    { code: "Pool over Channel / ArrayPool", name: "Mechanism", purpose: "Ration a scarce connection or buffer by handing it back instead of rebuilding it; the channel doubles as idle storage and wait queue." },
    { code: "ImmutableArray + record", name: "Mechanism", purpose: "Share data across threads with nothing left to coordinate on; a with expression replaces mutation entirely." },
  ],
  "Seven rows, one ladder from the cheapest mechanism to the most structural. A synchronous lock and Lazy<T> solve the narrowest problems, one critical section or one shared value, with the least code. SemaphoreSlim and the Active Object's channel solve the same shared-servant problem at increasing scale, trading a bare gate for an awaitable one and then for a dedicated consumer once the gate itself becomes the bottleneck. A producer-consumer pipeline and a resource pool both reach for the identical Channel<T> primitive but in different roles, a shared work queue against an idle-resource store, which is exactly why naming each channel's bound and role mattered throughout this deck. Immutability closes the ladder at the top: a record and an immutable array remove the need for any of the other six mechanisms entirely, by making the underlying data not subject to a race in the first place."
);

/* --- Scan to explore (QR codes) --- */
{
  const s = S();
  addContentTitle(s, "SCAN TO EXPLORE", "Take it with you");

  s.addImage({ path: `${ASSETS}/qr-csharp-site.png`, x: 2.30, y: 1.78, w: 3.10, h: 3.10 });
  s.addText("The site", { x: 1.55, y: 4.98, w: 4.60, h: 0.40,
    fontFace: FONT.title, fontSize: 18, bold: true, color: COLOR.ink, align: "center", valign: "middle" });
  s.addText("Chapters, runnable C# examples, and the diagrams", { x: 1.45, y: 5.40, w: 4.80, h: 0.72,
    fontFace: FONT.body, fontSize: 13, italic: true, color: COLOR.caption, align: "center", valign: "top" });
  s.addText("patterncatalyst.github.io/csharp-design-patterns", { x: 1.45, y: 6.12, w: 4.80, h: 0.32,
    fontFace: FONT.mono, fontSize: 10, color: COLOR.red, align: "center", valign: "middle" });

  s.addImage({ path: `${ASSETS}/qr-hub.png`, x: 7.93, y: 1.78, w: 3.10, h: 3.10 });
  s.addText("Workshops & Tutorials", { x: 7.18, y: 4.98, w: 4.60, h: 0.40,
    fontFace: FONT.title, fontSize: 18, bold: true, color: COLOR.ink, align: "center", valign: "middle" });
  s.addText("The whole series: every workshop, tutorial, and deck in one place", { x: 7.08, y: 5.40, w: 4.80, h: 0.72,
    fontFace: FONT.body, fontSize: 13, italic: true, color: COLOR.caption, align: "center", valign: "top" });
  s.addText("patterncatalyst.github.io/patterncatalyst-workshops-tutorials-list", { x: 7.08, y: 6.12, w: 4.80, h: 0.32,
    fontFace: FONT.mono, fontSize: 9, color: COLOR.red, align: "center", valign: "middle" });

  addNotes(s, "This is the Design Patterns with C# deck, this is the 401 concurrency patterns deck. The two codes take it with you: on the left, the site, the chapters, the runnable C# examples, and the diagrams. On the right, the Workshops and Tutorials hub, every workshop, tutorial, and deck in the series in one place. Both are public on the Pattern Catalyst GitHub organization.");
}

// ===========================================================================
pres.writeFile({ fileName: OUT })
  .then(p => console.log("WROTE", p, "slides:", pageNum))
  .catch(e => { console.error(e); process.exit(1); });
