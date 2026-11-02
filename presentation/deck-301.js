// deck-301.js - "Design Patterns with C#" - 301: Testing, Legacy, and AI
// Red Hat-branded 16:9 deck. Build:  node deck-301.js  (from presentation/)

"use strict";

const H = require("./deck-helpers.js");
const {
  COLOR, FONT, W, ASSETS, PNG,
  newDeck, addFooter, addContentTitle, addBullets, addTwoColBullets,
  addStatusTable, addCaption, addSectionDivider, addNotes,
  addCodeSlide, addDiagramSlide,
} = H;

const OUT = "csharp-design-patterns-301-testing-legacy-and-ai.pptx";

const pres = newDeck();
pres.title = "Design Patterns with C# - 301: Testing, Legacy, and AI";
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
  s.addText("301: Testing, Legacy, and AI", {
    x: 6.00, y: 4.65, w: 6.70, h: 0.90,
    fontFace: FONT.body, fontSize: 18, italic: true, color: COLOR.caption,
    align: "left", valign: "top",
  });
  try { s.addImage({ path: `${ASSETS}/logo-candidate-2.png`, x: 11.10, y: 6.80, w: 1.55, h: 0.37 }); } catch (e) {}
  addNotes(s, "Design Patterns with C#, the 301 deck: testing, legacy, and AI. Where the idiomatic C# deck asked what a rate calculator looks like written the modern way, this deck asks three practical questions in sequence: how a team proves a shipping engine behaves correctly with xUnit and NSubstitute, how that same team changes an inherited engine that was never built with a test in mind, and how an engine built partly with AI assistance holds up once a design pattern shows up in the output. Every example lives in the same shipping domain as the rest of the series: consignments, carriers, rate quotes, tariffs, and the engines that price them.");
}

// ===========================================================================
// Agenda
// ===========================================================================
{
  const s = S();
  addContentTitle(s, "AGENDA", "What is ahead");
  addTwoColBullets(s, [
    "Proving it works: a four-phase xUnit test over NSubstitute doubles standing in for a carrier rate lookup, a tax policy, and an audit sink.",
    "Changing inherited code: a sealed, dependency-hiding rate engine opened one seam at a time, then reshaped from Feathers's minimal seam into Fowler's cleaner structure, then weighed for a refactor against a clean rewrite.",
  ], [
    "Working with AI and LLM tools: a Roslyn analyzer, SHIP0002, that flags a Strategy interface an assistant reached for before a second shape actually earned it.",
    "A closing reference: the test-double taxonomy, Dummy through Fake, mapped onto the xUnit and NSubstitute calls used throughout this deck.",
  ], { fontSize: 17 });
  addNotes(s, "Four stops, one underlying question: how does a team know a shipping engine behaves correctly, and how does that confidence survive both an inherited codebase and an AI-assisted one. The testing section establishes the vocabulary, Arrange-Act-Assert, stub, and verify, that every later section leans on. The legacy section is the longest because changing code safely without an existing test is the harder and more common problem than writing a test against new code. The AI section closes the deck because the same seam-and-verify discipline from the first two sections is exactly what checks an assistant's output against reality rather than taking it on its plausibility alone.");
}

// ===========================================================================
// Section 01: Proving it works
// ===========================================================================
divider("01", "Proving it works", "xUnit for the suite, NSubstitute for the stand-ins",
  "This section builds the testing vocabulary every section after it assumes. ShipmentRatingEngine quotes a consignment by combining a carrier's base rate, a tax policy, and an audit sink, three collaborators that arrive through its constructor, so a test substitutes exactly the ones it cares about. We walk the canonical four-phase fact, parametrized theories including the decimal-versus-double gotcha a typed TheoryData source avoids, the difference between stubbing a return value on Arrange and verifying a call on Assert, and the asynchronous idiom where an await belongs in front of Received just as much as in front of the call being tested. The section closes on NSubstitute against Moq, a default for new work, never a mandate to migrate a working suite.");

{
  const s = S();
  addDiagramSlide(s, "TESTING WITH XUNIT", "A four-phase test over three substitutes", "10-testing-with-xunit-and-nsubstitute",
    "ShipmentRatingEngine takes ICarrierRates, ITaxPolicy, and IAuditSink through its constructor; each phase of the test touches a different collaborator.");
  addNotes(s, "ShipmentRatingEngine is a sealed class whose three collaborators, ICarrierRates, ITaxPolicy, and IAuditSink, all arrive through constructor injection, the same seam this deck's legacy section spends an entire section teaching a team to open from scratch. QuoteAsync awaits the carrier's base rate, applies tax, records an audit entry, and returns the taxed quote, so a single method exercises all three collaborators in sequence. Arrange builds three Substitute.For<T>() doubles and stubs the two whose return values the method consumes; Act calls QuoteAsync; Assert checks the returned Money value. Nothing about this shape is specific to shipping: the same four-phase structure applies to any method whose behavior depends on collaborators a test can swap out.");
}

