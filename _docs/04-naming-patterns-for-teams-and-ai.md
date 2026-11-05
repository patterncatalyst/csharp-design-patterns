---
title: "Naming patterns for teams and AI assistants"
order: 4
layout: tutorial
sectionid: docs
part: "why-not-java"
description: "Why a shared pattern vocabulary is a review tool for a team and for an AI assistant, and how naming cuts across the consignment lifecycle."
---

## Learning objectives

- State why a pattern name functions as compressed, checkable design intent, rather than decoration on top of the code.
- Trace one naming scheme, booked, loaded, in transit, cleared, delivered, across the consignment lifecycle. State the precondition and postcondition each name commits a method to.
- Distinguish a name that describes structure (a Gang of Four pattern name) from one that describes a domain state transition. Explain why both do the same review job.
- Use a pattern or lifecycle-stage name to narrow what an AI coding assistant generates from an otherwise underspecified request.
- Recognize when a name has stopped matching the code it labels. State why that failure costs a reviewer and an assistant in the same way.

## Why this matters

A code review runs faster when the reviewer and the author already agree on what a piece of code is supposed to be. Take the claim "this class is a Decorator around `IRateQuote`." Before reading a line of the body, it tells a reviewer that each wrapper keeps the same interface, delegates the call it does not change, and adds exactly one responsibility. The reviewer checks those three things instead of re-deriving what the author meant from the implementation. The Gang of Four catalog gets read, often, as a list of implementation templates. Its more durable contribution to a working team is the opposite: a small, stable vocabulary that compresses a design decision into a name two people, or a person and a tool, can agree on without reading the whole method.

That compression is not limited to the twenty-three names in the catalog. A domain has its own vocabulary, usually less examined, and it carries the same weight. In the shipping domain this book uses throughout, a consignment moves through booked, loaded, in transit, cleared, and delivered. A method named `MarkCleared` instead of `UpdateStatus(4)` commits to a specific precondition, customs documentation has been validated, and a specific postcondition, billing may now proceed. A reviewer who knows the vocabulary checks both without being told. The same compression that makes "Decorator" useful in a review makes "Cleared" useful in one. An AI assistant reading either name benefits from it the same way a human reviewer does.

{% include excalidraw.html name="04-naming-patterns-for-teams-and-ai" caption="Figure 4.1: One pattern name, read the same way by a code reviewer and an AI assistant, carrying the same invariants both times." %}

## Naming as a review tool

A name does real work when it lets a reviewer stop reading. Compare two pull request descriptions for the same change. One reads "wraps the rate calculation to add a fuel surcharge without touching the pricing logic." The other reads "adds a fuel surcharge as a Decorator around `IRateQuote`." The first sentence is accurate and still leaves the reviewer to check, by reading the diff, whether the wrapper preserves the interface and adds only one responsibility. The second sentence makes those two properties part of the claim itself. A reviewer who knows what Decorator means checks the diff against a known shape instead of inferring the shape from scratch. The same effect runs the other way for Strategy. Naming three interchangeable rate algorithms as a Strategy tells a reviewer, before looking, that swapping one for another requires no change to the calling code. Neither name replaces reading the diff. Both narrow what the reviewer has to verify.

A named pattern is also a search key. A reviewer who wants to see every place the team layered a cross-cutting charge on top of a rate quote can search the codebase for Decorator and find every wrapper at once. That beats tracing every call site that constructs an `IRateQuote` to see which ones add something on top. The domain vocabulary works the same way. Searching for `MarkCleared` across the codebase finds every place the team decided that a consignment's customs clearance should trigger something else, billing, a notification, an audit entry, in one query. A codebase that names its patterns and its domain transitions consistently turns a design question, such as where customs clearance gets handled, into a text search. It replaces a guided tour from whoever remembers.

