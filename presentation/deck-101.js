// deck-101.js - "Design Patterns with C#" - 101: Patterns & SOLID
// Red Hat-branded 16:9 deck. Build:  node deck-101.js  (from presentation/)

"use strict";

const H = require("./deck-helpers.js");
const {
  COLOR, FONT, W, ASSETS, PNG,
  newDeck, addFooter, addContentTitle, addBullets,
  addStatusTable, addCaption, addSectionDivider, addNotes,
  addCodeSlide, addDiagramSlide,
} = H;

const OUT = "csharp-design-patterns-101-patterns-and-solid.pptx";

const pres = newDeck();
pres.title = "Design Patterns with C# - 101: Patterns & SOLID";
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
  s.addText("101: Patterns and SOLID", {
    x: 6.00, y: 4.65, w: 6.70, h: 0.90,
    fontFace: FONT.body, fontSize: 18, italic: true, color: COLOR.caption,
    align: "left", valign: "top",
  });
  try { s.addImage({ path: `${ASSETS}/logo-candidate-2.png`, x: 11.10, y: 6.80, w: 1.55, h: 0.37 }); } catch (e) {}
  addNotes(s, "Design Patterns with C#, the 101 deck: patterns and SOLID. The question running through every slide is not what the code looks like, but what pressure in the design made someone reach for a name. We will walk why a pattern is a decision under forces, what the 1994 catalogue assumed that modern C# now resolves directly in the language, the first five SOLID principles worked through one rate calculator and one carrier gateway, dependency injection with the container that ships in the framework, and a short discipline for choosing well. Every example lives in a shipping domain: consignments, carriers, rate quotes, tariffs, manifests. This deck stays conceptual; the idiomatic C# implementation detail for every pattern in the full catalogue is a separate deck.");
}

// ===========================================================================
// Section: Why Patterns
// ===========================================================================
divider("01", "Why Patterns", "Recognizing a decision under forces",
  "This section sets up the vocabulary everything else depends on. A pattern is not a recipe copied from memory; it is a name attached to a recurring set of forces, competing requirements that pull against each other in the same piece of code. A shipping company's rate-quote problem carries this book's running example: accuracy, speed, and changeability, three forces that never point the same direction at once. Naming a pattern before naming the forces behind it is how a design decision turns into ceremony, and that discipline matters more, not less, once an assistant can produce any pattern's shape on request.");

patternSlide(
  "WHY PATTERNS",
  "Patterns as decisions under forces",
  [
    "A force is a competing requirement pulling a design in a different direction: more flexibility against more predictability, faster execution against easier change, a cache against a guarantee the cache can go stale.",
    "The Consequences section of a classic pattern description is a forces ledger: what the pattern buys you and what it costs, stated in the same breath, not a list of benefits alone.",
    "Name the forces before naming a pattern. A pattern reached for by name, forces unexamined, produces an interface, several concrete classes, and a factory for a decision a single method already resolves.",
  ],
  "01-patterns-as-decisions-under-forces",
  "The rating problem as three forces, accuracy, speed, and changeability, pulling against a single rate-quote decision.",
  "A team that has only skimmed the pattern catalogue tends to treat it as a lookup table: a creation problem reaches for a factory, a varying-behavior problem reaches for a strategy. The catalogue was never meant to be read that way. Its own structure borrows a definition from architecture, where a pattern names a recurring problem together with the forces in tension around it, and a solution that balances those forces in a given context. Reading patterns as decisions under forces matters specifically for this book because several forces that once justified a full class-and-interface structure are resolved today by a single C# language feature, and only a forces reading can tell the difference between a tension that still needs an explicit structure and one a delegate variable or a nullable annotation already closes."
);

patternSlide(
  "WHY PATTERNS",
  "The rating problem: three forces pull at once",
  [
    "A rate quote for a consignment has to be accurate enough to match what a carrier bills, fast enough to return inside a checkout flow, and changeable enough for a surcharge or discount tier to update on finance's schedule, not a release calendar.",
    "The three forces compete directly: calling every carrier's live rate API maximizes accuracy and fails the speed force; caching tariffs maximizes speed and risks a stale surcharge the moment one changes between refreshes.",
    "Forces are local, not global: changeability dominates a base-tariff lookup, accuracy dominates the final cross-carrier comparison, and a workable design assigns a different compromise to each stage of the same pipeline.",
  ],
  "01-forces-first-vs-pattern-first",
  "Starting from forces versus starting from a pattern name, applied to the same rate-quote decision.",
  "A consignment arrives with an origin, a destination, a weight, and requested services, and producing a quote means combining a base tariff, a fuel surcharge, a peak-season surcharge, a residential-delivery surcharge, a negotiated discount tier, and a comparison across several carrier APIs, each changing on its own schedule. None of this is a defect in the problem; it is what a real pricing problem looks like once no single force is allowed to dominate. Treating the whole pipeline as one answer, cache everything or call everything live, ignores that a base-tariff lookup, a surcharge stage, and a cross-carrier comparison are each answering to a different dominant force, which is also, mechanically, what a well-chosen structure does when applied at the right granularity."
);