{
  const s = S();
  addCodeSlide(s, "TESTING WITH XUNIT", "Arrange, Act, Assert in one fact", "C#", [
    "[Fact]",
    "public async Task Quotes_the_base_rate_with_tax_applied()",
    "{",
    "    // Arrange",
    "    var carrierRates = Substitute.For<ICarrierRates>();",
    "    carrierRates.GetBaseRateAsync(\"Rotterdam\", 820m, Arg.Any<CancellationToken>())",
    "        .Returns(new Money(100.00m));",
    "    var taxPolicy = Substitute.For<ITaxPolicy>();",
    "    taxPolicy.ApplyTax(new Money(100.00m)).Returns(new Money(110.00m));",
    "    var auditSink = Substitute.For<IAuditSink>();",
    "    var engine = new ShipmentRatingEngine(carrierRates, taxPolicy, auditSink);",
    "    var consignment = new Consignment(\"C-0001\", \"Rotterdam\", 820m);",
    "",
    "    // Act",
    "    var quote = await engine.QuoteAsync(consignment, TestContext.Current.CancellationToken);",
    "",
    "    // Assert",
    "    Assert.Equal(new Money(110.00m), quote);",
    "}",
  ], "TestContext.Current.CancellationToken supplies a real token instead of CancellationToken.None, the token a running test can actually cancel.");
  addNotes(s, "Substitute.For<ICarrierRates>() builds a dynamic double directly from the interface, with no base class and no recorded expectation set up ahead of time the way an older mocking style required. Arg.Any<CancellationToken>() in the stub setup matters because the test cannot predict the exact token instance QuoteAsync will pass, only that some token will arrive; the Act line itself passes TestContext.Current.CancellationToken, the token xUnit ties to the running test, rather than CancellationToken.None, so a timed-out or cancelled test run actually propagates. Assert.Equal on the returned Money closes the loop: the test proves the taxed amount the engine returned, not merely that the method ran without throwing. Every other test in the testing section builds on this exact four-phase shape.");
}

{
  const s = S();
  addCodeSlide(s, "TESTING WITH XUNIT", "Theory, InlineData, and a typed TheoryData source", "C#", [
    "[Theory]",
    "[InlineData(\"Rotterdam\", 100.0)]",
    "[InlineData(\"Santos\", 250.0)]",
    "[InlineData(\"Busan\", 900.0)]",
    "public async Task Passes_the_destination_and_weight_through_to_the_carrier_rate_lookup(",
    "    string destination, decimal kilograms)",
    "{",
    "    var carrierRates = Substitute.For<ICarrierRates>();",
    "    carrierRates.GetBaseRateAsync(destination, kilograms, Arg.Any<CancellationToken>())",
    "        .Returns(new Money(50.00m));",
    "    // ... stub the tax policy, build the engine, Act, then verify below",
    "}",
    "",
    "// A typed source keeps each case named and strongly typed; xUnit1042 flags",
    "// a bare IEnumerable<object[]> as an untyped member data source.",
    "public static TheoryData<decimal, decimal> WeightTierBaseRates() => new()",
    "{",
    "    { 10m, 25.00m },",
    "    { 500m, 625.00m },",
    "    { 2000m, 2400.00m },",
    "};",
  ], "InlineData's 100.0 literal is a C# double; the decimal kilograms parameter widens it on every call.", { fontSize: 9, y: 1.72, h: 4.95 });
  addNotes(s, "InlineData accepts object arguments, so a literal like 100.0 is compiled as a double even though the test method's parameter is declared decimal; the runtime conversion from double to decimal happens silently and correctly here, but the same pattern with a value that does not round-trip cleanly through binary floating point is a real source of flaky theories, which is why a chapter on this idiom calls it out by name rather than leaving it implicit. WeightTierBaseRates returns TheoryData<decimal, decimal> instead of a bare IEnumerable<object[]>, keeping every case strongly typed from declaration through to the test method's parameters; the analyzer rule xUnit1042 specifically flags the untyped array form, nudging a team toward the typed source shown here. Three weight tiers and their expected carrier base rates read as named cases rather than as positional magic numbers in an array literal.");
}

{
  const s = S();
  addDiagramSlide(s, "TESTING WITH XUNIT", "Stub on Arrange, verify on Assert", "10-nsubstitute-stub-vs-verify",
    "Arg.Any<T>() accepts any argument when stubbing a return value; Arg.Is<T>(predicate) narrows a later verification to the exact call that matters.");
  addNotes(s, "Stubbing and verifying both use the same Arg matchers, but at opposite ends of a test: Arg.Any<T>() on Arrange says a call with any argument of that type should return the stubbed value, while the same Arg.Any<T>() on an Assert-side Received call says a test does not care what argument arrived, only that some call happened. Arg.Is<T>(predicate) narrows either side to a specific shape: on Arrange it stubs a return value only for arguments matching the predicate, and on Assert it verifies a call happened with an argument matching that predicate specifically. Reaching for Arg.Is instead of a literal argument value matters most on the Assert side, where a test wants to check a property of what was recorded, such as a message containing a particular reference, without pinning down every other detail of the call.");
}

{
  const s = S();
  addCodeSlide(s, "TESTING WITH XUNIT", "Received, DidNotReceive, and the Arg matchers", "C#", [
    "private static ShipmentRatingEngine BuildEngine(",
    "    out IAuditSink auditSink, Money baseRate, Money taxedQuote)",
    "{",
    "    var carrierRates = Substitute.For<ICarrierRates>();",
    "    carrierRates.GetBaseRateAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())",
    "        .Returns(baseRate);",
    "    var taxPolicy = Substitute.For<ITaxPolicy>();",
    "    taxPolicy.ApplyTax(baseRate).Returns(taxedQuote);",
    "    auditSink = Substitute.For<IAuditSink>();",
    "    return new ShipmentRatingEngine(carrierRates, taxPolicy, auditSink);",
    "}",
    "",
    "[Fact]",
    "public async Task Never_records_any_quote_other_than_the_one_it_returned()",
    "{",
    "    var engine = BuildEngine(out var auditSink, new Money(100.00m), new Money(110.00m));",
    "    var consignment = new Consignment(\"C-1002\", \"Santos\", 50m);",
    "",
    "    await engine.QuoteAsync(consignment, TestContext.Current.CancellationToken);",
    "",
    "    await auditSink.DidNotReceive().RecordAsync(",
    "        Arg.Any<string>(), Arg.Is<Money>(quoted => quoted != new Money(110.00m)), Arg.Any<CancellationToken>());",
    "}",
  ], "DidNotReceive with an Arg.Is predicate proves no other quote amount was ever recorded, not just that one call happened.", { fontSize: 9, y: 1.72, h: 4.95 });
  addNotes(s, "BuildEngine centralizes the three-substitute setup every verification test in this class shares, returning the audit sink through an out parameter so each test keeps its own reference to assert against. DidNotReceive is the negative counterpart to Received: rather than proving a call happened, it proves a call matching the given arguments never happened across the whole test run. The Arg.Is<Money> predicate here checks every recorded quote against the one correct taxed amount, so the assertion is stronger than checking RecordAsync was called exactly once; it rules out a second, incorrect call sneaking in alongside a correct one. This shape generalizes beyond auditing: anywhere a collaborator must receive exactly the right data and nothing else, DidNotReceive paired with a negated predicate closes the gap a bare Received(1) leaves open.");
}