This matters more as a team grows past the size where everyone remembers every design conversation. A commit message, a pull request title, or a comment that names a pattern survives the person who wrote it. A new team member who knows the catalog can orient on unfamiliar code faster by recognizing a name in a comment than by tracing every call site. The cost of this precision is that the name has to be accurate. A class labeled Decorator that actually replaces its wrapped object's behavior instead of adding to it is worse than an unlabeled class. It spends a reviewer's trust on a shape that is not there.

## Naming across the consignment lifecycle

The same discipline applies below the pattern catalog, at the level of the domain's own vocabulary. A consignment in this book's shipping domain moves through five named stages. Each stage name carries a precondition and a postcondition the way a pattern name carries a structural contract.

| Stage | Assumes | Leaves true |
|---|---|---|
| Booked | A rate quote has been accepted | A tracking number exists; no transit event has posted yet |
| Loaded | Booked | The manifest matches the declared weight; a carrier vehicle is assigned |
| In transit | Loaded | At least one location update has posted since loading |
| Cleared | In transit, at the destination customs point | Customs documentation is validated; billing may proceed |
| Delivered | Cleared | Proof of delivery is recorded; no further transit event is accepted |

A method named `MarkCleared` is doing the same job as a class named Decorator. It tells a reader which rule it enforces without the reader re-deriving the rule from a conditional three calls deep. A status field typed as a generic string or integer code, rather than a set of named stages, loses this entirely. `status == 4` means nothing to a reviewer until they go find where 4 is defined. It means nothing to an AI assistant reading the file in isolation either. Naming the stage is what turns an opaque code into a claim a reader can check against the method body. Does `MarkCleared` require an in-transit consignment at the destination customs point before it runs, or does it accept any prior state? The name states the question. The implementation answers it.

## Picking names a reviewer can trust

A name that does not commit to a specific precondition or postcondition has stopped doing this job. `RateManager`, `ConsignmentHelper`, and `DocumentProcessor` are common shapes in codebases that haven't thought about naming as a review tool. Each one tells a reader that some operations related to rates, consignments, or documents live inside, and nothing about what those operations assume or guarantee. A reviewer facing a class named `RateManager` has to read the whole class to find out what it does. That's the cost a well-chosen name is supposed to remove.

The fix is rarely a cleverer name. It's a more specific one.

- `RateManager` that only ever applies a fuel surcharge on top of a base quote is a Decorator. Naming it that way states the contract directly.
- A `ConsignmentHelper` that only ever validates a manifest against a booked weight is a validation step in the lifecycle. Naming it `ValidateManifestAgainstBooking` states the precondition and the check in the same identifier.
- A `DocumentProcessor` that only ever assembles a customs declaration, varying which fields it fills in by destination, is a Template Method. Keeping that name attached to it tells a reviewer which steps a destination-specific subclass overrides, and which ones stay fixed for every destination.

## When a local name beats a catalog name

Not every class needs a name from the Gang of Four catalog. Reaching for one where none fits causes the same damage as a vague name: it sets up an expectation the code doesn't satisfy. A `ManifestValidator` that checks a loaded consignment's weight against its booking is not a Strategy, a Visitor, or a Chain of Responsibility. It is a single validation step with one precondition and one postcondition. Naming it after the domain action it performs tells a reviewer everything a catalog name would, with no catalog mapping to get wrong. The test stays the same one that applies to any other name: does it state a precondition and a postcondition a reader can check against the implementation? A domain name that passes that test is doing its job. Forcing a GoF label onto a class that doesn't have the matching structure gives the code the catalog pattern's machinery without its benefit. An extra interface here, a factory method there, gets added only to make the label fit. A reviewer who trusts the label inherits a shape the code never has.

## Steering an AI assistant with a shared vocabulary

An AI coding assistant is trained on a large volume of code where pattern names and domain vocabulary correlate reliably with a particular shape. A request that names the shape, "add a fuel surcharge as a Decorator around `IRateQuote`," or "write the guard clause for the transition into Cleared," gives the assistant the same compressed information it gives a human reviewer. The assistant responds with code closer to the team's actual convention, because the name rules out alternatives the team never wanted considered. An unnamed request, "make the rate calculation add an extra charge somewhere," is compatible with a dozen shapes: a new conditional inline, a second calculator class, a mutated field on the original quote. The assistant has to guess which one a given team prefers. Naming the pattern or the lifecycle stage removes the guess.

