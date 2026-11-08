---
title: "Patterns as decisions under forces"
order: 1
layout: tutorial
sectionid: docs
part: "why-not-gof"
description: "Reframing the catalogue from a taxonomy to a set of decisions made under competing forces, with shipment rating as the running example."
---

## Learning objectives

By the end of this chapter you can:

- Define a *force* and find where a classic pattern description writes its
  forces down.
- Name the three forces in a shipping rate quote (accuracy, speed,
  changeability) and what each one costs.
- Pick a structure per pipeline stage from the force that dominates there,
  instead of matching the problem to a pattern by name.
- Tell a force that still needs a structure from one a C# language feature
  already resolves.

## Why this matters

A team that skims the Gang of Four catalog reads it as a lookup table: a
"creation" problem gets a Factory, a "varying behavior" problem gets a Strategy.
Each entry's one-line intent, name and class diagram make that easy.

The catalog defines a pattern differently. Its introduction borrows from
Christopher Alexander: a pattern names a recurring problem, the *forces* in
tension around it, and a solution that balances them in a context. A force is a
pressure on the design, such as toward flexibility, faster execution, fewer
moving parts, or tolerance for a requirement nobody has written yet.

Some forces that justified a full class-and-interface structure in 1994 are
resolved today by one C# feature, such as a delegate variable or a nullable
annotation. Matching by name can't see that. Asking which force is present can.

{% include excalidraw.html name="01-patterns-as-decisions-under-forces" caption="Figure 1.1: The rating problem as three forces, accuracy, speed, and changeability, pulling against a single rate-quote decision." %}

<!-- SECOND FIGURE SPEC: a two-panel comparison diagram, "Forces first" vs "Pattern first." Left panel: a design process starting from the three forces (accuracy, speed, changeability) as labeled pressure arrows converging on a decision point labeled "rate-quote design," with a small annotation that the chosen shape (rule pipeline, cache, data table) falls out of which forces dominate. Right panel: a design process starting from a pattern name in a box ("Strategy") applied directly to the same problem, arrow straight to a four-class structure (interface + three implementations + factory), with no force annotations, ending in a box labeled "ceremony, forces unexamined." Caption should read: "Figure 1.2: Starting from forces versus starting from a pattern name, applied to the same rate-quote decision." -->
{% include excalidraw.html name="01-forces-first-vs-pattern-first" caption="Figure 1.2: Starting from forces versus starting from a pattern name, applied to the same rate-quote decision." %}

## Forces, not a taxonomy

Every Gang of Four entry has the same sections: Intent, Motivation,
Applicability, Structure, Participants, Collaborations, Consequences,
Implementation, Known Uses, Related Patterns. Consequences is the forces ledger.
It states what the pattern buys and what it costs.

| Section | What it tells you |
|---|---|
| Consequences | The benefit and the cost together. Decorator adds responsibilities at runtime in any combination without a subclass per combination; it costs many small look-alike objects and a caller that can't see which decorators are in effect. |
| Known Uses | Systems that already chose this trade-off: evidence the forces line up this way often. |
| Related Patterns | The neighboring compromise once one force shifts. Trade flexibility for simplicity and a Strategy collapses into a parameterized method; trade runtime substitution for build-time and a Factory Method becomes a static dispatch table. |

Two forces rarely point the same way. A pricing system wants flexibility (any
combination of surcharges) and predictability (one engineer reads a stack trace
and knows the final price). A pattern records a trade-off that has worked for one
shape of problem, with the costs named up front.

## The rating problem

A shipping company quotes a price for a consignment before the customer
commits. The consignment has an origin, a destination, a weight and requested
services. The quote combines parts that change on their own schedules:

| Input | Changes |
|---|---|
| Base tariff by zone and weight band | When the carrier republishes, a few times a year |
| Fuel surcharge from a public fuel-price index | Weekly |
| Peak-season surcharge | Active only in set date windows |
| Residential-delivery surcharge | Per consignment, when the destination isn't a business |
| Negotiated discount tier | When sales renegotiates a contract, with no engineering involved |
| Three or four carrier rate APIs, each shaped differently | When the carrier changes its API |

Three forces compete:

| Force | Requirement | Maximized alone, it gives | Which costs |
|---|---|---|---|
| **Accuracy** | The quote matches what the carrier bills. | A live call to every carrier API and a fresh surcharge on every quote. | Speed: the quote fails when a carrier API is slow or down. |
| **Speed** | The quote returns in a checkout flow, typically under a second. | Cached tariffs and precomputed surcharges. | Accuracy: a cache is as accurate as its last refresh. |
| **Changeability** | Finance and sales change schedules without a release. | Surcharge logic in a table or a small rule language. | Accuracy and debuggability: each layer of indirection can hide a bug or a bad rule. |

A workable answer is a specific compromise. Carrier tariffs are cached with a
short time-to-live on a refresh schedule finance controls. Surcharges are
evaluated from a small rule set at quote time, because their inputs are cheap to
compute locally. Only the final cross-carrier comparison makes a live call,
because that's the one step that can't be stale. A company with fewer surcharges
or more tolerance for a stale quote lands elsewhere on the same three axes.

## Forces are local, not global

The forces weigh differently at each pipeline stage, so one policy for the whole
pipeline ("cache everything", "call everything live") is wrong somewhere.

| Stage | Dominant force | Consequence for the design |
|---|---|---|
| Base-tariff lookup | Changeability | Finance wants a revised table live the day it's published, and a table lookup threatens neither accuracy nor speed, so make it fully data-driven. |
| Surcharges | Changeability, against accuracy | A rule loose enough to edit easily can silently mis-price a shipment. Buy editability without a rule language expressive enough to hide a mistake. |
| Cross-carrier comparison | Accuracy | The customer sees this number and the invoice is checked against it. No cache, even though caching was right two stages earlier. |

The structure at the surcharge stage and the one at the comparison stage differ
because they resolve different forces.

## What a pattern buys you

At the surcharge stage, wrap the base tariff in one independent unit per
surcharge. A rule table can then add or remove a surcharge without touching the
others, which answers changeability, and each unit's isolation limits how far a
mistake in one surcharge reaches into the price. Whether the code calls that
Decorator or Chain of Responsibility matters less than the reason it was chosen.

Name-matching gives a different result: "we need to vary behavior, so this needs
a Strategy." Applied to the rating problem, that yields an interface, three or
four pricing classes and a factory, for a computation a single method or a
delegate value expresses in C#. Each extra type is one more thing a new engineer
has to find and confirm is wired correctly, and one more file an AI coding
assistant loads into context before it can reason about a change.

Language features now resolve several forces that once needed a class hierarchy:

| Force | 1994 structure | C# feature |
|---|---|---|
| Pass behavior as a value | Strategy or Command interface plus classes | Delegate, lambda |
| React to an event | Hand-rolled subscriber list | `event` |
| Represent a deliberate absence | Null Object class | Nullable annotation (`T?`) |

The question for each pattern is whether a flexibility, substitutability or
lifetime force remains that a built-in construct doesn't cover.

## The question for every pattern

Each classic pattern starts from the forces a shipping-domain example presents.
Where a C# feature resolves them, use the feature instead of building the
pattern's four or five participant types. Where the forces are structural, in any
language, keep the catalog's shape, because it's still the best answer.

The rating problem carries through the pricing patterns, from comparing rate
strategies to layering surcharges to caching a slow carrier lookup, with the same
consignment and the same three forces.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides, *Design Patterns: Elements of
  Reusable Object-Oriented Software* (Addison-Wesley, 1994), is the canonical
  catalog the pattern chapters track, including the Consequences sections
  read here as a forces ledger.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