{
  const s = S();
  addCodeSlide(s, "TESTING WITH XUNIT", "Await Received, and thread the caller's own token", "C#", [
    "[Fact]",
    "public async Task Verifying_an_awaited_call_needs_an_await_in_front_of_received()",
    "{",
    "    var carrierRates = Substitute.For<ICarrierRates>();",
    "    carrierRates.GetBaseRateAsync(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())",
    "        .Returns(new Money(40.00m));",
    "    var engine = new ShipmentRatingEngine(carrierRates, taxPolicy, Substitute.For<IAuditSink>());",
    "",
    "    await engine.QuoteAsync(consignment, TestContext.Current.CancellationToken);",
    "",
    "    await carrierRates.Received(1).GetBaseRateAsync(\"Busan\", 640m, Arg.Any<CancellationToken>());",
    "}",
    "",
    "[Fact]",
    "public async Task Passes_the_callers_cancellation_token_through_every_await()",
    "{",
    "    using var cancellationSource = new CancellationTokenSource();",
    "",
    "    await engine.QuoteAsync(consignment, cancellationSource.Token);",
    "",
    "    await carrierRates.Received(1).GetBaseRateAsync(\"Felixstowe\", 12m, cancellationSource.Token);",
    "    await auditSink.Received(1).RecordAsync(\"C-2002\", new Money(15.00m), cancellationSource.Token);",
    "}",
  ], "xUnit1051 flags a test that ignores an available cancellation token; both tests verify the caller's exact token reached both collaborators.", { fontSize: 9, y: 1.72, h: 4.95 });
  addNotes(s, "An async member on an NSubstitute double returns an already-completed task wrapping whatever .Returns stubbed, so awaiting a stubbed call behaves exactly like awaiting the real implementation would; verifying that same call needs the same await, because Received(1) on an async member itself returns a task that must complete before the assertion is meaningful. The second test builds a real CancellationTokenSource rather than relying on a default token, then asserts both carrierRates and auditSink received that exact token instance, proving the token flows through QuoteAsync's own internal awaits unchanged rather than being silently replaced with CancellationToken.None somewhere inside the method. The analyzer rule xUnit1051 exists for exactly this reason: a test method that accepts or has access to a real cancellation token but never threads it through its awaits is a test that cannot actually be cancelled when a test run is cut short.");
}

{
  const s = S();
  addContentTitle(s, "TESTING WITH XUNIT", "NSubstitute vs Moq: a default, not a mandate");
  addBullets(s, [
    "Substitute.For<T>() builds a fake directly from the interface; Moq's equivalent wraps the fake inside Mock<T>() and exposes it to the rest of a test only through a .Object property.",
    "NSubstitute's .Returns and .Received read as calls on the faked type itself, so a consignment-rating test reads like it is talking to ICarrierRates, not to a mock wrapper around one.",
    "This book defaults to NSubstitute for every new example; a chapter never tells a team running a working Moq suite to migrate it over for its own sake.",
  ], { x: 0.62, y: 1.85, w: 12.09, h: 4.55, fontSize: 17 });
  addNotes(s, "The practical difference between the two libraries is mostly where the indirection lives: NSubstitute's Substitute.For<T>() is itself the object a test passes around and asserts against, while Moq's Mock<T>() is a wrapper a test has to unwrap through .Object before handing the fake to the code under test, and has to go back to the Mock instance for .Setup and .Verify calls. Both libraries solve the same problem, creating a dynamic double of an interface and configuring its behavior without a hand-written stub class, and neither shape is wrong on its own merits. This series picks NSubstitute as its default because its calls read closest to calls on the real interface, which keeps a test readable without a reader needing to track two different objects for one collaborator; a team with an existing, working Moq suite has no reason to spend effort rewriting it just to match this convention.");
}

// ===========================================================================
// Section 02: Changing inherited code
// ===========================================================================
divider("02", "Changing Inherited Code", "Seams, the dependency-breaking catalogue, Fowler and Feathers, the refactor-or-rewrite call",
  "C# runs the opposite default from an object system where every method stays open to override: a method is non-virtual and a class can be sealed until its author chooses otherwise, which means a class built without a seam in mind leaves a later test nothing to substitute. This section opens a sealed, dependency-hiding rate engine one seam at a time, extracting an interface for a tariff lookup, wrapping an ambient clock, exposing an extract-and-override hook, and reaching an internal validation rule through InternalsVisibleTo. It then widens to five baked-in dependencies at once in a second engine, walks the same engine through Feathers's minimal seam and Fowler's full reshaping while an equivalence test checks the suite stays green across the move, and closes on four concrete criteria for choosing a refactor over a clean rewrite rather than deciding by instinct.");

{
  const s = S();
  addDiagramSlide(s, "SEAMS", "C# is non-virtual by default", "41-seams-non-virtual-by-default",
    "A sealed RateEngine with its dependencies built inside offers no way in; a constructor and a protected override open a seam for a test to use.");
  addNotes(s, "A method in C# stays non-virtual until its author writes virtual, and a class can close inheritance entirely with sealed, the opposite default from an object system where overriding a method nobody wrote for testing hands a caller a seam automatically. That default carries a real benefit: a sealed class with non-virtual methods guarantees a reader and the compiler that nothing outside the class changes its behavior through inheritance its author never anticipated. The cost shows up exactly where the legacy RateEngine sits: a class built without a seam in mind gives a later test nothing to substitute, and no amount of inheritance can manufacture a seam its author never left behind. Opening a constructor parameter and a protected override is what repairs that, without weakening the guarantee for every member that does not need it.");
}