patternSlide(
  "WHY PATTERNS",
  "Proven in code, not asserted",
  [
    "Every example targets the .NET 10 SDK and C# 14, built and tested with the local dotnet CLI, with nullable reference types and warnings-as-errors enforced project-wide through one shared build settings file.",
    "xUnit v3 plus NSubstitute test every example's behavior, a quote stays non-negative, a consignment stays unmutated, a substitute collaborator was called exactly once, not merely that a build exits clean.",
    "Every example stands alone: clone one example's directory, run dotnet test, and get the same six, eight, or twenty-nine passing cases the full repository's test run gets, with no container anywhere in the loop.",
  ],
  "00-prerequisites",
  "The .NET 10 SDK, dotnet CLI, and example harness this book builds against, with no IDE and no container between a reader and the code.",
  "There is no Windows path and no path through a particular IDE anywhere in this book; every editor named is a front end over the same dotnet CLI commands a contributor with only a terminal and a text editor can run by hand. A single shared build-properties file sets the target framework, nullable reference types, and treat-warnings-as-errors once, and every example inherits it automatically, so a nullable warning on code a reader wrote becomes a build failure before a test ever runs. That consistency is what lets every later pattern discussion skip the toolchain question entirely and spend its slide budget on the force the pattern actually resolves."
);

// ===========================================================================
// Section: What Modern C# Changed
// ===========================================================================
divider("02", "What Modern C# Changed", "The temporal thesis",
  "This book's thesis is temporal: modern C# against the assumptions a pattern's original description was written under, never one language measured against another. The 1994 catalogue's object model gave no cheap way to pass behavior around as a value and no type-level way to say a reference could never be absent, and a disproportionate share of its structural ceremony exists to work around exactly those two gaps. C# closes both directly in the language. The next several minutes walk three patterns that dissolve into a single compiler-checked feature, and one, Composite, that does not, because its force is a statement about the shipping domain's own shape, not a gap in what the language can express.");

patternSlide(
  "WHAT MODERN C# CHANGED",
  "What 1994 assumed",
  [
    "The 1994 object model gave no cheap, type-safe way to pass a piece of behavior around as an ordinary value, so wrapping behavior in an object and passing the object was the only portable option available.",
    "The same object model gave no type-level way to say a reference could never be absent; every reference was a potential null, so a method with nothing to return picked between a check at every call site or a do-nothing stand-in object.",
    "Those two gaps, no first-class functions and no compiler-checked nullability, explain a disproportionate share of the catalogue's structural ceremony, and C# closes both directly in the language rather than asking every team to re-solve them.",
  ],
  "02-what-1994-assumed-and-csharp-invalidated",
  "Three gaps the 1994 object model left open, no first-class functions, no compiler-checked nullability, and hand-rolled subscriber lists, each now closed by a C# language feature.",
  "A developer trained only on the catalogue's abstract-class-and-factory habits reaches for an interface, a concrete class per variation, and a factory the moment a rate quote needs to vary by customer, because that was the only type-safe way to parameterize behavior under the catalogue's original constraints. In C#, the same requirement is a parameter typed as a delegate value. Writing the interface-and-factory version anyway is not more correct; it is several extra types a reviewer has to read, a test suite has to cover, and an assistant has to load into context before it can reason about a one-line change, all to resolve a force the compiler already resolves for free."
);

{
  const s = S();
  addCodeSlide(s, "WHAT MODERN C# CHANGED", "Observer becomes the event keyword", "C#", [
    "public sealed class Consignment",
    "{",
    "    public event EventHandler<ConsignmentDelayedEventArgs>? Delayed;",
    "",
    "    public void ReportDelay(TimeSpan by) =>",
    "        Delayed?.Invoke(this, new ConsignmentDelayedEventArgs(by));",
    "}",
    "",
    "// A tracking dashboard subscribes without implementing an Observer interface.",
    "consignment.Delayed += (sender, args) =>",
    "    Console.WriteLine($\"Consignment delayed by {args.By}\");",
  ], "The compiler generates the subscriber list and the +=/-= accessors; a listener subscribes with no Observer interface to implement.");
  addNotes(s, "The event keyword declares a member backed by a delegate type, and the compiler generates the subscription list, the += and -= operators to join and leave it, and the null-safe invocation pattern to broadcast to every current subscriber. One field declaration replaces a hand-rolled subject, a listener interface, and a manual notification loop. What the language does not remove is the discipline Observer always required: a subscriber still has to unsubscribe when it is no longer interested, or the subject keeps a live reference to it and the listener can never be collected.");
}

