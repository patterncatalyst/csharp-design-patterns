---
title: "Patterns as decisions under forces"
order: 1
layout: tutorial
sectionid: docs
part: "why-not-gof"
description: "Reframing the catalogue from a taxonomy to a set of decisions made under competing forces, with the rating problem as the book's running thread."
---

## Learning objectives

By the end of this chapter you can:

- Define what a *force* is in a design pattern and point to the section of a
  classic pattern description where the forces are written down.
- State the three competing forces in a shipping company's rate-quote problem
  (accuracy, speed, changeability) and explain, for each, what pulls it up and
  what that costs.
- Explain why matching a problem to a pattern by name produces worse designs
  than analyzing the forces the problem actually presents.
- Describe the question this book asks of every classic pattern before
  reaching for its original structure.

## Why this matters

A team that has only skimmed the Gang of Four catalogue tends to treat it as
a lookup table: a "creation" problem gets a Factory, a "varying behavior"
problem gets a Strategy. The catalogue rewards this reading. Each entry has a
one-line intent, a bolded name, and a tidy class diagram. All three are easy
to memorize, and just as easy to pattern-match against a sentence in a ticket.

The catalogue was never written to be read that way. Its own introduction
borrows its definition of a pattern from Christopher Alexander's work on
architecture. There, a pattern names a recurring problem together with the
*forces* in tension around it, and a solution that balances those forces in a
given context. A force is a pressure pulling the design in some direction:
toward more flexibility, faster execution, fewer moving parts, or tolerance
for a requirement nobody has written down yet. Most real design problems have
several forces pulling in different directions at once. The pattern is the
compromise point, not a shape you apply because its name sounds like your
problem.

This book has a second reason to read patterns as decisions under forces.
Several of the forces that justified a pattern's full class-and-interface
structure in 1994 are resolved today by a single C# language feature. A
reflex, name-matching reading of the catalogue can't tell a force that still
needs an explicit structure from one a delegate variable or a nullable
annotation already resolves. A forces reading can, because it asks what
tension is actually present before asking what shape resolves it.

{% include excalidraw.html name="01-patterns-as-decisions-under-forces" caption="Figure 1.1: The rating problem as three forces, accuracy, speed, and changeability, pulling against a single rate-quote decision." %}

<!-- SECOND FIGURE SPEC: a two-panel comparison diagram, "Forces first" vs "Pattern first." Left panel: a design process starting from the three forces (accuracy, speed, changeability) as labeled pressure arrows converging on a decision point labeled "rate-quote design," with a small annotation that the chosen shape (rule pipeline, cache, data table) falls out of which forces dominate. Right panel: a design process starting from a pattern name in a box ("Strategy") applied directly to the same problem, arrow straight to a four-class structure (interface + three implementations + factory), with no force annotations, ending in a box labeled "ceremony, forces unexamined." Caption should read: "Figure 1.2: Starting from forces versus starting from a pattern name, applied to the same rate-quote decision." -->
{% include excalidraw.html name="01-forces-first-vs-pattern-first" caption="Figure 1.2: Starting from forces versus starting from a pattern name, applied to the same rate-quote decision." %}

## Forces, not a taxonomy

The Gang of Four catalogue structures every pattern the same way: Intent,
Motivation, Applicability, Structure, Participants, Collaborations,
Consequences, Implementation, Known Uses, Related Patterns. The section that
gets skipped most often in a skim, and the one that matters most for this
book, is Consequences. That section is a forces ledger. It states what the
pattern buys you and what it costs, in the same breath. A Decorator buys you
the ability to add responsibilities to an object at runtime, in any
combination, without a subclass for every combination. It costs you a system
built from many small objects that look alike, and a caller that can't
always tell at a glance which decorators are in effect. Both halves are the
point. A pattern that only listed benefits wouldn't be a design pattern,
it would be marketing copy.

This matters because two forces rarely point the same direction. The same
system can want more flexibility (support any combination of surcharges) and
more predictability (one engineer can read a stack trace and know the final
price). A pattern isn't a way to get both at no cost. It's a documented place
where a particular tradeoff has worked out well for a particular shape of
problem, with the costs named up front so the next engineer doesn't
rediscover them the hard way.

