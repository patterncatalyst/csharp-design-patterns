// deck-201.js - "Design Patterns with C#" - 201: Idiomatic C#
// Red Hat-branded 16:9 deck. Build:  node deck-201.js  (from presentation/)

"use strict";

const H = require("./deck-helpers.js");
const {
  COLOR, FONT, W, ASSETS, PNG,
  newDeck, addFooter, addContentTitle, addBullets, addTwoColBullets,
  addStatusTable, addCaption, addSectionDivider, addNotes,
  addCodeSlide, addDiagramSlide,
} = H;

const OUT = "csharp-design-patterns-201-idiomatic-csharp.pptx";

const pres = newDeck();
pres.title = "Design Patterns with C# - 201: Idiomatic C#";
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
function patternSlide(eyebrow, title, bullets, fig, caption, notes) {
  const s = S();
  addContentTitle(s, eyebrow, title);
  const hasFig = !!fig;
  addBullets(s, bullets, {
    x: 0.62, y: 1.85, w: hasFig ? 5.55 : 12.09, h: 4.55,
    fontSize: hasFig ? 15.5 : 17,
  });
  if (hasFig) {
    s.addImage({
      path: `${PNG}/${fig}.png`,
      x: 6.45, y: 1.75, w: 6.25, h: 4.35,
      sizing: { type: "contain", w: 6.25, h: 4.35 },
    });
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
  s.addText("201: Idiomatic C#", {
    x: 6.00, y: 4.65, w: 6.70, h: 0.90,
    fontFace: FONT.body, fontSize: 18, italic: true, color: COLOR.caption,
    align: "left", valign: "top",
  });
  try { s.addImage({ path: `${ASSETS}/logo-candidate-2.png`, x: 11.10, y: 6.80, w: 1.55, h: 0.37 }); } catch (e) {}
  addNotes(s, "Design Patterns with C#, the 201 deck: idiomatic C#. Where the first deck asked which force a classic pattern resolves, this deck asks what a rate calculator, a carrier gateway, and a consignment manifest look like when they are written the way C# 14 and .NET 10 actually want them written. We walk value types and immutable records, the iterator and async shapes the language generates for us, two ways to report an expected failure plus the options pattern for configuration, and the four features that let C# treat code itself as inspectable or generable data: extension members, expression trees, source generators, and analyzers, closing with static abstract members and generic math. Every example lives in the same shipping domain as the rest of the series: consignments, carriers, rate quotes, manifests, tariffs.");
}

// ===========================================================================
// Section 01: Value, immutability, and lifetime
// ===========================================================================
divider("01", "Value, Immutability, and Lifetime", "Model state as a value, not a mutable object",
  "This section is about where a piece of state lives and who owns the moment it goes away. A readonly record struct turns a quantity like a weight or a monetary amount into a value with built-in equality and a validated constructor; a sealed record turns a consignment into an immutable snapshot that amends by producing a new snapshot rather than mutating the old one. The section closes with IDisposable and IAsyncDisposable, where a file handle or a network stream gets released the instant a using scope ends rather than whenever a garbage collector eventually gets to it. All three habits share one idea: make the lifetime and the shape of a value explicit in the type, so a caller never has to guess.");

patternSlide(
  "VALUE TYPES",
  "Model a value as a value",
  [
    "A readonly record struct gives Money and Weight copy-on-assignment semantics and compiler-generated equality, with validation enforced in the constructor.",
    "default(Money) skips that constructor; the type owns its own safe zero through Money.Zero rather than leaving every caller to guard against the struct's implicit default.",
    "A ReadOnlySpan<char> parses a fixed-width consignment segment directly off the wire with no intermediate string allocation on the happy path.",
  ],
  "08-value-types-struct-span",
  "Weight and Money as readonly record structs; EdiRecordParser reading a consignment segment from a span with zero allocation.",
  "A readonly record struct is the right shape for a quantity that should behave like a number: assigning it copies it, comparing two instances compares their values, and the compiler writes the equality members so nobody hand-rolls them. The catch every struct carries is the implicit parameterless default every .NET struct gets for free, which bypasses every constructor and every validation a type's own constructor performs; Money answers that by publishing its own safe Money.Zero rather than asking every caller to remember the trap. EdiRecordParser is a readonly ref struct specifically because only a ref struct may hold a ReadOnlySpan<char> field, and that restriction is also what keeps every instance stack-only and forces the parsing to stay synchronous and non-capturing."
);

{
  const s = S();
  addCodeSlide(s, "VALUE TYPES", "readonly record struct: equality without boilerplate", "C#", [
    "public readonly record struct Weight(decimal Kilograms)",
    "{",
    "    public decimal Kilograms { get; init; } = Kilograms >= 0m",
    "        ? Kilograms",
    "        : throw new ArgumentOutOfRangeException(",
    "            nameof(Kilograms), Kilograms, \"Weight cannot be negative.\");",
    "",
    "    public override string ToString() => $\"{Kilograms:0.00} kg\";",
    "}",
    "",
    "// The compiler generates IEquatable<Weight>, GetHashCode, ==, and !=",
    "// from the Kilograms member alone.",
  ], "One property declaration gets validated construction, value equality, and a formatted ToString with zero hand-written boilerplate.");
  addNotes(s, "The primary constructor parameter is shadowed by an explicit init-only property so construction can validate: a negative kilogram value throws before a Weight ever exists. Because this is a record struct, the compiler also generates IEquatable<Weight>, a consistent GetHashCode, and both equality operators from that one member, so two independently constructed weights with the same value compare equal without a line of hand-written equality code. Unlike Money, a Weight's implicit default of zero kilograms is itself a legitimate, formattable value, so this type needs no separate Zero sentinel the way Money does.");
}

{
  const s = S();
  addCodeSlide(s, "VALUE TYPES", "A view, not a copy", "C#", [
    "public readonly ref struct EdiRecordParser",
    "{",
    "    private readonly ReadOnlySpan<char> _segment;",
    "",
    "    private EdiRecordParser(ReadOnlySpan<char> segment) => _segment = segment;",
    "",
    "    public static bool TryParse(ReadOnlySpan<char> segment, out ConsignmentRecord record)",
    "    {",
    "        var parser = new EdiRecordParser(segment);",
    "        return parser.TryParseCore(out record);",
    "    }",
    "",
    "    private bool TryParseCore(out ConsignmentRecord record)",
    "    {",
    "        record = default;",
    "        if (!int.TryParse(_segment.Slice(0, 6), NumberStyles.None,",
    "                CultureInfo.InvariantCulture, out var consignmentId))",
    "        {",
    "            return false;",
    "        }",
    "",
    "        if (!decimal.TryParse(_segment.Slice(10, 7), NumberStyles.Number,",
    "                CultureInfo.InvariantCulture, out var kilograms))",
    "        {",
    "            return false;",
    "        }",
    "        // remaining fields follow the same Slice-and-TryParse shape",
    "    }",
    "}",
  ], "A readonly ref struct can hold a ReadOnlySpan<char> field; Slice and TryParse read the segment with CultureInfo.InvariantCulture and allocate nothing on a successful parse.");
  addNotes(s, "EdiRecordParser never materializes the 28-character segment as a string; it slices the span at fixed offsets and hands each slice straight to int.TryParse or decimal.TryParse, both of which accept a ReadOnlySpan<char> overload directly. CultureInfo.InvariantCulture is passed explicitly so the parse result never depends on the host machine's regional settings, a correctness requirement for EDI data rather than a performance one. The method returns false rather than throwing for a wrong-length segment, a non-numeric field, or an unrecognized currency code, so a stream of consignment records can skip a malformed line without aborting the whole file.");
}

patternSlide(
  "RECORDS & IMMUTABILITY",
  "State the contract once",
  [
    "A sealed record Consignment gets recursive value equality for free: two independently built consignments with the same Route, Weight, and reference number compare equal.",
    "required plus init closes a gap nullable reference types leave open: a non-nullable property can only be flagged possibly-unassigned, but required refuses to compile an initializer that omits it.",
    "A non-destructive with expression produces an amended consignment and leaves the original untouched; a nullable string? ReferenceNumber retires the instinct to reach for a do-nothing stand-in object.",
  ],
  "09-nrts-records-init-required-with",
  "Consignment as a sealed record: required and init on the invariant fields, with expressions for every amendment, string? for a legitimately absent reference number.",
  "Consignment is a non-positional record class: every property is declared explicitly with required and init rather than through a primary constructor, so construction and later amendment read as two separate concerns. required is specifically the piece nullable reference types cannot enforce: a non-nullable property without it can still be left unassigned, with only a compiler warning to catch it, while a required property makes an incomplete object initializer a compile error. A 1994-era object model had no type-level way to mark a reference as never-absent, which is part of why a do-nothing stand-in object was the only safe shape for a legitimately missing value; here string? ReferenceNumber says plainly that absence is expected and lets the compiler's flow analysis catch an unchecked read."
);

{
  const s = S();
  addCodeSlide(s, "RECORDS & IMMUTABILITY", "required, init, and non-destructive with", "C#", [
    "public sealed record Consignment",
    "{",
    "    public required string ConsignmentId { get; init; }",
    "    public required Route Route { get; init; }",
    "    public required Weight Weight { get; init; }",
    "    public string? ReferenceNumber { get; init; }",
    "    public Money DeclaredValue { get; init; } = Money.Zero;",
    "",
    "    public Consignment ReRouteTo(string destination) =>",
    "        this with { Route = Route with { Destination = destination } };",
    "",
    "    public Consignment WithCorrectedWeight(Weight weight) =>",
    "        this with { Weight = weight };",
    "}",
  ], "ReRouteTo nests a with expression one level into Route; the original consignment instance never changes.");
  addNotes(s, "ReRouteTo reaches one level into Route with its own with expression before applying the outer Consignment's with, so a re-route amends exactly one field two layers down and copies everything else unchanged, including the weight, the reference number, and the declared value. DeclaredValue shows that a default value and a required member coexist without friction: it defaults to Money.Zero yet still participates fully in the record's generated equality. Equality itself recurses into Route, Weight, and Money, so two independently constructed consignments with identical property values compare equal through == with no hand-written comparison logic anywhere.");
}

{
  const s = S();
  addCodeSlide(s, "RECORDS & IMMUTABILITY", "Deterministic cleanup", "C#", [
    "public sealed class ManifestWriter : IDisposable",
    "{",
    "    private bool _disposed;",
    "",
    "    public void WriteLine(ManifestEntry entry)",
    "    {",
    "        ObjectDisposedException.ThrowIf(_disposed, this);",
    "        // write and flush the line immediately",
    "    }",
    "",
    "    public void Dispose()",
    "    {",
    "        if (_disposed) return;",
    "        _stream.Dispose();",
    "        _disposed = true;",
    "    }",
    "}",
    "",
    "public sealed class LedgerAppender : IAsyncDisposable",
    "{",
    "    public async ValueTask DisposeAsync()",
    "    {",
    "        if (_disposed) return;",
    "        await _writer.DisposeAsync().ConfigureAwait(false);",
    "        _disposed = true;",
    "    }",
    "}",
  ], "ObjectDisposedException.ThrowIf guards every call after disposal; DisposeAsync returns a ValueTask and flows ConfigureAwait(false) through library code.");
  addNotes(s, "ManifestWriter is sealed and owns exactly one managed IDisposable, the underlying FileStream, so it needs no finalizer and no full Dispose(bool) pattern: the full pattern exists for a subclassable type that might hold an unmanaged resource directly, and neither condition applies here. ObjectDisposedException.ThrowIf is the modern one-line guard that replaces a hand-written if-throw block at the top of every member that touches disposed state. LedgerAppender's DisposeAsync flushes and closes an async-capable StreamWriter, returning a ValueTask because disposal rarely needs to allocate a full Task, and ConfigureAwait(false) on every internal await keeps a library method from forcing a continuation back onto a captured synchronization context it does not need.");
}

// ===========================================================================
// Section 02: Sequences and async, the idiomatic shapes
// ===========================================================================
divider("02", "Sequences and Async, the Idiomatic Shapes", "Where iteration and asynchrony meet",
  "A method that produces a sequence of values one at a time, and a method that waits on something outside the process, used to require very different hand-written plumbing. The compiler now generates both: yield return turns an ordinary-looking method into a state machine that resumes where it left off, and async turns an await-laced method into a state machine that suspends without blocking a thread. This section walks a tracking history paginated lazily, a live carrier feed streamed through IAsyncEnumerable<T>, and the recognizable, consistent async surface the Task-based Asynchronous Pattern expects every asynchronous member to present.");

patternSlide(
  "ITERATORS & LINQ",
  "A method that becomes a state machine",
  [
    "yield return compiles Paginate into a state machine that suspends and resumes across each MoveNext call, with no manual enumerator class to write by hand the way a 1994-era Iterator pattern required.",
    "Paginate validates pageSize eagerly and delegates the yield return work to PaginateIterator, because an iterator method defers its entire body, argument checks included, until the caller actually enumerates it.",
    "Select and Distinct compose lazily over the same sequence; nothing runs until a foreach pulls a value, which is deferred execution.",
  ],
  "23-deferred-execution",
  "Paginate validates eagerly before handing off to PaginateIterator; StagesReached composes Select and Distinct over the same lazily evaluated sequence.",
  "A classic Iterator pattern meant writing a concrete enumerator class that tracked its own position by hand; yield return lets the compiler generate that state machine from a method body that reads like an ordinary loop with yield statements sprinkled through it. The split between Paginate and PaginateIterator exists because an iterator method's whole body, including an ArgumentOutOfRangeException on an invalid page size, is deferred until the first MoveNext call; validating in a small eager wrapper means a caller gets the exception the moment it calls Paginate, not on the first iteration several lines later. StagesReached layers Select and Distinct on top of the same buffered event list, and neither operator does any work until something actually enumerates the result, which is deferred execution working exactly the way LINQ promises."
);

{
  const s = S();
  addCodeSlide(s, "ITERATORS & LINQ", "yield return and eager validation", "C#", [
    "public IEnumerable<TrackingPage> Paginate(int pageSize)",
    "{",
    "    if (pageSize <= 0)",
    "    {",
    "        throw new ArgumentOutOfRangeException(",
    "            nameof(pageSize), pageSize, \"Page size must be a positive number of events.\");",
    "    }",
    "",
    "    return PaginateIterator(pageSize);",
    "}",
    "",
    "private IEnumerable<TrackingPage> PaginateIterator(int pageSize)",
    "{",
    "    var pageNumber = 1;",
    "    var buffer = new List<TrackingEvent>(pageSize);",
    "",
    "    foreach (var trackingEvent in _events)",
    "    {",
    "        buffer.Add(trackingEvent);",
    "        if (buffer.Count == pageSize)",
    "        {",
    "            yield return new TrackingPage(pageNumber, buffer);",
    "            pageNumber++;",
    "            buffer = new List<TrackingEvent>(pageSize);",
    "        }",
    "    }",
    "",
    "    if (buffer.Count > 0)",
    "    {",
    "        yield return new TrackingPage(pageNumber, buffer);",
    "    }",
    "}",
  ], "Paginate throws on an invalid page size the instant it is called; PaginateIterator's yield return work only starts once a caller enumerates the result.");
  addNotes(s, "PaginateIterator is the method the compiler actually rewrites into a state machine: every local variable, the buffer, the page number, and the position in the foreach loop, becomes a field on a compiler-generated class that implements IEnumerable<TrackingPage> and IEnumerator<TrackingPage>. Each yield return suspends execution and hands one TrackingPage back to the caller; the next MoveNext call resumes exactly where the method left off, buffer and all. Paginate itself is deliberately not an iterator method: it validates pageSize synchronously and returns the call to PaginateIterator, so a caller sees the exception immediately rather than on the first enumeration.");
}

{
  const s = S();
  addCodeSlide(s, "ITERATORS & LINQ", "await foreach over a live feed", "C#", [
    "public sealed class CarrierEventFeed(IEnumerable<TrackingEvent> events) : ICarrierFeed",
    "{",
    "    public async IAsyncEnumerable<TrackingEvent> StreamAsync(",
    "        string consignmentId, [EnumeratorCancellation] CancellationToken cancellationToken)",
    "    {",
    "        foreach (var trackingEvent in _events)",
    "        {",
    "            if (trackingEvent.ConsignmentId != consignmentId) continue;",
    "            cancellationToken.ThrowIfCancellationRequested();",
    "            await Task.Yield();",
    "            yield return trackingEvent;",
    "        }",
    "    }",
    "}",
    "",
    "// Consumed with cancellation flowing through the attribute:",
    "await foreach (var evt in feed.StreamAsync(id, token).WithCancellation(token))",
    "{",
    "    Record(evt);",
    "}",
  ], "[EnumeratorCancellation] lets .WithCancellation(token) reach the iterator's own cancellationToken parameter instead of a default, uncancellable token.");
  addNotes(s, "An async IAsyncEnumerable<T> method combines both state machines at once: it suspends across an await the way any async method does, and it yields one value at a time the way a synchronous iterator does. Without the EnumeratorCancellationAttribute on the token parameter, the compiler generates its own internal, uncancellable token for the enumerator, and a caller's .WithCancellation(token) on the consuming side would have nowhere to attach; the attribute is what wires the two together. Task.Yield stands in for the network hop a real carrier call would make, letting the example demonstrate suspension without any wall-clock delay that would make a test suite flaky.");
}

patternSlide(
  "ASYNC CONVENTIONS",
  "One recognizable async surface",
  [
    "Every asynchronous member on ICarrierGateway ends in Async and returns Task, Task<T>, or ValueTask<T>, the shape the Task-based Asynchronous Pattern names once so every caller recognizes it on sight.",
    "A trailing CancellationToken is honored, not merely accepted: DispatchAsync and DispatchBatchAsync both check it, so a caller can cancel a dispatch without the gateway inventing its own cancellation story.",
    "IsReachableAsync returns ValueTask<bool> because a healthy gateway often answers synchronously, avoiding a Task<T> allocation for work that never actually goes asynchronous.",
  ],
  "32-tap-and-async-conventions",
  "ICarrierGateway's three members: DispatchAsync, DispatchBatchAsync with an optional IProgress<T>, and IsReachableAsync returning ValueTask<bool>.",
  "The Task-based Asynchronous Pattern is a naming and shape convention, not a language feature: Async as a suffix, a Task-family return type, and a trailing CancellationToken with a default value so a caller who does not care about cancellation can still call the method with no extra argument. DispatchBatchAsync's optional IProgress<DispatchProgress> lets a caller observe partial completion across a batch without forcing every caller to supply one, which is the same shape the framework's own long-running operations use. Reserving ValueTask<T> for IsReachableAsync rather than every method matters: a method that is usually asynchronous should keep returning Task<T>, and the ValueTask<T> saving is real only for a method like a reachability check that frequently completes synchronously."
);

{
  const s = S();
  addCodeSlide(s, "ASYNC CONVENTIONS", "Honor the token; ValueTask where synchronous", "C#", [
    "public interface ICarrierGateway",
    "{",
    "    Task<DispatchReceipt> DispatchAsync(",
    "        DispatchRequest request, CancellationToken cancellationToken = default);",
    "",
    "    Task<IReadOnlyList<DispatchReceipt>> DispatchBatchAsync(",
    "        IReadOnlyList<DispatchRequest> requests,",
    "        IProgress<DispatchProgress>? progress = null,",
    "        CancellationToken cancellationToken = default);",
    "",
    "    ValueTask<bool> IsReachableAsync(CancellationToken cancellationToken = default);",
    "}",
  ], "An optional IProgress<DispatchProgress> reports partial completion through DispatchBatchAsync without forcing every caller to supply one.");
  addNotes(s, "Every member name ends in Async, every return type is a Task-family type, and every member accepts a trailing CancellationToken with a default value, so a reader scanning this interface for the first time already knows how each member behaves before reading its body. DispatchBatchAsync's progress parameter is also optional and defaults to null, following the same pattern as the cancellation token: a caller who does not need progress reporting pays nothing for the capability being present. IsReachableAsync is the one ValueTask<bool> member on the interface, chosen because a reachability check against a healthy gateway frequently has an answer ready without ever touching the network, and allocating a Task<bool> for that common case would be pure overhead.");
}

// ===========================================================================
// Section 03: Typed outcomes and typed configuration
// ===========================================================================
divider("03", "Typed Outcomes and Typed Configuration", "Say what can fail, and what can be misconfigured, in the type system",
  "An expected failure and a broken contract are not the same event, and this section is about keeping them that way. A malformed EDI message, an untested customs declaration, and a bad carrier endpoint are all expected failures that a caller should be able to see coming; a null argument is a broken contract that should throw immediately. We compare three ways an expected failure shows up in modern C#, a Try-Parse boolean, a Tester-Doer pair, and a Result<T> value, then close with the options pattern, where a configuration error surfaces at startup instead of on the first request that happens to touch the bad setting.");

patternSlide(
  "FAILURE SHAPES",
  "Report expected failure as a value",
  [
    "EdiTryParser.TryParse reports a malformed message through its boolean return, the Framework Design Guidelines shape, never through an exception for ordinary bad input.",
    "DeclarationProcessor's CanProcess and Process form a Tester-Doer pair, though nothing stops another thread acting between a passed test and a later Process call, an atomicity gap a single Result<T> call does not have.",
    "Result<T> carries either a value or a named Error and composes through Map, so a chain of operations never branches on IsSuccess by hand; ArgumentNullException.ThrowIfNull still guards a genuinely broken contract.",
  ],
  "31-try-parse-vs-result",
  "Three ways to report failure on the same declaration: Try-Parse's boolean return, Tester-Doer's CanProcess/Process pair, and Result<T>'s Success, Failure, and Map.",
  "All three shapes answer the same question, how should a method tell its caller that an input it cannot act on was expected, not exceptional, but they trade differently. TryParse keeps the two concerns, testing and acting, inside one call, so there is nothing for a caller to forget between them. The Tester-Doer pair on DeclarationProcessor separates CanProcess from Process explicitly, which reads clearly but opens a window between the two calls where state could change out from under a caller in a concurrent setting; Process still throws if that window is exploited, because skipping the tester is a programming error, not an expected outcome. Result<T> folds the test and the payload into a single value that a caller cannot misuse by forgetting a check, and Map lets a chain of validations keep composing without ever branching on IsSuccess directly."
);

{
  const s = S();
  addCodeSlide(s, "FAILURE SHAPES", "Try-Parse, Tester-Doer, and Result<T>", "C#", [
    "public readonly record struct Result<T>",
    "{",
    "    public bool IsSuccess { get; }",
    "    public T? Value { get; }",
    "    public string? Error { get; }",
    "",
    "    public static Result<T> Success(T value) => new(true, value, null);",
    "    public static Result<T> Failure(string error) => new(false, default, error);",
    "",
    "    public Result<TOut> Map<TOut>(Func<T, TOut> selector) =>",
    "        IsSuccess ? Result<TOut>.Success(selector(Value!)) : Result<TOut>.Failure(Error!);",
    "}",
    "",
    "public static bool TryParse(string raw, out EdiMessage? message)",
    "{",
    "    message = null;",
    "    if (string.IsNullOrWhiteSpace(raw)) return false;",
    "    // parses one segment per line; returns false on the first malformed line",
    "}",
  ], "Result<T>.Map transforms a success and passes a failure through unchanged; EdiTryParser.TryParse never throws for a malformed message.");
  addNotes(s, "Result<T> is a readonly record struct with a private constructor, so Success and Failure are the only two ways to build one, and a caller can never construct a half-valid instance with both a value and an error set. Map is what makes this a composable shape rather than just a tagged pair: calling it on a success runs the selector and wraps the output as a new success, while calling it on a failure skips the selector entirely and forwards the same error message, so a pipeline of transformations reads as a flat chain with no manual branching. EdiTryParser.TryParse sits beside it as the Framework Design Guidelines alternative for the same kind of problem, parsing one EDI segment per line and returning false the moment any line fails to parse, with no partial message ever escaping through the out parameter.");
}

{
  const s = S();
  addCodeSlide(s, "FAILURE SHAPES", "Typed configuration: the Options pattern", "C#", [
    "public sealed class CarrierGatewayOptions",
    "{",
    "    public const string SectionName = \"CarrierGateway\";",
    "    public string Endpoint { get; set; } = \"\";",
    "    public int TimeoutSeconds { get; set; }",
    "    public int RetryBudget { get; set; }",
    "}",
    "",
    "public static IServiceCollection AddCarrierGateway(",
    "    this IServiceCollection services, IConfiguration configuration)",
    "{",
    "    services",
    "        .AddOptions<CarrierGatewayOptions>()",
    "        .Bind(configuration.GetSection(CarrierGatewayOptions.SectionName))",
    "        .ValidateOnStart();",
    "",
    "    services.AddSingleton<IValidateOptions<CarrierGatewayOptions>, CarrierGatewayOptionsValidator>();",
    "    return services;",
    "}",
  ], "ValidateOnStart runs IValidateOptions<CarrierGatewayOptions> at startup, failing fast on a bad endpoint instead of on the first request.");
  addNotes(s, "CarrierGatewayOptions is a plain settable class rather than a record because the configuration binder populates it by setting properties after construction, not through a constructor; Bind wires it to the CarrierGateway configuration section by name, and ValidateOnStart means a bad endpoint or an invalid retry budget fails application startup instead of surfacing on whatever request happens to touch the gateway first. Beyond IValidateOptions<T>, the framework exposes the bound value through three different interfaces depending on how often a consumer needs a fresh read: IOptions<T> resolves once and caches for the life of a singleton, IOptionsSnapshot<T> re-binds once per scope so a web request sees configuration current as of that request, and IOptionsMonitor<T> actively watches the configuration source and can notify a subscriber the moment a value changes, even inside a singleton. Choosing between the three is a question of how fresh a consumer's view of configuration needs to be, not a question of which one is more correct.");
}

// ===========================================================================
// Section 04: C# as data
// ===========================================================================
divider("04", "C# as Data: Extend, Inspect, Generate, Enforce", "Treat code itself as a value the compiler can reason about",
  "The last four idioms in this deck treat a piece of code, a type, an expression, or a design rule, as something another piece of code can read, extend, or produce. Extension members add behavior to a sealed vendor type without touching its source; expression trees turn a lambda into a tree of nodes a visitor can read or rewrite before anything runs; source generators and analyzers both run inside the compiler itself, one producing new code from a manifest, the other flagging a violated rule as a build diagnostic. The section closes with static abstract members and generic math, where a single algorithm runs unchanged over a custom shipment type and over a built-in numeric type.");

patternSlide(
  "EXTENSION MEMBERS",
  "Add members to a type you do not own",
  [
    "An extension(CarrierShipment shipment) block adds instance-shaped members, WeightKilograms and IsHeavy properties and a non-mutating WithAddedWeight, to a sealed vendor type with no setters to mutate.",
    "A second extension(CarrierShipment) block with no receiver parameter adds static members, Empty and FromKilograms, reading like real static members of the vendor type itself.",
    "An extension operator + composes two shipments into one manifest line, paired with a named Add alternate the way CA2225 expects; the classic this-parameter form still reads right for a single method like NormalisedDestination.",
  ],
  "25-extension-members",
  "Two extension blocks over the same sealed CarrierShipment vendor type: one instance-shaped, one static, plus an extension operator.",
  "CarrierShipment stands in for a third-party carrier SDK type this codebase cannot modify: sealed, constructor and read-only properties only, owned by a vendor. The two extension blocks let CarrierShipmentExtensions add an instance-shaped surface and a static surface to that type without ever touching its source again, and because the vendor type has no setters, WithAddedWeight returns an entirely new CarrierShipment rather than mutating anything. The classic this-parameter extension method, NormalisedDestination, still earns its keep for a single standalone method; the newer extension block syntax pays off once several related members, including an operator, need to live together against the same receiver."
);

{
  const s = S();
  addCodeSlide(s, "EXTENSION MEMBERS", "An extension block over a sealed vendor type", "C#", [
    "public static class CarrierShipmentExtensions",
    "{",
    "    extension(CarrierShipment shipment)",
    "    {",
    "        public decimal WeightKilograms => shipment.WeightGrams / 1000m;",
    "        public bool IsHeavy => shipment.WeightGrams > 30_000;",
    "",
    "        public CarrierShipment WithAddedWeight(int grams) =>",
    "            new(shipment.WeightGrams + grams, shipment.DestinationCode,",
    "                shipment.DeclaredValueMinorUnits);",
    "",
    "        public static CarrierShipment operator +(CarrierShipment left, CarrierShipment right) =>",
    "            new(left.WeightGrams + right.WeightGrams, left.DestinationCode,",
    "                left.DeclaredValueMinorUnits + right.DeclaredValueMinorUnits);",
    "    }",
    "",
    "    extension(CarrierShipment)",
    "    {",
    "        public static CarrierShipment Empty => new(0, string.Empty, 0);",
    "        public static CarrierShipment FromKilograms(decimal kilograms, string destinationCode) =>",
    "            new(decimal.ToInt32(decimal.Round(kilograms * 1000m)), destinationCode, 0);",
    "    }",
    "}",
  ], "The vendor's CarrierShipment class is never edited; every added member lives in CarrierShipmentExtensions instead.");
  addNotes(s, "The first extension block takes a shipment receiver and adds instance-shaped members that read exactly like real instance properties and methods at the call site; WithAddedWeight constructs a brand-new CarrierShipment because the vendor type offers nothing to mutate. The second block has no receiver parameter at all, which is what makes Empty and FromKilograms static members of CarrierShipment rather than of some separate helper class. The extension operator plus a named Add alternate follows the same convention every operator overload in this book follows, because an analyzer rule expects a named method alongside any custom operator so a caller in a language or context that cannot use operator syntax still has a way to call it.");
}

patternSlide(
  "EXPRESSION TREES",
  "Code as inspectable data",
  [
    "Expression<Func<RateRequest, decimal>> builds a tree of nodes describing a computation, not a compiled method; nothing runs until something calls Compile().",
    "RateDescriber, an ExpressionVisitor, walks VisitBinary and VisitMember to render a tariff rule as readable text, reading the tree without ever running it.",
    "RateInflator rewrites every decimal constant by a factor and hands back a brand-new expression, because expression trees are immutable; PredicateComposer.And joins two independently authored rules and Compile() runs only once, at the end.",
  ],
  "26-expression-trees",
  "RateDescriber reads a tariff rule's tree; RateInflator rewrites its constants; PredicateComposer.And joins two rules into one before a single Compile().",
  "An Expression<Func<RateRequest, decimal>> parameter, rather than a plain Func<RateRequest, decimal>, is what makes a rate rule inspectable: the compiler builds a tree of ConstantExpression, MemberExpression, and BinaryExpression nodes describing the lambda instead of compiling it straight to IL, and that tree is what RateDescriber walks to narrate a rule as text. RateInflator demonstrates that the tree is immutable data: scaling every decimal constant by a factor means visiting the existing tree and building a new one, never mutating a node in place. PredicateComposer.And takes this further by rebinding one predicate's parameter onto another's before joining their bodies with a short-circuiting AndAlso, producing a single combined expression that gets compiled exactly once rather than evaluating two separately compiled delegates at runtime."
);

{
  const s = S();
  addCodeSlide(s, "EXPRESSION TREES", "An ExpressionVisitor that reads a rule", "C#", [
    "public sealed class RateDescriber : ExpressionVisitor",
    "{",
    "    private readonly StringBuilder _text = new();",
    "",
    "    public string Describe(Expression<Func<RateRequest, decimal>> rule)",
    "    {",
    "        _text.Clear();",
    "        Visit(rule.Body);",
    "        return _text.ToString();",
    "    }",
    "",
    "    protected override Expression VisitBinary(BinaryExpression node)",
    "    {",
    "        _text.Append('(');",
    "        Visit(node.Left);",
    "        _text.Append(' ').Append(DescribeOperator(node.NodeType)).Append(' ');",
    "        Visit(node.Right);",
    "        _text.Append(')');",
    "        return node;",
    "    }",
    "",
    "    protected override Expression VisitMember(MemberExpression node)",
    "    {",
    "        if (node.Expression is ParameterExpression parameter)",
    "        {",
    "            _text.Append(parameter.Name).Append('.').Append(node.Member.Name);",
    "        }",
    "        return node;",
    "    }",
    "}",
  ], "Describe never calls Compile(); it only reads the nodes the compiler already built for the lambda.");
  addNotes(s, "RateDescriber inherits ExpressionVisitor and overrides only the node types a tariff rule actually contains, VisitBinary, VisitMember, VisitConstant, and VisitParameter, letting the base class's default traversal handle every other node shape. VisitBinary recurses into both operands before and after writing the operator symbol, which naturally produces a fully parenthesized, readable rendering of however deeply the arithmetic nests. The whole point of the example is restraint: Describe calls Visit, never Compile, so narrating a rule as text never risks running the pricing logic it describes.");
}

patternSlide(
  "SOURCE GENERATORS",
  "Build the registry at build time",
  [
    "CarrierGatewayGenerator implements IIncrementalGenerator and runs inside the compiler itself, turning a plain-text carriers.manifest file into real gateway classes before the project's own code ever compiles.",
    "The pipeline reads AdditionalTextsProvider, Parses each manifest, Collects and Combines them, then RegisterSourceOutput emits one class per carrier plus a GeneratedCarrierRegistry.",
    "EquatableArray<CarrierEntry> lets the incremental pipeline compare an unchanged manifest by value, so an editor keystroke that does not touch the manifest reruns nothing; GeneratedCarrierRegistry.Find is called directly, with no reflection scan involved.",
  ],
  "27-incremental-generator-pipeline",
  "AdditionalTextsProvider feeds Parse, Collect, and Combine; RegisterSourceOutput emits one carrier gateway class per manifest row plus the registry.",
  "A source generator is compiler-adjacent code: IIncrementalGenerator.Initialize wires a pipeline of providers and transforms that run every time the compiler needs updated output, not a one-shot script run before the build. Each stage of CarrierGatewayGenerator's pipeline, Parse, Collect, Combine, carries a WithTrackingName so an incremental generator testing harness can assert on exactly which stage reran after an edit, which is how a generator author proves the caching is actually working rather than assuming it. EquatableArray<CarrierEntry> wraps the parsed entries specifically so the pipeline can compare two manifests by their contents rather than by array reference identity, which is what lets an edit to an unrelated file skip regenerating the carrier registry entirely."
);

{
  const s = S();
  addCodeSlide(s, "SOURCE GENERATORS", "The incremental pipeline", "C#", [
    "[Generator(LanguageNames.CSharp)]",
    "public sealed class CarrierGatewayGenerator : IIncrementalGenerator",
    "{",
    "    public void Initialize(IncrementalGeneratorInitializationContext context)",
    "    {",
    "        var parsedManifests = context.AdditionalTextsProvider",
    "            .Where(static file => file.Path.EndsWith(\".manifest\", StringComparison.OrdinalIgnoreCase))",
    "            .Select(static (text, cancellationToken) => Parse(text, cancellationToken))",
    "            .WithTrackingName(TrackingNames.Parse);",
    "",
    "        var model = parsedManifests.Collect()",
    "            .Select(static (manifests, _) => Combine(manifests))",
    "            .WithTrackingName(TrackingNames.Combine);",
    "",
    "        context.RegisterSourceOutput(model, Emit);",
    "    }",
    "}",
  ], "Each stage is named with WithTrackingName so the incremental generator testing harness can assert on which stage reran after an edit.");
  addNotes(s, "AdditionalTextsProvider surfaces every non-code file the build includes, and the Where filter narrows that down to files ending in .manifest before Select parses each one into a CarrierManifestModel; every lambda in the pipeline is marked static specifically so it cannot capture outer state and break the incremental caching. Collect gathers every parsed manifest into one array and Combine merges them into a single model, after which RegisterSourceOutput runs Emit once per distinct model value to produce one gateway class per carrier plus a GeneratedCarrierRegistry. Adding a new carrier to the shipped application becomes a one-line data change to carriers.manifest; no hand-written class is written or reviewed for it.");
}

{
  const s = S();
  addDiagramSlide(s, "SOURCE GENERATORS", "Generated registry versus a reflection scan", "27-generated-registry-vs-reflection",
    "GeneratedCarrierRegistry.All is a list the compiler wrote; a reflection-based scan for the same carriers runs at startup and does not survive trimming or ahead-of-time compilation.");
  addNotes(s, "GeneratedCarrierRegistry.Find walks a plain list that already exists at compile time, built once by the generator and baked into the assembly; a reflection-based alternative would scan loaded assemblies for types implementing the gateway interface every time the application starts, paying that cost on every cold start rather than once at build time. The reflection path also depends on every candidate type and its metadata surviving into the deployed binary, which a trimmed or ahead-of-time compiled application is specifically designed to strip away unless something tells the toolchain to keep it. The generated registry has no such dependency: it is ordinary, already-compiled code by the time trimming or native compilation ever runs.");
}

patternSlide(
  "ROSLYN ANALYZERS",
  "Encode a rule as a build diagnostic",
  [
    "MoneyAsDecimalAnalyzer is a DiagnosticAnalyzer with one DiagnosticDescriptor, SHIP0001, that flags a decimal-typed property on any type carrying a WireContract attribute.",
    "Initialize resolves the WireContractAttribute marker once in RegisterCompilationStartAction, then RegisterSymbolAction runs AnalyzeType against every named type in the compilation, concurrently.",
    "The rule reports a Warning by default; a project with TreatWarningsAsErrors turns a decimal money field on a wire contract into a build failure before it ships, with an AnalyzerReleases file tracking the ID under RS2008.",
  ],
  "28-roslyn-analyzers",
  "SHIP0001 flags a decimal-typed property on any WireContract type, resolved once per compilation and then checked symbol by symbol.",
  "An analyzer runs during every build and every keystroke inside an editor with live analysis enabled, so the rule it encodes is checked continuously rather than only at a dedicated review step. EnableConcurrentExecution and RegisterCompilationStartAction together are what make a single marker-type lookup, GetTypeByMetadataName for the WireContractAttribute, happen once per compilation instead of once per symbol, and RegisterSymbolAction then runs AnalyzeType against every named type the compiler visits, safely, in parallel. The Warning default severity means the rule surfaces without blocking a build by itself; a project opts into treating it as a hard failure through its own warnings-as-errors setting, and the AnalyzerReleases tracking file under the RS2008 convention is what keeps a shipped diagnostic ID and its severity documented across releases."
);

{
  const s = S();
  addCodeSlide(s, "ROSLYN ANALYZERS", "A diagnostic and its Initialize contract", "C#", [
    "private static readonly DiagnosticDescriptor Rule = new(",
    "    \"SHIP0001\",",
    "    title: \"Money must not be modeled as decimal on a wire contract\",",
    "    messageFormat: \"Property '{0}' on wire-contract type '{1}' uses decimal for money; use integer minor units\",",
    "    category: \"ShippingContracts\",",
    "    defaultSeverity: DiagnosticSeverity.Warning,",
    "    isEnabledByDefault: true);",
    "",
    "public override void Initialize(AnalysisContext context)",
    "{",
    "    context.EnableConcurrentExecution();",
    "    context.RegisterCompilationStartAction(start =>",
    "    {",
    "        var marker = start.Compilation.GetTypeByMetadataName(\"WireContracts.WireContractAttribute\");",
    "        if (marker is null) return;",
    "        start.RegisterSymbolAction(ctx => AnalyzeType(ctx, marker), SymbolKind.NamedType);",
    "    });",
    "}",
  ], "GetTypeByMetadataName resolves the marker attribute once per compilation rather than once per symbol.");
  addNotes(s, "The DiagnosticDescriptor is the whole public contract of the rule: a stable ID, a human-readable title and message format with placeholders for the offending property and type names, a category for grouping in build output, and a default severity a consuming project can still override in its own configuration. RegisterCompilationStartAction runs once at the start of each compilation and is where the marker attribute's symbol gets resolved exactly once; registering AnalyzeType inside that callback, scoped to RegisterSymbolAction, means the per-type check only ever runs against a compilation where the marker attribute was actually found. AnalyzeType itself walks the members of a flagged type looking for a decimal-typed property and reports the diagnostic at that property's own location, so an editor underlines the specific line that needs to change.");
}

patternSlide(
  "STATIC ABSTRACT & GENERIC MATH",
  "Reach a type-level operation through a type parameter",
  [
    "IRateable<TSelf>.BaseRate and RateOf are static abstract members; the self-constraint where TSelf : IRateable<TSelf> is what lets RateBook.Quote<T> call T.RateOf with no instance to dispatch on.",
    "Money implements five System.Numerics interfaces, IAdditionOperators, IAdditiveIdentity, IMultiplyOperators, IComparisonOperators, and IComparable<Money>, so it behaves like a built-in numeric type to any generic algorithm.",
    "Totals.Sum<T> is written once against IAdditionOperators<T,T,T> and IAdditiveIdentity<T,T>; it runs unchanged over a sequence of Money values and over a sequence of plain decimal values.",
  ],
  "29-irateable-static-dispatch",
  "RateBook.Quote<T> dispatches to T.RateOf through the type parameter alone; Totals.Sum<T> folds from T.AdditiveIdentity instead of a literal zero.",
  "Static abstract interface members let a generic method call a static member through its type parameter, something no earlier version of the language could express: RateBook.Quote<T> calls T.RateOf(shipment) with T constrained to IRateable<T>, and the compiler resolves that call per concrete T at the call site with no reflection and no virtual dispatch table involved. Money in this chapter carries a single Amount field by design, built purely to satisfy the generic math interfaces, which is why it implements IAdditionOperators, IAdditiveIdentity, IMultiplyOperators, IComparisonOperators, and IComparable<Money> rather than carrying a currency code the way an everyday monetary type would. Totals.Sum<T> folding from T.AdditiveIdentity instead of a literal zero is what lets one method body run unchanged over a list of Money values and a list of plain decimal values, because both types satisfy the same constraint."
);

{
  const s = S();
  addCodeSlide(s, "STATIC ABSTRACT & GENERIC MATH", "IRateable<TSelf> and generic math over Money", "C#", [
    "public interface IRateable<TSelf> where TSelf : IRateable<TSelf>",
    "{",
    "    static abstract Money BaseRate { get; }",
    "    static abstract Money RateOf(TSelf shipment);",
    "}",
    "",
    "public static class RateBook",
    "{",
    "    public static Money Quote<T>(T shipment) where T : IRateable<T> =>",
    "        T.RateOf(shipment);",
    "}",
    "",
    "public readonly record struct Money(decimal Amount) :",
    "    IAdditionOperators<Money, Money, Money>,",
    "    IAdditiveIdentity<Money, Money>,",
    "    IMultiplyOperators<Money, decimal, Money>,",
    "    IComparisonOperators<Money, Money, bool>,",
    "    IComparable<Money>",
    "{",
    "    public static Money AdditiveIdentity => new(0m);",
    "    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount);",
    "}",
  ], "This Money is a single-field readonly record struct built purely to satisfy the generic math interfaces; it carries no currency field.");
  addNotes(s, "The self-constraint on IRateable<TSelf> is what makes RateOf callable through a bare type parameter: it tells the compiler that any T satisfying IRateable<T> also supplies a static RateOf(T) member, so Quote<T> can call T.RateOf(shipment) with the JIT specializing the call per concrete value-type T rather than paying a virtual-dispatch cost. Money's interface list is what lets it plug into any algorithm written against the BCL's generic math interfaces without that algorithm knowing Money exists; AdditiveIdentity in particular gives a generic summation a correct starting point to fold from instead of an arbitrary literal zero that might not even be the right type. This Money is deliberately minimal, one Amount field with no currency, which keeps the generic math interfaces themselves the entire point of the example rather than mixing in the validation and currency concerns a production Money type would carry.");
}

{
  const s = S();
  addDiagramSlide(s, "STATIC ABSTRACT & GENERIC MATH", "One summation, two numeric types", "29-generic-math-over-money",
    "Totals.Sum<T> runs over a list of Money values and over a list of plain decimal values with the exact same method body.");
  addNotes(s, "Totals.Sum<T> is constrained only to IAdditionOperators<T, T, T> and IAdditiveIdentity<T, T>, both of which Money implements and both of which every built-in numeric type in the base class library already implements as well. Running the same Sum<T> call once over a list of Money values and once over a list of plain decimal values exercises two entirely different concrete types through one unchanged method body, proving the algorithm was never specific to this book's own domain types. That reuse is the practical payoff of generic math: a billing routine written once against the interfaces works on a custom monetary type today and on any future numeric type that satisfies the same constraint, with no overload and no special case added for either.");
}

// ===========================================================================
// Closing
// ===========================================================================
{
  const s = S();
  addContentTitle(s, "CLOSING", "The idiomatic C# 14 / .NET 10 toolkit, in one pass");
  addTwoColBullets(s, [
    "Substrate: readonly record struct for Money and Weight, a sealed record Consignment built on required, init, and with, IDisposable and IAsyncDisposable for deterministic cleanup.",
    "Sequences and async: yield return state machines, IAsyncEnumerable<T> with await foreach, and a TAP surface where every Async method honors its CancellationToken.",
  ], [
    "Typed outcomes: Try-Parse, Tester-Doer, and Result<T> for expected failure; the options pattern for configuration validated once, at startup.",
    "C# as data: extension members on an unowned type, expression trees read and rewritten by an ExpressionVisitor, source generators and analyzers running inside the compiler, static abstract members reaching a type through its own type parameter.",
  ], { fontSize: 17 });
  addNotes(s, "Four groups, one underlying habit: state the shape and the lifetime of a value in the type itself, and let the compiler carry the weight a developer used to carry by hand. The substrate group is about what a value is and when it goes away; the sequences and async group is about two kinds of suspendable method the compiler turns into state machines; the typed outcomes group is about making an expected failure or a bad configuration visible in a signature rather than discovered at runtime; the C# as data group is about treating a type, an expression, or a design rule as something another piece of code, running inside the compiler itself, can read or produce. None of these are exotic; every example in this deck lives in a shipping company's own consignments, carriers, and tariffs.");
}

refTable(
  "REFERENCE",
  "The idiom and what C# 14 / .NET 10 enables",
  [
    { code: "Extension members", name: "Idiom", purpose: "Members on a type you do not own, instance and static, plus an operator, without a this-parameter method for every shape." },
    { code: "Expression trees", name: "Idiom", purpose: "A computation as inspectable data: read it, rewrite it, or compose it before a single Compile() runs it." },
    { code: "Source generators", name: "Idiom", purpose: "A build-time registry that survives trimming and ahead-of-time compilation, with no reflection scan at startup." },
    { code: "Analyzers", name: "Idiom", purpose: "A design rule enforced as a build diagnostic, promotable to a build failure with TreatWarningsAsErrors." },
    { code: "Static abstract / generic math", name: "Idiom", purpose: "One algorithm written once, running unchanged over a custom type and over a built-in numeric type." },
    { code: "Records + NRT + required", name: "Idiom", purpose: "Recursive value equality, a required property, and a nullable string? retire the Null-Object reflex for a genuinely absent value." },
  ],
  "A quick-reference scan from idiom to what it buys. Each row names a C# 14 or .NET 10 feature covered in this deck and states the capability it hands a developer directly, in the positive, with no comparison to any other language or era beyond the temporal one this whole book measures against. Keep this slide open as a checklist when deciding whether a new piece of code reaches for one of these idioms or stays with a simpler shape."
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

  addNotes(s, "This is the Design Patterns with C# deck, this is the 201 idiomatic C# deck. The two codes take it with you: on the left, the site, the chapters, the runnable C# examples, and the diagrams. On the right, the Workshops and Tutorials hub, every workshop, tutorial, and deck in the series in one place. Both are public on the Pattern Catalyst GitHub organization.");
}

// ===========================================================================
pres.writeFile({ fileName: OUT })
  .then(p => console.log("WROTE", p, "slides:", pageNum))
  .catch(e => { console.error(e); process.exit(1); });