{
  const s = S();
  addCodeSlide(s, "WHAT MODERN C# CHANGED", "Strategy becomes a delegate value", "C#", [
    "public static Money QuoteRate(",
    "    Consignment consignment, Func<Consignment, Money> pricing) =>",
    "    pricing(consignment);",
    "",
    "Money standard = QuoteRate(consignment,",
    "    c => c.Weight.Kilograms * StandardRatePerKilogram);",
    "",
    "Money expedited = QuoteRate(consignment,",
    "    c => c.Weight.Kilograms * ExpeditedRatePerKilogram + ExpeditedSurcharge);",
  ], "QuoteRate has no rate-strategy interface, no concrete class per algorithm, and no factory to choose between them: the caller's lambda is the whole solution.");
  addNotes(s, "A delegate type built into the base class library encapsulates a method with one parameter and a return value, specifically so a caller does not have to define a delegate of its own; the caller passing a lambda at the call site is Strategy's force, substitutable behavior chosen by the caller, resolved with a value instead of an object graph. An interface-based Strategy still earns its structure when the algorithm needs to carry its own state across calls, implement several related operations instead of one, or be resolved by the dependency injection container under a key rather than chosen inline. Those are different forces; when the sole requirement is that the caller picks the behavior, a delegate value is the entire answer.");
}

{
  const s = S();
  addCodeSlide(s, "WHAT MODERN C# CHANGED", "Null Object becomes nullable reference types", "C#", [
    "public interface ICarrierGateway",
    "{",
    "    Quote? TryGetQuote(Consignment consignment);",
    "}",
    "",
    "Quote? quote = gateway.TryGetQuote(consignment);",
    "if (quote is not null)",
    "{",
    "    Book(quote);",
    "}",
  ], "Quote? and the compiler's flow analysis move the absent-value check into the type signature; a do-nothing stand-in survives only as a deliberate default or test fixture.");
  addNotes(s, "A non-nullable return type is a claim that a value is always present, enforced by the compiler; a nullable return type tells a caller the method sometimes has nothing to return, and the compiler warns on a dereference that has not first checked for that case. That warning is the mechanism a do-nothing stand-in object existed to make unnecessary by a different route: instead of guaranteeing every call site behaves safely by handing it a safe object that does nothing, the compiler now tells a call site directly when it is handling a possibly-absent value unsafely, before the program runs. A collaborator that wants a no-op implementation it can call unconditionally, a null audit sink used in a test double or a default configuration, still benefits from a real object that satisfies the interface and does nothing, because the goal there is eliminating a branch, not expressing possible absence.");
}

{
  const s = S();
  addDiagramSlide(s, "WHAT MODERN C# CHANGED", "The divergence map", "03-the-divergence-map",
    "Ten C# 14 / .NET 10 language and runtime features, each mapped to the force it closes.");
  addNotes(s, "A C# 14 source file and one written in the catalogue's object-and-interface style read alike at a glance: braces, interfaces, generics, a class keyword doing the same job in both. The family resemblance is the trap. The language has shipped more than a decade of language and runtime additions the catalogue's era had no way to anticipate: the event keyword, delegates with a built-in multicast invocation list, yield and LINQ, nullable reference types, extension members, expression trees, source generators, reified generics, static abstract interface members, and value types that opt into stack-allocated, copy-by-value semantics. Three of the ten live entirely in library and runtime design, not new syntax, and seven depend on dedicated compiler syntax added across later language versions; the distinction matters for judging how deep a given feature goes before a pattern's shape can lean on it.");
}

{
  const s = S();
  addContentTitle(s, "WHAT MODERN C# CHANGED", "The test: did the force move, or stay?");
  addBullets(s, [
    "Ask what force a classic pattern's forces ledger names, then ask whether that force is a gap in what the language itself can express, or a gap in how collaborators in the domain relate to each other.",
    "A language-expressibility gap is a candidate to dissolve into the feature that closes it; a domain-shape gap keeps earning its original structure, in modern C#, for as long as the domain keeps presenting that shape of problem.",
    "Composite is the counter-example: treating a single shipment and a consolidated shipment made of several smaller ones through the same interface is a statement about how a shipping company's domain pieces relate, not a gap any delegate type or nullable annotation closes.",
  ], { fontSize: 17 });
  addNotes(s, "Observer, Strategy, and Null Object dissolve under this reading because the force each one resolved, broadcasting to unknown listeners, substituting behavior at a call site, representing an absent value safely, is now resolved by a construct built into the language and checked by the compiler. None of the three disappear because they were wrong when first described; they disappear because the gap they were invented to fill is not there anymore in C#. Composite fails the test in the opposite direction on purpose: no delegate type, no nullable annotation, and no other language feature changes what a consolidated shipment is or how its parts compose, which is why this book keeps Composite's original shape, in idiomatic modern syntax, while letting Observer, Strategy, and Null Object collapse.");
}