The catalogue's remaining two sections reinforce the same point. Known Uses
lists real systems where this exact tradeoff was already chosen, which is
evidence that the forces line up this way often, not a license to apply the
shape anywhere the name seems to fit. Related Patterns points sideways to
the neighboring compromise that applies once one force shifts. Swap a
flexibility force for a simplicity force, and a Strategy can collapse into a
single parameterized method. Swap a runtime-substitution force for a
build-time one, and a Factory Method can become a static dispatch table.
Reading these two sections together turns the catalogue into a map of nearby
tradeoffs rather than a set of forty-some isolated recipes.

## The rating problem

This book returns to one running example to make the forces concrete: a
shipping company needs a rate quote for a consignment before the customer
commits. A consignment arrives with an origin, a destination, a weight, and a
set of requested services. Producing a number for it means combining several
pieces that each change on their own schedule:

- A base tariff, looked up by zone and weight band, that a carrier publishes
  and revises a few times a year.
- A fuel surcharge, recalculated weekly from a public fuel-price index.
- A peak-season surcharge, active only in defined date windows.
- A residential-delivery surcharge, applied when the destination is not a
  business address.
- A negotiated discount tier, specific to the customer's contract, that a
  sales team can renegotiate without involving engineering.
- Three or four carrier APIs, each shaped differently, that need to be
  compared so the company can quote the cheapest viable option.

Three forces compete over how this gets built:

- **Accuracy.** The quoted number has to match what the carrier actually
  bills. A quote that is wrong costs the company the difference, or costs it
  the customer's trust when the invoice disagrees with the quote.
- **Speed.** The quote has to return inside a checkout flow, typically under a
  second, before the customer abandons the page or a competitor's quote
  arrives first.
- **Changeability.** Surcharge schedules, discount contracts, and carrier
  tariffs change on a schedule set by finance and sales, not by a release
  calendar. A surcharge change that needs a deployment is a change that
  arrives late.

These forces pull against each other directly. Maximizing accuracy argues for
calling every carrier's live rate API and recomputing every surcharge from
the freshest index on every quote. That's also the slowest possible design,
and it fails the speed force the moment a carrier's API is slow or down.
Maximizing speed argues for caching tariffs and precomputing surcharges. A
cache is only as accurate as its last refresh, so this design pushes directly
against the accuracy force the moment a surcharge changes between refreshes.
Maximizing changeability argues for pulling the surcharge logic out of code
entirely, into a table or a small rule language a non-engineer can edit.
Every layer of indirection that buys that flexibility is also a layer a bug
or a misconfigured rule can hide in, which puts both accuracy and the
engineering team's ability to reason about a wrong quote at risk.

None of this is a defect in the problem. It's what a real pricing problem
looks like once you stop assuming one force dominates. The company's actual
answer, at any given time, is a specific compromise. Carrier tariffs are
cached with a short time-to-live and refreshed on a schedule the finance team
controls. Surcharges are evaluated from a small rule set at quote time,
because the inputs are cheap to compute locally. Only the final comparison
across carriers is allowed to make a live call, because that's the one step
accuracy can't tolerate being stale. That compromise is a design decision,
made under these three forces, for this company, at this scale. A different
company, with a smaller catalogue of surcharges or a higher tolerance for a
slightly stale quote, would land somewhere else on the same three axes.

## Forces are local, not global

The three forces don't act with equal strength at every point in the
pipeline that turns a consignment into a quote. They compound differently
stage by stage, and a design that treats the whole pipeline as one force
budget misses that.

At the base-tariff lookup, changeability dominates. A carrier revises its
zone-and-weight table a few times a year, and finance wants that revision
live the same day it's published. Nothing about looking up a number in a
table threatens accuracy or speed, so the lookup can be as data-driven as the
business wants. At the surcharge stage, changeability still dominates, but
now it's in tension with accuracy: a surcharge rule written sloppily enough
to be easy to edit is also sloppy enough to silently mis-price a shipment.
So the design has to buy editability without buying a rule language
expressive enough to hide a mistake. At the final cross-carrier comparison,
the dominant force flips to accuracy. This is the one number the customer
sees, and the one number the invoice gets checked against. A cached or
approximate answer here is unacceptable, even though caching was the right
call two stages earlier.