{
  const s = S();
  addCodeSlide(s, "SEAMS", "The legacy shape: no seam for either collaborator", "C#", [
    "public sealed class RateEngine",
    "{",
    "    public decimal Quote(RateRequest request)",
    "    {",
    "        ArgumentNullException.ThrowIfNull(request);",
    "",
    "        var tariff = new LegacyTariffTable();",
    "        var subtotal = request.DeclaredValue * 0.015m;",
    "        subtotal += request.WeightKilograms * tariff.RatePerKilogram(\"STD\");",
    "",
    "        if (DateTime.Now.Hour is >= 8 and < 18)",
    "        {",
    "            subtotal *= 1.1m;",
    "        }",
    "",
    "        return subtotal;",
    "    }",
    "}",
    "",
    "// Reflection proof: IsSealed is true, GetConstructors() returns exactly one",
    "// parameterless constructor, and the Quote MethodInfo reports IsVirtual as false.",
  ], "A hard-coded new LegacyTariffTable() and the ambient DateTime.Now leave nothing here for a test to substitute.", { fontSize: 10, y: 1.72, h: 4.95 });
  addNotes(s, "This RateEngine closes off every seam a test could use at once: it is sealed, so no subclass can override anything; its tariff table is a hard-coded new inside the method rather than a constructor parameter; and its peak-hour check reads the ambient wall clock through DateTime.Now rather than an injected notion of now. A test that wanted to prove this engine untestable without itself becoming flaky cannot assert against DateTime.Now directly, since the result would depend on when the suite happens to run; instead, a reflection-based test asserts on the type's structure, IsSealed, a single parameterless constructor, and a non-virtual Quote method, which together rule out a constructor seam, an interface seam, and an override seam in one assertion block. Every fact that reflection test checks is a structural property of the class, not a runtime outcome, which is what makes the proof of untestability itself reliable.");
}

{
  const s = S();
  addDiagramSlide(s, "SEAMS", "Four seams, one engine", "41-c-sharp-seam-kinds",
    "Constructor injection, wrapping an ambient clock, a protected override hook, and an internal entry point each open a different kind of seam in the same engine.");
  addNotes(s, "Four seams sit inside the refactored RateEngine and each answers a different question about where a dependency comes from. The constructor seam over ITariffSource answers which object computes the per-kilogram rate, replacing the hard-coded new. The interface seam over IClock answers which instant counts as now, the same move applied to a value read from a static call rather than built with new; the base class library's own TimeProvider abstraction solves the identical problem for a project with broader scheduling needs than a single rate quote. The extract-and-override seam over PeakSurcharge answers which rule decides the peak-hour outcome without reopening the rest of Quote to a subclass, and the internal seam over PeakWindow.IsPeak, reached through an InternalsVisibleTo grant to the test assembly, answers who else besides this engine needs to see that rule at all.");
}

{
  const s = S();
  addCodeSlide(s, "SEAMS", "Constructor injection plus an extract-and-override seam", "C#", [
    "public class RateEngine(ITariffSource tariffs, IClock clock)",
    "{",
    "    public RateQuote Quote(RateRequest request)",
    "    {",
    "        ArgumentNullException.ThrowIfNull(request);",
    "",
    "        var subtotal = request.DeclaredValue * 0.015m;",
    "        subtotal += request.WeightKilograms * tariffs.RatePerKilogram(\"STD\");",
    "",
    "        var at = clock.UtcNow;",
    "        var total = subtotal + PeakSurcharge(subtotal, at);",
    "",
    "        return new RateQuote(request.ConsignmentId, total, at);",
    "    }",
    "",
    "    protected virtual decimal PeakSurcharge(decimal subtotal, DateTimeOffset at) =>",
    "        PeakWindow.IsPeak(at) ? subtotal * 0.1m : 0m;",
    "}",
  ], "Not sealed: a test subclass can override PeakSurcharge directly, the extract-and-override seam alongside constructor injection.");
  addNotes(s, "RateEngine is no longer sealed and its two collaborators, ITariffSource and IClock, arrive through a primary constructor rather than being built or read from inside the method; a production caller passes a StandardTariffSource and a SystemClock, while a test passes an NSubstitute substitute for the tariff source and a FakeClock fixed at a known instant. PeakSurcharge is marked protected virtual specifically so a test subclass can replace the entire peak-hour rule without touching Quote's own logic, the extract-and-override seam working alongside the two constructor seams rather than in place of them. PeakWindow.IsPeak stays internal rather than public, reachable from the test assembly only through an InternalsVisibleTo grant in the project file, so the peak-hour boundary itself can be pinned by a direct theory test without widening the engine's public surface for every other consumer.");
}

{
  const s = S();
  addDiagramSlide(s, "DEPENDENCY-BREAKING", "The dependency-breaking catalogue in C#", "42-feathers-catalogue-in-c-sharp",
    "Extract Interface, Parameterize Constructor, Parameterize Method, Extract-and-Override, and InternalsVisibleTo each answer one dependency the legacy engine baked in.");
  addNotes(s, "The legacy RateEngine in this part of the deck bakes in five dependencies at once inside one method: a newed-up LegacyAuditLog, the static LegacyTariffBook.RateFor lookup, Guid.NewGuid for a reference number, DateTime.UtcNow for a timestamp, and Environment.UserName for the caller's identity, none of them reachable from outside the method. Five dependency-breaking techniques answer the five, each one a named move from Feathers's catalogue adapted to C# idiom: Extract Interface for the tariff book and the audit log, Parameterize Constructor to move all four collaborators to the constructor, Parameterize Method to replace the ambient Environment.UserName with an explicit parameter, Extract-and-Override for the reference generator, and InternalsVisibleTo to reach an internal validation rule from the test project. None of these techniques redesigns the method's logic; each one only relocates where a value comes from.");
}