patternSlide(
  "WHAT MODERN C# CHANGED",
  "Naming patterns for teams and AI",
  [
    "A pattern name is compressed, checkable design intent: labeling a class a Decorator around a rate quote tells a reviewer it keeps the same interface, delegates the call it does not change, and adds exactly one responsibility, before a line of the body is read.",
    "The same compression applies below the pattern catalogue, to a domain's own vocabulary: a method named MarkCleared commits to a specific precondition, customs documentation validated, and a specific postcondition, billing may proceed, that a status code typed as a bare integer never states.",
    "The same compression steers an AI assistant: a request that names the pattern or the lifecycle stage narrows what gets generated toward a known shape, and a name an assistant attaches to its own output is a claim to check, not a guarantee to trust.",
  ],
  "04-naming-patterns-for-teams-and-ai",
  "One pattern name, read the same way by a code reviewer and an AI assistant, carrying the same invariants both times.",
  "A code review runs faster when the reviewer and the author already agree on what a piece of code is supposed to be; a reviewer who knows what Decorator means checks a diff against a known shape instead of inferring the shape from scratch. The same discipline runs across the consignment lifecycle, booked, loaded, in transit, cleared, delivered, where a method name carries the same precondition-and-postcondition contract a pattern name carries. A request that names the pattern or the lifecycle stage gives an assistant the same compressed information it gives a human reviewer, and a generated class that labels itself a Decorator gets held to the same three checks a human author's claim would invite: same interface, one added responsibility, delegates the rest."
);

// ===========================================================================
// Section: SOLID
// ===========================================================================
divider("03", "SOLID", "One force: this will change",
  "SOLID is not a separate topic from patterns; it is the forces-and-trade-offs vocabulary the whole discussion gets judged against. All five principles respond to one underlying pressure: the code around a decision should not have to change just because the decision itself changes. A rate calculator that prices, taxes, and audits a shipment in one method walks through Single Responsibility, Open/Closed, and Liskov Substitution; a carrier gateway that quotes, books, and tracks walks through Interface Segregation and Dependency Inversion, wired by the dependency injection container that ships inside the framework.");

patternSlide(
  "SOLID",
  "One overloaded calculator, three reasons to break",
  [
    "A single rate-quote method that prices a consignment, applies a jurisdiction's tax, and writes an audit record mixes three unrelated reasons to change: a new pricing tier, a tax-rate update from finance, a new compliance field on the audit record.",
    "Splitting the method into focused collaborators, a pricing calculator, a tax policy, an audit sink, means a change to one never risks the other two, and each one gets its own focused test with no unrelated collaborator standing in the way.",
    "The split sets up the rest of this discussion: a surcharge pipeline that grows by addition, and two pricing strategies that substitute for each other because a test proves it rather than assumes it.",
  ],
  "05-srp-ocp-lsp",
  "A single overloaded rate calculator splits into focused collaborators for pricing, tax, and audit; a surcharge decorator adds charges without editing existing code; two pricing strategies substitute for each other safely.",
  "A rate quote for a shipment looks like a single calculation: take a weight, apply a rate, done. In a production carrier system it never stays that simple, because the same method that prices the shipment usually also applies a jurisdiction's tax and writes an audit record, three different reasons for someone to come back and change it, sharing local variables, a call order, and one set of tests that has to keep passing for all three jobs at once. None of these three changes is unusual; each one is routine, and each one is harder than it needs to be only because the method never decided which job it owns."
);

{
  const s = S();
  addCodeSlide(s, "SOLID", "Single Responsibility: one reason to change", "C#", [
    "public interface IRateQuote",
    "{",
    "    Money Quote(Consignment consignment);",
    "}",
    "",
    "public interface ITaxPolicy",
    "{",
    "    Money ApplyTax(Money subtotal);",
    "}",
    "",
    "public interface IAuditSink",
    "{",
    "    Task RecordQuoteAsync(Consignment consignment, Money finalQuote,",
    "        CancellationToken cancellationToken = default);",
    "}",
  ], "WeightBasedRateCalculator takes no tax policy and no audit sink; a tax-rate change from finance touches one file and reruns one focused test class.");
  addNotes(s, "Single Responsibility does not say a class should be small; it says a class should have one reason to change, and the size follows from that, not the other way around. WeightBasedRateCalculator implements IRateQuote by multiplying a consignment's weight by a configured rate and nothing else, with a test that constructs it alone and gets a correct quote straight back, the test-level proof that pricing's only dependency is a rate. A reviewer approving a change to the tax policy is reviewing a tax change, not a tax change bundled with whatever pricing or auditing code happened to share its method body before the split.");
}

{
  const s = S();
  addCodeSlide(s, "SOLID", "Open/Closed: a surcharge pipeline that grows by addition", "C#", [
    "public interface ISurcharge",
    "{",
    "    Money Apply(Money subtotal, Consignment consignment);",
    "}",
    "",
    "public sealed class SurchargedRateQuote(",
    "    IRateQuote basePricing, IReadOnlyList<ISurcharge> surcharges) : IRateQuote",
    "{",
    "    public Money Quote(Consignment consignment)",
    "    {",
    "        var subtotal = basePricing.Quote(consignment);",
    "        return surcharges.Aggregate(subtotal,",
    "            (running, surcharge) => running + surcharge.Apply(running, consignment));",
    "    }",
    "}",
  ], "A new HazmatHandlingSurcharge, defined only in a test, composes alongside the shipped FuelSurcharge with zero edits to any pricing or surcharge type already shipped.");
  addNotes(s, "SurchargedRateQuote implements the same IRateQuote interface its wrapped base pricing implements, so a caller that only knows about IRateQuote cannot tell whether it is quoting a bare calculator or one wrapped in an arbitrary stack of surcharges. The proof that this pipeline stays open for extension is a test file that defines a brand-new flat-fee surcharge nowhere in the shipped library, composes it alongside the existing fuel surcharge, and asserts on the combined total, with zero changes required to the calculator, the quote wrapper, or any surcharge that existed before the test was written. Open/Closed is not free: a surcharge abstraction costs a constructor parameter and a small amount of indirection that a single fixed fuel surcharge would not need, and paying that cost ahead of a second surcharge actually arriving is speculation, not Open/Closed design.");
}