The same effect runs in reverse. If an assistant proposes a class and labels it a Decorator in a comment or in generated documentation, a reviewer holds it to the same three checks a human author's claim would invite. Same interface, one added responsibility, delegates the rest. A name an assistant attaches to its own output is a claim, not a guarantee. Checking a claim against the code it labels costs less than reading the code cold. A generated `MarkCleared` method that doesn't check the consignment is in transit at the destination customs point is a name mismatched to its implementation, regardless of who or what wrote it. The mismatch is exactly as costly either way. The discipline runs both directions. A team that names its domain vocabulary precisely gets generated code that fits the convention more often. A team that checks generated code against the name attached to it catches the cases where the shape and the label disagree before they reach production.

A named request narrows an assistant's output toward a known shape, and that narrowing can overshoot. An assistant asked to add a fuel surcharge "as a Decorator" sometimes generates the full apparatus: a new interface, a concrete wrapper class, a constructor that takes the wrapped instance. This codebase's own convention for the same shape is a single delegate field. Naming the pattern tells the assistant what contract to honor. It doesn't tell the assistant which C# mechanism satisfies that contract most directly. A reviewer who knows both the pattern name and the idiom the team uses for it catches the oversized version before it merges. That's the same way a reviewer catches a hand-rolled loop where one LINQ call would do the same job.

## When a name stops matching the code

A name has a failure mode. If `MarkCleared` quietly sends a billing event with no customs check, or a consignment sometimes reaches `Loaded` before `Booked` validation has run, the name no longer matches what the code does. Every reviewer and every assistant that relies on it inherits the mismatch. Naming discipline is only worth the compression it buys when the invariant it promises is tested, not merely implied by a well-chosen word. A test that asserts `MarkCleared` throws on a consignment that has not reached the destination customs point keeps the name and the behavior aligned. Without it, the vocabulary degrades one mismatch at a time until nobody trusts a name enough to skip reading the implementation. That's the benefit naming was supposed to provide in the first place.

The cost falls hardest on the reader who trusts the name enough to skip the body. That's the reader the vocabulary was built to serve. A convention that lives only in reviewers' memories erodes as a team turns over. An executable check keeps the same rule alive: a test that fails when `MarkCleared` runs on a consignment short of the destination customs point. That check holds the name to its promise after the people who first agreed on it have moved on. Naming and testing are the same discipline seen from two sides. The name states the invariant a reader relies on, and the test is what keeps that statement true as the code around it changes.

## Where this leaves you

You can now:

- Read a name like "Decorator around `IRateQuote`" as a compressed claim, same interface, one added responsibility, delegates the rest, that a reviewer checks against the diff instead of re-deriving from scratch.
- Trace a consignment through booked, loaded, in transit, cleared, and delivered, and state the precondition and postcondition each stage commits a method like `MarkCleared` to.
- Tell a structural name (`Decorator`, `Template Method`) apart from a domain-state name (`MarkCleared`, `ValidateManifestAgainstBooking`), and explain why both state a precondition and a postcondition a reader can check against the implementation.
- Put a pattern or lifecycle-stage name in a request to an AI assistant, "add a fuel surcharge as a Decorator around `IRateQuote`," to rule out the shapes the team never wanted.
- Spot a name that no longer matches its code, a `MarkCleared` that sends a billing event with no customs check, and say why that mismatch costs a reviewer and an AI assistant the same way.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides built the pattern catalog this chapter borrows its naming argument from. Each entry lists a pattern's name and intent before its structure, by design.
- Martin's *Clean Code* and McConnell's *Code Complete* both treat a name as a design decision worth the same scrutiny as the code it labels.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