refTable(
  "DEPENDENCY-BREAKING",
  "Five techniques, five seams in one engine",
  [
    { code: "Extract Interface", name: "Technique", purpose: "ITariffBook and IAuditLog replace a static rate lookup and a self-constructed audit log the legacy engine built for itself." },
    { code: "Parameterize Constructor", name: "Technique", purpose: "Tariffs, audit, clock, and references all arrive through RateEngine's constructor instead of each being built inline, one new per dependency." },
    { code: "Parameterize Method", name: "Technique", purpose: "quotedBy arrives as a Quote parameter in place of the ambient Environment.UserName, so a test controls the caller's identity directly." },
    { code: "Extract-and-Override", name: "Technique", purpose: "A protected virtual NewReference() lets a subclass replace reference generation, the seam this engine exposes alongside its four constructor seams." },
    { code: "InternalsVisibleTo", name: "Technique", purpose: "LaneValidation.IsKnownLane stays internal; the test project reaches it directly without widening RateEngine's public surface for production callers." },
  ],
  "Five rows, five dependencies, one engine. Extract Interface and Parameterize Constructor do the bulk of the work, moving four of the five baked-in dependencies, the tariff book, the audit log, the clock, and the reference generator, onto the constructor behind a named interface each. Parameterize Method handles the one dependency that was never an object to begin with, the caller's own identity, by turning an ambient read into an explicit argument. Extract-and-Override and InternalsVisibleTo round out the catalogue for two dependencies that call for a seam narrower than a full constructor parameter: a reference generator a subclass can replace directly, and a validation rule a test needs to reach without making it part of the library's public contract."
);

{
  const s = S();
  addCodeSlide(s, "DEPENDENCY-BREAKING", "Four fakes expose the audit message a test could not otherwise see", "C#", [
    "private static RateEngine BuildEngine(",
    "    out ITariffBook tariffs, out IAuditLog audit,",
    "    out IClock clock, out IReferenceGenerator references)",
    "{",
    "    tariffs = Substitute.For<ITariffBook>();",
    "    audit = Substitute.For<IAuditLog>();",
    "    clock = Substitute.For<IClock>();",
    "    references = Substitute.For<IReferenceGenerator>();",
    "    return new RateEngine(tariffs, audit, clock, references);",
    "}",
    "",
    "[Fact]",
    "public void Quote_records_an_audit_entry_for_the_quote()",
    "{",
    "    var engine = BuildEngine(out var tariffs, out var audit, out var clock, out var references);",
    "    tariffs.RateFor(\"SIN-LAX\").Returns(5.10m);",
    "    clock.UtcNow.Returns(DateTimeOffset.UnixEpoch);",
    "    references.NewReference().Returns(\"RQ-0002\");",
    "    var request = new RateRequest(\"C-9002\", 5_000m, 40m, \"SIN-LAX\");",
    "",
    "    engine.Quote(request, \"ops.lead\");",
    "",
    "    audit.Received(1).Record(Arg.Is<string>(message =>",
    "        message.Contains(\"RQ-0002\", StringComparison.Ordinal) &&",
    "        message.Contains(\"C-9002\", StringComparison.Ordinal)));",
    "}",
  ], "All four seams, ITariffBook, IAuditLog, IClock, and IReferenceGenerator, are fakes here; the Arg.Is predicate checks the audit message's exact text.", { fontSize: 9, y: 1.72, h: 4.95 });
  addNotes(s, "BuildEngine wires four separate Substitute.For<T>() fakes into one RateEngine, one for each dependency the legacy shape baked in, and hands all four back through out parameters so a test can configure and assert against exactly the ones it needs. The audit log was never reachable from outside the legacy method at all; here it is a fifth fake whose single Record call becomes directly verifiable. The Arg.Is<string> predicate checks that the recorded message contains both the generated reference and the consignment identifier, a stronger assertion than checking a message was recorded at all, since it proves the right values flowed into the audit trail rather than merely that something did. This same four-fake pattern repeats, with different stubbed values, across every test in the suite that exercises a different one of the four seams.");
}

{
  const s = S();
  addDiagramSlide(s, "FOWLER VS FEATHERS", "Feathers first, then Fowler", "43-feathers-then-fowler",
    "SeamedRateEngine opens the minimal seam a test needs; RateEngine then reshapes the method's structure on top of that same seam.");
  addNotes(s, "Feathers and Fowler answer two different questions about the same inherited method, and this part of the deck keeps them visibly separate rather than collapsing them into one step. The Feathers question is the narrower one: what is the smallest change that lets a test substitute every collaborator, with everything else about the method's shape left exactly as it was. The Fowler question comes after a test exists to protect the change: what is this method actually doing, and what does its real shape look like once an inline conditional becomes a named strategy and a loose parameter list becomes a single typed request. Doing the second without the first first means refactoring with no safety net; doing the first without ever reaching for the second leaves a method that compiles and tests clean but still reads like the legacy shape it came from.");
}

{
  const s = S();
  addContentTitle(s, "FOWLER VS FEATHERS", "One engine, three forms");
  addBullets(s, [
    "LegacyRateEngine: sealed, a hard-coded new LegacyTariffTable(), the ambient DateTime.Now, and an inline string switch over \"EXPRESS\" and \"OVERNIGHT\".",
    "SeamedRateEngine, the Feathers stage: only the two collaborators move to the constructor as ITariffSource and IClock; the loose (decimal, decimal, string) signature, the inline switch, and the rate literals stay exactly as they were, since changing them is not this stage's job.",
    "RateEngine, the Fowler stage: Extract Method splits BaseCharge and WeightCharge, Replace Conditional with Polymorphism swaps the switch for IServiceSurcharge and ServiceSurcharges.For, Introduce Parameter Object folds three loose parameters into RateRequest, and Replace Magic Literal names the constants in RateRules.",
  ], { x: 0.62, y: 1.85, w: 12.09, h: 4.55, fontSize: 16 });
  addNotes(s, "SeamedRateEngine changes only what it must: the two lines that made the legacy engine untestable, the hard-coded tariff table and the ambient clock, and leaves the loose (decimal, decimal, string) parameter list and the inline string switch for the service surcharge exactly as they were, because the only goal at this stage is a passing test, not a better-looking method. RateEngine, built on top of that same seam, is where the real refactoring happens: BaseCharge and WeightCharge pull two separate calculations out of one long method, IServiceSurcharge and the ServiceSurcharges.For factory replace the inline switch with a small polymorphic hierarchy, RateRequest replaces three positional parameters with one named type that also carries a typed ServiceLevel, and RateRules names the ad-valorem rate, the peak multiplier, and the standard lane code that used to be unnamed literals scattered through the method. Nothing about the quoted amount changes between the two stages; only the method's own structure does.");
}