{
  const s = S();
  addContentTitle(s, "SOLID", "Liskov Substitution: a contract both calculators honor");
  addBullets(s, [
    "WeightBasedRateCalculator and TieredRateCalculator price a consignment with completely different formulas, and a caller holding an IRateQuote has to be able to swap one for the other with no change in behavior it can observe.",
    "Four behavioral checks run over both implementations as data-driven test cases: every quote returns a non-negative amount, preserves the consignment's currency, leaves the consignment unmutated, and never charges less for a heavier consignment than a lighter one.",
    "The same four checks run again over both surcharge implementations, because a surcharge is exactly as substitutable through its interface as a pricing strategy is through IRateQuote, and deserves the same proof rather than an assumption.",
  ], { fontSize: 17 });
  addNotes(s, "Liskov Substitution asks whether a difference in formula between two pricing strategies is the only difference, or whether it comes bundled with a difference in behavior a caller cannot see from the method signature: a negative amount under some input, a silently converted currency, a mutated consignment, a heavier shipment charged less than a lighter one. What distinguishes a Liskov substitution test from a compiler's type check is that the test calls each implementation with a real consignment and a real money value and asserts on the number that comes back, rather than confirming a method merely exists with the right shape. Writing those four invariants into the interface's own documentation, not only into test assertions, matters just as much: a third pricing strategy added later, without ever looking at the test suite, still has three sentences to satisfy, and the same checks will catch a strategy that quietly violates one of them.");
}

patternSlide(
  "SOLID",
  "Interface Segregation",
  [
    "A fat carrier gateway interface with a quote method, a booking method, and a tracking method forces a rate-shopping consumer to depend on members it never calls, and forces a quote-only freight broker to fake a booking method it has no correct implementation for.",
    "Splitting into a rates interface and a tracking interface, then recomposing both into one gateway interface for implementers that need the whole surface, gives each consumer exactly the capabilities it uses and nothing wider.",
    "A fat interface manufactures a Liskov violation that segregation makes structurally impossible: once the interface is narrow, there is nothing left on it for an incomplete implementer to fake.",
  ],
  "06-isp-dip-and-built-in-di",
  "ISP splits one fat carrier interface into focused role interfaces; DIP routes each consumer through those abstractions instead of a concrete gateway.",
  "A shipping backend touches a carrier at two moments that have nothing to do with each other: a prospective customer wants a price before committing to anything, and a customer who already committed wants a shipment booked and later wants to know where it is. A rate-shopping consumer built against one fat interface still carries a constructor parameter for booking and tracking it never calls, and its test has to substitute something for both anyway because the compiler requires the full interface before anything type-checks. Segregating into a rates role and a tracking role, recomposed into one gateway interface only where an implementer genuinely needs both, means a quote-only freight broker integration now implements just the rates role and has nothing left to throw from or fake."
);

{
  const s = S();
  addContentTitle(s, "SOLID", "Dependency Inversion");
  addBullets(s, [
    "ShipmentRateAdvisor depends on the narrow rates interface alone, never on the full gateway interface and never on a concrete gateway; it has no booking or tracking concern to fake in a test.",
    "ShipmentBookingService depends on the full gateway interface, also never on a concrete type; the narrow consumer and the wide consumer each point at an abstraction, never at each other.",
    "High-level policy and the carrier gateway both depend on the same abstraction, and the abstraction depends on neither; swapping which concrete gateway backs it touches a composition root, never either consumer.",
  ], { fontSize: 17 });
  addNotes(s, "Dependency Inversion states that high-level modules should not depend on low-level modules; both should depend on an abstraction, and the abstraction should not depend on the details. A concrete REST-backed carrier gateway implements the gateway interface rather than being depended on by name, so the dependency that would naturally run from a high-level policy down into a concrete carrier integration is redirected to stop at the interface both sides share. ShipmentRateAdvisor's own test substitutes a double for the rates interface alone and never links against any concrete gateway, which is the payoff made concrete: a change to which carrier backs the system is a one-line registration change, not a search across every class that talks to a carrier.");
}