Treating the pipeline as having one answer, "cache everything" or "call
everything live," ignores that the three stages are answering to different
forces. A workable design assigns a different compromise to each stage and
is explicit about why. That's also what a well-chosen pattern does at the
right granularity. The surcharge-stage structure and the comparison-stage
structure can, and often should, differ, because they're resolving different
forces, not because one is more "advanced" than the other.

## What a pattern actually buys you

Once the forces are named, a structural idea stops being a name to memorize
and becomes an answer to a specific tension. Layering each surcharge as an
independent unit that wraps a base tariff is a direct response to the
changeability force: a rule table can then add or remove a surcharge without
touching the others. Each surcharge becomes something a sales or finance
change can swap, without an engineer rewriting a quote calculation. The
unit's isolation also bounds how far a mistake in one surcharge's logic can
reach into the rest of the price. Whether the eventual shape in code gets
called Decorator or Chain of Responsibility matters far less than why it was
chosen. Changeability was the dominant force where surcharges are applied;
accuracy was dominant where carrier tariffs get compared.

The reflex reading of the catalogue produces a different, worse habit:
"we need to vary behavior, so this needs a Strategy." Applied to the rating
problem without asking which forces are present, that reflex can produce an
interface, three or four concrete pricing classes, and a factory to select
among them. In C#, a single method or a delegate value already expresses the
same computation without any of that structure. The ceremony doesn't resolve
a force that was actually present in the problem. It imitates the textbook
shape because the textbook shape is what the name brings to mind. The cost
is not hypothetical. Every extra type is one more thing a new engineer has
to locate and trust is wired up correctly. It's also a type an AI coding
assistant has to load into context before it can reason about a change,
whether or not that type does any useful work.

This is also where the catalogue's age starts to matter directly. Several of
the forces that once justified a full class hierarchy are resolved today by a
single language feature. That feature covers the need to pass behavior as a
value, to react to an event without hand-rolling a subscriber list, or to
represent the deliberate absence of an object. Whether a force has already
been resolved by the compiler is itself a forces judgment. The question
isn't "does this look like the Strategy pattern," it's "is there still a
flexibility, substitutability, or lifetime force here that a built-in
language construct doesn't already cover."

## Where this lens goes from here

Every chapter past this one that covers a classic pattern starts the same
way: by naming the forces a shipping-domain example presents, before showing
any structure. Where a C# language feature already resolves those forces, the
chapter says so directly and shows the feature, rather than walking through
the exercise of building the pattern's original four or five participant
types for a tension the compiler already handles. Where the forces are
structural, independent of which language expresses them, the chapter keeps
the shape the catalogue describes, because that shape is still the best
answer, not because the catalogue said so.

The rating problem is the thread that carries this discipline forward. Each
pattern chapter that touches pricing, from comparing rate strategies to
layering surcharges to caching a slow carrier lookup, reuses the same
consignment, the same three forces, and the same vocabulary introduced here.
The goal isn't novelty on every page. It's a shared example specific enough
that the forces stay concrete, and recognizable enough that a returning
reader never has to re-learn the domain to follow a new decision.

## Where this leaves you

You can now:

- Define what a force is and point to where a classic pattern writes its
  forces down: the Consequences section, read here as a forces ledger that
  states what a Decorator buys and what it costs in the same breath.
- Name the three forces in the rating problem, accuracy, speed, and
  changeability, and say what pulls each one up and what that costs, using
  the base-tariff, surcharge, and cross-carrier stages where each force
  dominates in turn.
- Explain why a forces reading beats matching a problem to a pattern by name,
  using the Strategy reflex applied to the rating problem: an interface and a
  factory for a tension a C# delegate value already resolves.
- State the question this book asks of every classic pattern before reaching
  for its original structure: not "does this look like the pattern," but "is
  there still a force here a built-in language construct doesn't already
  cover." Figure 1.2 contrasts that question against starting from a pattern
  name.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, *Design Patterns: Elements of
  Reusable Object-Oriented Software* (Addison-Wesley, 1994), is the canonical
  catalogue this book's pattern chapters track, including the Consequences
  sections this chapter reads as a forces ledger.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