{
  const s = S();
  addDiagramSlide(s, "FOWLER VS FEATHERS", "Behaviour preserved across the refactor", "43-behaviour-preserved",
    "EquivalenceTests.Both_catalogues_produce_the_same_quote checks SeamedRateEngine and RateEngine against identical input; the standard, off-peak case lands at 495.00 either way.");
  addNotes(s, "EquivalenceTests.Both_catalogues_produce_the_same_quote drives SeamedRateEngine and RateEngine with the same StandardTariffSource, the same FakeClock instant, and the same declared value and weight, across every ServiceLevel and both inside and outside the peak window, asserting the two amounts match exactly in every case. For a standard-service request quoted outside the peak window, both engines land on the identical figure, 495.00, proving the Extract Method, Replace Conditional with Polymorphism, Introduce Parameter Object, and Replace Magic Literal moves changed the method's shape without changing a single number a real consignment would be quoted. That equivalence check is what turns the Fowler-stage refactor from a hopeful rewrite into a verified one: the test suite that passed against SeamedRateEngine passes against RateEngine for the same reason, because the two compute the same thing.");
}

{
  const s = S();
  addDiagramSlide(s, "REFACTOR OR REWRITE", "Refactor or rewrite: four criteria, not a reflex", "44-decision-criteria",
    "Existing test coverage, how often the code changes, the blast radius of a mistake, and how settled the domain rules are, decide the call together.");
  addNotes(s, "Choosing between refactoring an inherited engine in place and replacing it with a clean rewrite is a decision with real consequences in both directions, and this part of the deck names four concrete criteria instead of leaving the choice to instinct or to whichever approach feels more satisfying. Existing test coverage matters because a refactor without a safety net is a rewrite with extra steps and worse guarantees; a rewrite can earn its own fresh coverage, but a refactor depends on tests that already exist. How often the code changes matters because a rarely touched engine rarely justifies the risk of a rewrite, while one under constant active development accumulates refactoring friction a rewrite can shed in one pass. Blast radius asks what breaks if the replacement is subtly wrong, and how settled the domain rules are asks whether the business logic itself is still being discovered or is stable enough to reimplement with confidence; both IRateEngine-based replacements in this part of the deck, RefactoredRateEngine and RewrittenRateEngine, exist specifically so a team can compare the two paths side by side under identical equivalence tests before committing to either one in production.");
}

{
  const s = S();
  addCodeSlide(s, "REFACTOR OR REWRITE", "Same contract, two paths", "C#", [
    "public interface IRateEngine",
    "{",
    "    RateQuote Quote(RateRequest request);",
    "}",
    "",
    "public sealed class RefactoredRateEngine(ITariffSource tariffs, IClock clock) : IRateEngine",
    "{",
    "    public RateQuote Quote(RateRequest request)",
    "    {",
    "        var subtotal = request.DeclaredValue * RateRules.DeclaredValueRate;",
    "        subtotal += request.WeightKilograms * tariffs.RatePerKilogram(request.LaneCode);",
    "        var at = clock.UtcNow;",
    "        if (PeakWindow.IsPeak(at)) subtotal *= 1m + RateRules.PeakMultiplier;",
    "        return new RateQuote(request.ConsignmentId, subtotal, at);",
    "    }",
    "}",
    "",
    "public sealed class RewrittenRateEngine(ITariffSource tariffs, IClock clock) : IRateEngine",
    "{",
    "    // Same contract as one consolidated expression, not a running subtotal.",
    "    public RateQuote Quote(RateRequest request) { /* ... */ }",
    "}",
    "",
    "public sealed class QuoteService(IRateEngine engine)",
    "{",
    "    public RateQuote QuoteFor(RateRequest request) => engine.Quote(request);",
    "}",
    "",
    "// Assert.Equal(refactored.Quote(request), rewritten.Quote(request)) passes.",
  ], "RateQuote is a record, so the equality assertion compares Amount and QuotedAt by value, not by reference.", { fontSize: 9, y: 1.72, h: 4.95 });
  addNotes(s, "IRateEngine is the one seam neither the legacy engine nor either replacement needs to see through each other: a caller bound to this interface, such as QuoteService, can swap RefactoredRateEngine for RewrittenRateEngine, or back again, as a one-line change at the construction site with no change to QuoteFor itself. RefactoredRateEngine keeps the legacy calculation's original shape, a running subtotal built up statement by statement, now computed through injected collaborators instead of a hard-coded tariff table and the ambient clock. RewrittenRateEngine computes the identical result as one consolidated expression instead, the clean-rewrite alternative built from scratch rather than migrated in place. Because RateQuote is a record, Assert.Equal between the two engines' outputs compares ConsignmentId, Amount, and QuotedAt by value, which is what lets a test prove a rewrite is not a silent behavior change rather than merely assuming it from the code review.");
}