// ===========================================================================
// Section: Dependency Injection, built in
// ===========================================================================
divider("04", "Dependency Injection, Built In", "No separate framework decision",
  "A carrier gateway split into role interfaces still leaves one question neither interface answers on its own: which concrete type backs them at runtime, and under what lifetime. The dependency injection container that ships inside the framework answers both in one composition root, with no separate package to weigh and no framework decision to make first. This section walks one singleton instance resolved under three different interface faces, the three lifetimes the container offers, and a registration swap that changes a running application's carrier with no consumer ever touched.");

{
  const s = S();
  addContentTitle(s, "DEPENDENCY INJECTION, BUILT IN", "The container ships in the box");
  addBullets(s, [
    "Microsoft.Extensions.DependencyInjection is a built-in part of the framework, alongside configuration, logging, and the options pattern; there is no separate framework decision to make before a project can register a service.",
    "A consumer built against role interfaces still has to answer one question neither interface answers on its own: which concrete type backs them at runtime, and that question gets answered once, in a composition root.",
    "Reaching for the built-in container costs little even on a small object graph: no package to weigh against alternatives, just a handful of registration calls in one place.",
  ], { fontSize: 17 });
  addNotes(s, "A project reaching for dependency injection on this platform is not choosing a framework the way it might choose a web framework or a testing library; the container is already a package reference the same project likely already carries. That matters for how small a graph can be and still benefit: five registrations and two consumer classes are enough to see the full shape, a composition root that is the only place in the codebase that names a concrete carrier gateway type. Everything after this point in the discussion happens inside that one static class.");
}

{
  const s = S();
  addCodeSlide(s, "DEPENDENCY INJECTION, BUILT IN", "One singleton, three faces", "C#", [
    "public static IServiceCollection AddCarrierGateway<TGateway>(",
    "    this IServiceCollection services)",
    "    where TGateway : class, ICarrierGateway",
    "{",
    "    services.AddSingleton<TGateway>();",
    "    services.AddSingleton<ICarrierGateway>(p => p.GetRequiredService<TGateway>());",
    "    services.AddSingleton<ICarrierRates>(p => p.GetRequiredService<TGateway>());",
    "    services.AddSingleton<ICarrierTracking>(p => p.GetRequiredService<TGateway>());",
    "",
    "    services.AddSingleton<ShipmentRateAdvisor>();",
    "    services.AddSingleton<ShipmentBookingService>();",
    "    return services;",
    "}",
  ], "Assert.Same(gateway, rates) and Assert.Same(gateway, tracking) prove the container resolves one instance under three different interface faces.");
  addNotes(s, "The first registration line builds the concrete gateway as a singleton against itself; the three factory-registration calls after it each hand back the exact same instance the first line already registered, resolved through a different interface type every time. The result is three service types, the gateway interface, the rates interface, and the tracking interface, that all resolve to one object, proven directly by asserting the same reference comes back from each. That single instance is what lets ShipmentRateAdvisor ask for the narrow rates face and ShipmentBookingService ask for the full gateway face without either one knowing or caring that the other consumer exists.");
}

refTable(
  "DEPENDENCY INJECTION, BUILT IN",
  "Lifetimes: singleton, scoped, transient",
  [
    { code: "Singleton", name: "Lifetime", purpose: "Created once, on first request; every later resolution for the life of the application returns the same instance." },
    { code: "Scoped", name: "Lifetime", purpose: "Created once per request-like boundary; must be resolved from within a scope, never cached past it." },
    { code: "Transient", name: "Lifetime", purpose: "Created fresh on every resolution; nothing is shared and no state carries between calls." },
  ],
  "A carrier gateway that holds no per-caller state beyond an in-memory booking ledger is safe to share for the life of the whole application, which is why every registration in this example chooses singleton rather than leaving the lifetime unexamined. Scoped registration exists for state that belongs to one request or one unit of work and must not leak into the next one; a console library with no request boundary has nothing for a scope to attach to. The real failure mode here is not choosing the wrong lifetime in isolation, it is resolving a scoped service from a singleton: the scoped service behaves like a singleton too, silently promoting its lifetime and producing state that leaks across requests that were supposed to be isolated from each other."
);

{
  const s = S();
  addCodeSlide(s, "DEPENDENCY INJECTION, BUILT IN", "Swap the registration, touch no consumer", "C#", [
    "var restServices = new ServiceCollection();",
    "restServices.AddCarrierGateway<RestCarrierGateway>();",
    "await using var restProvider = restServices.BuildServiceProvider();",
    "",
    "var fakeServices = new ServiceCollection();",
    "fakeServices.AddCarrierGateway<FakeCarrierGateway>();",
    "await using var fakeProvider = fakeServices.BuildServiceProvider();",
  ], "Neither ShipmentRateAdvisor nor ShipmentBookingService is recompiled or even aware two providers exist; only the resolved behavior differs.");
  addNotes(s, "Two separate containers, one registered against a REST-backed gateway and one against a fake gateway, resolve the same ShipmentRateAdvisor and ShipmentBookingService types with neither class recompiled, edited, or aware that a second provider exists anywhere. What differs between the two runs is only what each resolved service produces: a real tracking number format against a fixed fake one, a destination-and-weight price against a flat rate. A sandbox deployment, a demo environment, or an early integration test against a carrier whose contract is not yet signed all become a one-line change to which generic argument the registration call is given, not a search-and-replace across every class that talks to a carrier.");
}

