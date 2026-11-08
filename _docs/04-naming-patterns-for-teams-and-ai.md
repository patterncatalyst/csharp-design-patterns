---
title: "Naming patterns for teams and AI assistants"
order: 4
layout: tutorial
sectionid: docs
part: "why-not-java"
description: "Why a shared pattern vocabulary is a review tool for a team and for an AI assistant, and how naming cuts across the consignment lifecycle."
---

## Learning objectives

By the end of this chapter you can:

- Use a pattern name as a checkable statement of design intent in a review.
- State the precondition and postcondition each consignment lifecycle stage, booked, loaded, in transit, cleared, delivered, commits a method to.
- Replace a vague class name with a pattern name or a domain-action name.
- Narrow what an AI coding assistant generates by naming the pattern or stage.
- Keep a name true to its code with an executable check.

## Why this matters

"This class is a Decorator around `IRateQuote`" tells a reviewer three things before they read the body: each wrapper keeps the interface, delegates the call it doesn't change, and adds one responsibility. The reviewer checks those three instead of reconstructing the intent from the implementation.

Domain names do the same job. A method named `MarkCleared` instead of `UpdateStatus(4)` commits to a precondition, customs documentation is validated, and a postcondition, billing may proceed. A reviewer who knows the vocabulary checks both, and so does an AI assistant reading the name.

{% include excalidraw.html name="04-naming-patterns-for-teams-and-ai" caption="Figure 4.1: One pattern name, read the same way by a code reviewer and an AI assistant, carrying the same invariants both times." %}

## Naming as a review tool

Two pull request descriptions for the same change:

| Description | What the reviewer must verify |
|---|---|
| "Wraps the rate calculation to add a fuel surcharge without touching the pricing logic." | From the diff alone, whether the wrapper keeps the interface and adds one responsibility. |
| "Adds a fuel surcharge as a Decorator around `IRateQuote`." | The diff against a known shape: same interface, one added responsibility, delegates the rest. |

Strategy works the same way: naming three interchangeable rate algorithms a Strategy says that swapping one needs no change to the caller. The name doesn't replace reading the diff; it narrows what to verify.

A consistent name is also a search key. Search for Decorator to find every charge layered on a rate quote, instead of tracing every call site that builds an `IRateQuote`. Search for `MarkCleared` to find everything clearance triggers: billing, a notification, an audit entry. A pattern name in a commit message, PR title or comment outlives the person who wrote it, and lets a new team member orient without tracing call sites.

The name has to be accurate. A class labeled Decorator that replaces its wrapped object's behavior is worse than an unlabeled one, because the reviewer trusts a shape that isn't there.

## Naming across the consignment lifecycle

Each lifecycle stage name carries a precondition and a postcondition, the way a pattern name carries a structural contract.

| Stage | Assumes | Leaves true |
|---|---|---|
| Booked | A rate quote has been accepted | A tracking number exists; no transit event has posted yet |
| Loaded | Booked | The manifest matches the declared weight; a carrier vehicle is assigned |
| In transit | Loaded | At least one location update has posted since loading |
| Cleared | In transit, at the destination customs point | Customs documentation is validated; billing may proceed |
| Delivered | Cleared | Proof of delivery is recorded; no further transit event is accepted |

`status == 4` means nothing to a reviewer until they find where 4 is defined, and nothing to an AI assistant reading the file alone. `MarkCleared` turns the code into a claim the method body can be checked against: does it require an in-transit consignment at the destination customs point, or accept any prior state?

## Picking names a reviewer can trust

`RateManager`, `ConsignmentHelper` and `DocumentProcessor` say which area some operations belong to, and nothing about what they assume or guarantee. The reviewer reads the whole class. The fix is a more specific name:

| Vague name | What it does | Better name |
|---|---|---|
| `RateManager` | Applies a fuel surcharge on top of a base quote | A Decorator, named as one |
| `ConsignmentHelper` | Validates a manifest against the booked weight | `ValidateManifestAgainstBooking` |
| `DocumentProcessor` | Assembles a customs declaration, filling fields by destination | A Template Method, which tells the reviewer which steps a destination-specific subclass overrides and which stay fixed |

## When a local name beats a catalog name

A forced catalog name sets up an expectation the code doesn't meet. A `ManifestValidator` that checks a loaded consignment's weight against its booking isn't a Strategy, a Visitor or a Chain of Responsibility. It's one validation step with one precondition and one postcondition, and the domain name says all of that. A name passes if it states a precondition and postcondition a reader can check against the code. An interface or factory method added only to make a GoF label fit gives the code the pattern's machinery without its benefit.

## Steering an AI assistant with a shared vocabulary

An assistant is trained on code where pattern names and domain vocabulary correlate with particular shapes.

| Request | What the assistant can produce |
|---|---|
| "Make the rate calculation add an extra charge somewhere." | An inline conditional, a second calculator class, a mutated field on the original quote, or a dozen other shapes. |
| "Add a fuel surcharge as a Decorator around `IRateQuote`." | A wrapper that keeps the interface and adds one charge. |
| "Write the guard clause for the transition into Cleared." | A check for in transit at the destination customs point. |

A name the assistant attaches to its own output is a claim. Hold a generated "Decorator" to the same three checks as a human's. A generated `MarkCleared` that skips the customs check is a mismatch whoever wrote it.

Naming can overshoot. Asked for a Decorator, an assistant may generate a new interface, a wrapper class and a constructor taking the wrapped instance, where the codebase's convention for that shape is one delegate field. The name gives the contract, not the C# mechanism that meets it most directly. A reviewer who knows both the pattern and the team's idiom catches the oversized version, as they'd catch a hand-rolled loop where one LINQ call does the job.

## When a name stops matching the code

If `MarkCleared` sends a billing event with no customs check, or a consignment reaches `Loaded` before `Booked` validation has run, every reviewer and assistant that relies on the name inherits the mismatch. Make `MarkCleared` throw for a consignment short of the destination customs point, and keep a test that fails if it stops throwing. A convention held only in reviewers' memories erodes as the team turns over; an executable check keeps the name and the behavior aligned after the people who agreed on it have moved on. The name states the invariant; a test keeps it true.

## For deeper coverage

- Gamma, Helm, Johnson, and Vlissides built the pattern catalog the naming argument borrows from: each entry lists a pattern's name and intent before its structure, by design.
- Martin's *Clean Code* and McConnell's *Code Complete* both treat a name as a design decision worth the same scrutiny as the code it labels.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