// ===========================================================================
// Section 03: Working with AI and LLM tools
// ===========================================================================
divider("03", "Working with AI and LLM Tools", "Name the intent, hold the output to the forces test, a human owns the gate",
  "An AI assistant drafting C# is fast at producing a plausible shape and has no way of knowing whether this particular codebase has earned that shape yet. One recurring failure mode names the gap directly: a Strategy interface with exactly one implementation, the strategy tower an assistant reaches for before a second rating formula actually exists. SingleImplementationStrategyAnalyzer encodes the check as a Roslyn diagnostic, SHIP0002, that runs on every build and every keystroke rather than depending on a reviewer noticing the pattern by eye. The section closes on what the analyzer does not do: it raises a warning, never an automatic fix, because deciding whether to collapse the abstraction or add the second strategy that justifies it is a judgment call a human still has to make.");

{
  const s = S();
  addDiagramSlide(s, "WORKING WITH AI", "Treat AI output like a draft from a fast, inexperienced teammate", "46-working-with-ai-and-llm-tools",
    "Fast at producing a plausible shape, with no way to know whether this codebase has actually earned that shape yet.");
  addNotes(s, "The throughline across this whole section is a stance toward AI-assisted code rather than a single technique: an assistant can draft a Strategy interface, an extracted seam, or a dependency-breaking refactor quickly and often reasonably, but it has no visibility into whether this specific codebase has a second rating formula planned, a test suite that already depends on a particular shape, or a team convention the draft does not match. That gap is exactly why the testing and legacy-code sections of this deck matter as a prerequisite to this one: the same seam-and-verify discipline that makes an inherited engine safe to change is what makes an AI-drafted engine safe to accept. A reviewer who can name the specific forces a pattern is meant to resolve is equipped to ask whether an assistant's output actually resolves them, rather than accepting a familiar-looking shape on sight.");
}

{
  const s = S();
  addDiagramSlide(s, "WORKING WITH AI", "A Strategy tower with exactly one strategy", "46-single-implementation-strategy",
    "IRatingStrategy with only StandardRatingStrategy in place is pure abstraction cost until a real second shape arrives.");
  addNotes(s, "An interface with a single implementer costs a reader an indirection, a reader now has to open two files to find out what one call actually does, while buying nothing a direct class would not already provide, since nothing varies behind the abstraction yet. This specific shape, an interface whose name ends in Strategy with exactly one implementing type in the whole compilation, is common enough in AI-drafted output to name directly: an assistant asked to price a booking will often reach for a Strategy interface on the first draft, anticipating a second pricing rule that has not been requested and may never arrive. The fix is not to ban interfaces from AI-assisted code; it is to flag this specific, checkable shape automatically and let a reviewer decide, case by case, whether the abstraction is pulling its weight or anticipating a need nobody has yet.");
}

{
  const s = S();
  addCodeSlide(s, "WORKING WITH AI", "SHIP0002: flag a Strategy interface with one implementation", "C#", [
    "private const string StrategySuffix = \"Strategy\";",
    "",
    "private static readonly DiagnosticDescriptor Rule = new(",
    "    \"SHIP0002\",",
    "    title: \"Strategy interface has a single implementation\",",
    "    messageFormat: \"Strategy interface '{0}' has a single implementation '{1}'; \" +",
    "        \"review before this merges\",",
    "    category: \"ShippingReview\",",
    "    defaultSeverity: DiagnosticSeverity.Warning,",
    "    isEnabledByDefault: true,",
    "    customTags: WellKnownDiagnosticTags.CompilationEnd);",
    "",
    "public override void Initialize(AnalysisContext context)",
    "{",
    "    context.EnableConcurrentExecution();",
    "    context.RegisterCompilationStartAction(start =>",
    "    {",
    "        var candidateInterfaces = new ConcurrentDictionary<ISymbol, byte>(SymbolEqualityComparer.Default);",
    "",
    "        start.RegisterSymbolAction(ctx => CollectType(ctx, candidateInterfaces), SymbolKind.NamedType);",
    "        start.RegisterCompilationEndAction(end => ReportSingleImplementations(end, candidateInterfaces));",
    "    });",
    "}",
    "",
    "// IsStrategyInterface: type.TypeKind == TypeKind.Interface",
    "//     && type.Name.EndsWith(\"Strategy\", StringComparison.Ordinal)",
  ], "CompilationEnd tells the analyzer this rule can only be judged once every type in the compilation has been seen.", { fontSize: 9, y: 1.72, h: 4.95 });
  addNotes(s, "Counting how many types implement a given Strategy interface can only be judged once every type in the whole compilation has been visited, so SHIP0002 reports from a RegisterCompilationEndAction rather than a per-type check, and the WellKnownDiagnosticTags.CompilationEnd tag on the descriptor is what the Roslyn analyzer rule RS1037 requires to say so. RegisterCompilationStartAction runs once at the start of each compilation and sets up the concurrent collections every subsequent symbol visit writes into; RegisterSymbolAction then runs CollectType against every named type the compiler visits, safely in parallel, recording each Strategy-suffixed interface and every type that implements it. The naming guard itself, type.Name.EndsWith(\"Strategy\"), is a convention rather than a language feature, which is exactly why this rule needs to be written down as code: no compiler feature enforces a Strategy-suffix naming convention on its own.");
}