// ===========================================================================
// Section: Choosing Well
// ===========================================================================
divider("05", "Choosing Well", "Forces before names",
  "Not every varying behavior needs a full pattern, and not every principle applied rigidly earns its cost. This section closes the loop the whole deck opened with: a repeatable test for deciding whether a pattern, a principle, or a piece of generated code is solving a force actually present in the code in front of you, or imitating a familiar shape because the shape is familiar.");

{
  const s = S();
  addContentTitle(s, "CHOOSING WELL", "The forces test: how to choose");
  addBullets(s, [
    "Ask what varies in the code, what stays fixed, and what is expensive to get wrong, before any pattern name enters the conversation.",
    "Name a candidate pattern and a candidate plain alternative side by side, a delegate against an interface hierarchy, a single type against a split by role, and let the forces actually present decide between them.",
    "The rating problem's own answer changes stage by stage: a base-tariff lookup, a surcharge stage, and a cross-carrier comparison each resolve a different dominant force, and a single answer for the whole pipeline misses that.",
  ], { fontSize: 17 });
  addNotes(s, "Every principle and pattern walked through in this deck runs through the same arc: recognize the forces, name a candidate structure and a candidate plain alternative, choose the idiomatic C# expression, test the behavior, and know when it stops paying for itself. A pattern name shows up often enough in interviews and in training material that it is tempting to treat the whole catalogue as trivia; the actual skill is asking what an abstraction is buying right now, in this code, for this team, and having a real answer either way. The rating problem's own three forces, accuracy, speed, and changeability, never carry equal weight at every stage, and a workable design assigns a different compromise to each stage rather than one answer for the whole pipeline.");
}

{
  const s = S();
  addContentTitle(s, "CHOOSING WELL", "When not to apply, and reviewing AI output");
  addBullets(s, [
    "A one-implementation interface with no second carrier, no sandbox double, and no test that would benefit from faking it is cost with no benefit; a concrete class called directly is the correct amount of abstraction.",
    "Ask an assistant to apply SOLID and a common result is an interface for every class regardless of whether a second implementation exists, because that shape appears constantly in material that treats the principles as a checklist.",
    "Hold generated code to the same forces test a human author's diff gets: does a proposed split follow a real difference in who changes the code and why, and does a generated implementation honor the contract its interface promises, not merely its method signature.",
  ], { fontSize: 17 });
  addNotes(s, "Interface Segregation and Dependency Inversion both earn their cost only when a second implementation or a second capability-shaped consumer is actually in the picture; splitting a two-method interface into two single-method interfaces when every consumer already needs every member produces a maze of role types for no consumer's benefit. An assistant asked to add a fuel surcharge as a Decorator sometimes generates the full apparatus, a new interface and a concrete wrapper class, when the codebase's own convention for the same shape is a single delegate field; naming the pattern tells the assistant what contract to honor, not which mechanism satisfies that contract most directly. A reviewer who knows both the pattern and the team's idiom for it catches the oversized version before it merges, the same way a reviewer catches a hand-rolled loop where one built-in query operator would do the same job.");
}

// ===========================================================================
// Section: Reference
// ===========================================================================
divider("06", "Reference", "Worth a second window",
  "The following reference tables are mined directly from the pattern-to-force quick-reference appendix and the wider programming-principles appendix. Keep this section open in a second window during a design discussion: scan from the force you are facing back to the principle or pattern name that answers it, rather than the other way around. A short look forward closes the section: what the next short-term-support release changes, and what it leaves alone.");

refTable(
  "REFERENCE",
  "Principles: SOLID",
  [
    { code: "Single Responsibility", name: "Principle", purpose: "One reason to change per collaborator, not a line-count target." },
    { code: "Open/Closed", name: "Principle", purpose: "New behavior arrives as a new type; proven code is never edited to add it." },
    { code: "Liskov Substitution", name: "Principle", purpose: "A substitute implementation honors the same contract a shared interface promises, proven by behavior, not by compiling." },
    { code: "Interface Segregation", name: "Principle", purpose: "A consumer depends on exactly the capabilities it calls, nothing wider." },
    { code: "Dependency Inversion", name: "Principle", purpose: "High-level policy and low-level detail both depend on an abstraction; neither depends on the other." },
  ],
  "A quick-reference scan from force to principle. Use this table the way the earlier section used it: a competing requirement in the left-hand column names the principle that resolves it in the row. Keep this slide open as a checklist during a design review."
);

refTable(
  "REFERENCE",
  "Patterns that dissolved into a feature",
  [
    { code: "Observer", name: "-> event keyword", purpose: "The event keyword generates the subscriber list and the +=/-= accessors a hand-rolled subject once required." },
    { code: "Strategy", name: "-> delegate value", purpose: "A Func<> value replaces an interface and a concrete class per algorithm when the caller only needs to pick the behavior." },
    { code: "Null Object", name: "-> nullable reference types", purpose: "A nullable annotation and the compiler's flow analysis replace a do-nothing stand-in for the common absent-value case." },
    { code: "Prototype", name: "-> with expression", purpose: "A record with expression replaces a cloning interface for a non-destructive copy of a configured exemplar." },
    { code: "The test", name: "Did the force move?", purpose: "A language-expressibility gap dissolves into the feature that closes it; a domain-shape gap, like Composite's, keeps its structure." },
  ],
  "Four patterns and the feature each one dissolves into, plus the test that decides a fifth case either way. None of the four disappear because the original description was wrong; each one disappears because the gap it was built to work around is no longer present in the language. The last row is the general form of the test: ask what the force actually is before asking which shape resolves it."
);

refTable(
  "REFERENCE",
  "Principles beyond SOLID",
  [
    { code: "DRY", name: "Principle", purpose: "Every piece of knowledge has one authoritative representation; a fixed sequence is written once, not restated by every variant." },
    { code: "YAGNI", name: "Principle", purpose: "Build the abstraction the current requirement needs, not the one a possible future requirement might." },
    { code: "KISS", name: "Principle", purpose: "Prefer the solution a reader can hold in their head; a delegate before an interface, an interface before a keyed registration." },
    { code: "Composition over inheritance", name: "Principle", purpose: "Compose a behavior in as a held reference instead of fixing it at compile time through a base class." },
    { code: "Law of Demeter", name: "Principle", purpose: "A caller talks to its immediate collaborators only, never reaching through one collaborator to another." },
    { code: "Fail-fast", name: "Principle", purpose: "A broken precondition throws at the boundary where it first becomes observable, not several calls later." },
    { code: "Separation of concerns", name: "Principle", purpose: "Distinct concerns, what varies for different reasons, live in distinct places." },
  ],
  "Seven principles from outside the five SOLID letters, each anchored to a real decision already made somewhere in this deck's examples: a fixed template sequence written once, a dispatch table built because a second rendering concern never showed up, a delegate chosen over an interface because it is the form a reader can hold in their head. Recognizing a principle's name in a decision already made is the point, not a new rule to apply on top of it.",
  { rowH: 0.38 }
);

{
  const s = S();
  addContentTitle(s, "REFERENCE", "On the horizon: C# 15 / .NET 11");
  addBullets(s, [
    "As of this writing, the next release is in release candidate; every example in this book targets the current long-term-support SDK and language version, and stays there.",
    "The next runtime's async change changes how the runtime executes an async method, not what an async method is; a task-returning method still compiles to the same stackless state machine this book's concurrency material teaches.",
    "Union types and closed hierarchies sharpen an exhaustive switch this book already leans on; asynchronous options validation adds a network-aware check alongside the synchronous validator this book ships.",
  ], { fontSize: 17 });
  addNotes(s, "Nothing in this appendix asks an existing example to change; it is a map of what moves and what does not once the next release ships. Runtime-native async produces cleaner stack traces and lower overhead without changing the model: a task-returning method still returns a task, cancellation still flows through a cancellation token, and an awaited call still suspends without blocking a thread. Union types and a closed-hierarchy declaration would let a result type's success and failure cases be compiler-checked exhaustively, and an asynchronous options validator would let a configuration check confirm a carrier gateway endpoint actually resolves and answers, rather than only checking the shape of bound configuration in memory.");
}

{
  const s = S();
  addContentTitle(s, "REFERENCE · FOR DEEPER COVERAGE", "Reading list");
  addBullets(s, [
    "Erich Gamma, Richard Helm, Ralph Johnson, and John Vlissides, Design Patterns: Elements of Reusable Object-Oriented Software (Addison-Wesley).",
    "Robert C. Martin, Agile Software Development: Principles, Patterns, and Practices (Prentice Hall).",
    "Andrew Hunt and David Thomas, The Pragmatic Programmer: Your Journey to Mastery (Addison-Wesley).",
  ], { fontSize: 18 });
  addNotes(s, "Three sources behind everything in this deck. The Gang of Four catalogue is where the forces-and-consequences reading of a pattern comes from, and the book this deck measures every classic pattern's original shape against before asking whether a C# feature now resolves the same force directly. Robert Martin's book is the original statement of the SOLID principles this deck builds its rate-calculator and carrier-gateway examples on. Hunt and Thomas's book is the source for DRY, YAGNI, and the Law of Demeter named in the principles-beyond-SOLID reference. All three are worth reading in full once a name alone stops being enough to settle a design review.");
}

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

  addNotes(s, "This is the Design Patterns with C# 101 deck. The two codes take it with you: on the left, the site, the chapters, the runnable C# examples, and the diagrams. On the right, the Workshops and Tutorials hub, every workshop, tutorial, and deck in the series in one place. Both are public on the Pattern Catalyst GitHub organization.");
}

// ===========================================================================
pres.writeFile({ fileName: OUT })
  .then(p => console.log("WROTE", p, "slides:", pageNum))
  .catch(e => { console.error(e); process.exit(1); });