{
  const s = S();
  addCodeSlide(s, "WORKING WITH AI", "A second shape the analyzer can see", "C#", [
    "public interface IRatingStrategy",
    "{",
    "    decimal Quote(BookingRequest request);",
    "}",
    "",
    "public sealed class StandardRatingStrategy : IRatingStrategy",
    "{",
    "    private const decimal PerKilogramRate = 2.35m;",
    "    private const decimal InsuranceRate = 0.004m;",
    "",
    "    public decimal Quote(BookingRequest request) =>",
    "        request.WeightKilograms * PerKilogramRate + request.DeclaredValue * InsuranceRate;",
    "}",
    "",
    "public sealed class ExpressRatingStrategy : IRatingStrategy",
    "{",
    "    private const decimal PerKilogramRate = 6.10m;",
    "    private const decimal HandlingFee = 18.00m;",
    "    private const decimal InsuranceRate = 0.009m;",
    "",
    "    public decimal Quote(BookingRequest request) =>",
    "        request.WeightKilograms * PerKilogramRate + HandlingFee + request.DeclaredValue * InsuranceRate;",
    "}",
    "",
    "// SingleImplementationStrategyAnalyzer has nothing to flag on IRatingStrategy now.",
  ], "A flat ground rate and an express rate with its own handling fee are two real formulas, not a placeholder second class.", { fontSize: 10, y: 1.72, h: 4.95 });
  addNotes(s, "StandardRatingStrategy and ExpressRatingStrategy give IRatingStrategy exactly what SingleImplementationStrategyAnalyzer is checking for: two distinct pricing formulas, a flat per-kilogram ground rate against a higher express rate that adds a fixed handling fee and a steeper insurance surcharge for faster transit. Neither strategy is a placeholder standing in for the other; each prices a different service level a shipper actually offers, which is the difference between an abstraction with real work to do and one anticipating a need nobody has stated yet. Once the second implementation exists, the analyzer's symbol-counting pass finds two implementers for IRatingStrategy instead of one and reports nothing, exactly the quiet outcome the corrected code should produce. The fix here was never to delete the interface; it was to supply the second shape that justified having drafted one in the first place.");
}

{
  const s = S();
  addDiagramSlide(s, "WORKING WITH AI", "The analyzer as the gate, not the fix", "46-analyzer-as-the-gate",
    "SHIP0002 raises a build-time warning; it never rewrites the code itself, so a human still decides how to resolve it.");
  addNotes(s, "SHIP0002 stays a Warning, not an error, and carries no attached code fix that would collapse the interface or scaffold a second strategy automatically: the diagnostic's entire job is to make a single-implementation Strategy interface visible to a reviewer, not to decide for that reviewer what to do about it. The two legitimate responses look different depending on the situation an assistant could not see: collapse the interface into a single concrete class when no second strategy is actually planned, or keep the interface and add the second rating formula that was supposed to justify it in the first place. A project that sets TreatWarningsAsErrors turns this diagnostic into a hard build failure, which is the right posture for a team that wants every AI-drafted Strategy interface reviewed before it merges rather than discovered later; a project that leaves it as a warning still gets the visibility without blocking a build over a judgment call still in progress.");
}

// ===========================================================================
// Closing
// ===========================================================================
{
  const s = S();
  addContentTitle(s, "CLOSING", "Prove it, change it safely, and keep the human on the gate");
  addTwoColBullets(s, [
    "Test it: a four-phase xUnit fact over three NSubstitute doubles proves ShipmentRatingEngine's behavior, not just that it compiles and runs.",
    "Change it safely: a reflection proof exposes a sealed legacy engine's missing seams, Feathers opens the minimal one a test needs, Fowler reshapes the method on top of it, and an equivalence test checks the suite stays green across the move.",
  ], [
    "Choose on the evidence: IRateEngine lets RefactoredRateEngine and RewrittenRateEngine sit behind QuoteService as a one-line swap, decided by coverage, change frequency, blast radius, and domain stability.",
    "Let AI accelerate, not decide: SHIP0002 flags a Strategy interface with one implementation at build time; a human still decides whether to collapse it or add the second shape that earns it.",
  ], { fontSize: 16 });
  addNotes(s, "Four sections, one habit underneath all of them: make the thing a test needs to check, or a reviewer needs to see, visible and checkable rather than implicit. The testing section gives a vocabulary, Arrange-Act-Assert, stub, and verify, for proving a method's behavior rather than only its compilation. The legacy-code sections turn that vocabulary into a repair kit for code that was never built with a test in mind, opening one seam at a time and proving behavior is preserved across a reshaping with an equivalence test. The refactor-or-rewrite section turns a gut call into four named criteria compared side by side behind a shared interface. The AI section closes the loop: the same discipline that makes an inherited engine safe to change is what makes an assistant-drafted engine safe to accept, with a build-time diagnostic doing the noticing and a person doing the deciding.");
}

refTable(
  "REFERENCE",
  "The test-double taxonomy, mapped onto this stack",
  [
    { code: "Dummy", name: "Test double", purpose: "Substitute.For<IAuditSink>() passed into a constructor but never configured or asserted on, filling a parameter slot nothing else needs." },
    { code: "Stub", name: "Test double", purpose: "carrierRates.GetBaseRateAsync(...).Returns(new Money(100.00m)) hands back a canned value with no behavior behind it." },
    { code: "Spy", name: "Test double", purpose: "ListAuditLog.Entries records every message Record() receives, so a caller reads back the full history after the fact." },
    { code: "Mock", name: "Test double", purpose: "audit.Received(1).Record(Arg.Is<string>(...)) asserts a specific call happened with the expected content." },
    { code: "Fake", name: "Test double", purpose: "FakeClock and StandardTariffSource are working implementations, lighter than production, standing in for an injected seam." },
  ],
  "The same five-way taxonomy from the testing literature, named against the calls this deck actually used: a Dummy that fills a slot, a Stub that returns a canned value, a Spy that records what it saw, a Mock that is itself asserted against, and a Fake that runs as a working implementation, lighter than production. NSubstitute's Substitute.For<T>() plays several of these roles depending only on how a test configures and asserts against it afterward; the taxonomy describes the role a double plays in a given test, not a property fixed on the double itself."
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

  addNotes(s, "This is the Design Patterns with C# deck, this is the 301 testing, legacy, and AI deck. The two codes take it with you: on the left, the site, the chapters, the runnable C# examples, and the diagrams. On the right, the Workshops and Tutorials hub, every workshop, tutorial, and deck in the series in one place. Both are public on the Pattern Catalyst GitHub organization.");
}

// ===========================================================================
pres.writeFile({ fileName: OUT })
  .then(p => console.log("WROTE", p, "slides:", pageNum))
  .catch(e => { console.error(e); process.exit(1); });
