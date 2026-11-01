---
title: "Tester-Doer and the Result debate"
order: 31
layout: tutorial
sectionid: docs
part: "platform-patterns"
description: "The Framework Design Guidelines convention against the community Result<T>, taught together, with the tradeoff stated rather than a side picked."
---

<!-- EXAMPLE (shipping domain): Parsing an EDI message and validating a declaration. -->

## Learning objectives

By the end of this chapter you can:

- Parse the same malformed EDI text through `EdiTryParser.TryParse` and
  explain why it reports failure through a boolean return and an `out`
  parameter rather than an exception, even though the text is unusable.
- Call `DeclarationProcessor.CanProcess` before `DeclarationProcessor.Process`
  and state the Tester-Doer atomicity caveat: nothing stops a declaration, or
  the thread calling into it, from changing between the Tester call and the
  Doer call.
- Build a `Result<CustomsDeclaration>` with `DeclarationValidator.Validate`,
  read a failure's `Error` without ever catching an exception, and chain a
  success through `Result<T>.Map` without an intermediate branch.
- Run `EdiTryParser.TryParse` and `EdiResultParser.Parse` against the same
  four inputs and explain why `Both_styles_agree_on_whether_an_input_is_valid`
  expects their verdicts, a `bool` and a `Result<T>.IsSuccess`, to match on
  every one of them.
- State the tradeoff between a boolean-and-`out` return, a Tester-Doer pair,
  and `Result<T>`, without treating any one of the three as the correct
  default.

## Why this matters

A customs declaration arrives with a malformed commodity code often enough
that handling the bad case is part of the job, not an exception to it. An EDI
message truncated mid-transfer, a declared value typed as zero, an origin
country left blank: every one of these is a routine outcome a documentation
pipeline is built to expect, not a surprise that interrupts it. The question
this chapter answers is not whether to handle that outcome, every shape
covered here handles it, but which shape a method uses to report it to its
caller.

.NET gives that question more than one accepted answer. The Framework Design
Guidelines name two conventions for exactly this situation: `Try...` methods
that return a `bool` and hand back their result through an `out` parameter,
and a Tester-Doer pair where one method answers a yes-or-no question and a
second method acts on a yes. A `Result<T>` type that wraps a success-or-failure
outcome as a single returned value, with no `out` parameter and no exception
anywhere in the ordinary path, has become common in C# codebases without ever
joining the Framework Design Guidelines itself. All three report the same
fact, an operation that can fail in an expected way succeeded or did not,
through a different shape. A reader who only knows one of the three
misreads code written in the others, and a codebase that mixes them without a
convention makes every call site a small decision about which contract it is
honoring.

{% include excalidraw.html name="31-tester-doer-and-the-result-debate" caption="Figure 31.1: The same parse-and-validate decision appears twice: Try-Parse style methods report failure through a boolean, while Result wraps the same outcome as a value a caller cannot ignore." %}

## An EDI message and a declaration, two gates to clear

The example behind this chapter lives in
`examples/31-tester-doer-and-the-result-debate`, namespace `DeclarationParsing`.
It has two routine points of expected failure, one on the way in and one on
the way out. Parsing turns raw EDI text into structured segments:

```csharp
public sealed record EdiSegment(string Tag, IReadOnlyList<string> Elements);

public sealed record EdiMessage(string MessageType, IReadOnlyList<EdiSegment> Segments);
```

An EDI message in this example is one segment per line, `Tag*Element1*
Element2`, a tag followed by zero or more pipe-free elements. `UNH*1*CUSDEC`
is a well-formed segment; a line with no tag before its first element,
including a blank line, is not. Validation turns a parsed declaration into
one ready for the duty office, or rejects it:

```csharp
public sealed record CustomsDeclaration(
    string Reference,
    string CommodityCode,
    decimal DeclaredValue,
    string OriginCountry);

public sealed record ProcessedDeclaration(string Reference, decimal DeclaredValue);
```

A declaration is well-formed when its reference and origin country are not
blank, its declared value is positive, and its commodity code is a six-digit
Harmonized System number. Both checks, the parse and the validation, fail
routinely and for reasons a caller needs to act on, not merely detect. Every
type in this chapter answers the same two questions for one check or the
other: did it succeed, and if not, why.

## The Try-Parse convention: a boolean and an out parameter

`EdiTryParser.TryParse` is the Framework Design Guidelines' Try-Parse
pattern applied to EDI text. It returns `true` and populates its `out`
parameter on success, and returns `false` with that parameter left `null` on
any malformed input, never throwing for text that merely fails to parse:

```csharp
public static bool TryParse(string raw, out EdiMessage? message)
{
    ArgumentNullException.ThrowIfNull(raw);

    message = null;
    if (string.IsNullOrWhiteSpace(raw))
    {
        return false;
    }

    var lines = raw.Trim().Split('\n');
    var segments = new List<EdiSegment>(lines.Length);

    foreach (var line in lines)
    {
        if (!TryParseSegment(line, out var segment) || segment is null)
        {
            return false;
        }

        segments.Add(segment);
    }

    message = new EdiMessage(segments[0].Tag, segments);
    return true;
}
```

`TryParse` calls a smaller `TryParseSegment` once per line, itself returning
`bool` and populating an `out EdiSegment?`, so the same convention nests:
a line that fails to parse makes the whole message fail to parse, each
failure reported the same way at its own scope. `ArgumentNullException.
ThrowIfNull(raw)` is the one exception this method does throw, and it throws
for a reason the Try-Parse pattern treats as entirely separate from the
pattern's own job: a `null` argument is a programming error, the caller
violated the method's contract before the parse ever started, while empty
text, a blank line, or a segment with no tag is a value the method is built
to receive and reject without raising anything.
`TesterDoerTests.TryParse_does_not_throw_on_expected_failure` checks exactly
that distinction by passing a string with a tagless segment, text that will
never parse, and asserting `Record.Exception` around the call returns `null`.
The boolean return is the whole story a caller needs: check it, and only read
`message` when it is `true`.

## The Tester-Doer convention: ask first, then act

`DeclarationProcessor` applies the Framework Design Guidelines' Tester-Doer
pattern to validating a declaration. `CanProcess` is the Tester, a pure
yes-or-no question with no side effect, and `Process` is the Doer, which
assumes the Tester already ran and passed:

```csharp
public bool CanProcess(CustomsDeclaration declaration)
{
    ArgumentNullException.ThrowIfNull(declaration);

    return !string.IsNullOrWhiteSpace(declaration.Reference)
        && IsWellFormedCommodityCode(declaration.CommodityCode)
        && declaration.DeclaredValue > 0m
        && !string.IsNullOrWhiteSpace(declaration.OriginCountry);
}

public ProcessedDeclaration Process(CustomsDeclaration declaration)
{
    ArgumentNullException.ThrowIfNull(declaration);

    if (!CanProcess(declaration))
    {
        throw new InvalidOperationException(
            $"Declaration '{declaration.Reference}' was not tested with {nameof(CanProcess)} " +
            "before processing, or it failed that test.");
    }

    return new ProcessedDeclaration(declaration.Reference, declaration.DeclaredValue);
}
```

Neither method throws for a declaration that is merely invalid.
`CanProcess` reports that with its `false` return, exactly like
`EdiTryParser.TryParse`'s boolean. `Process` throws
`InvalidOperationException` only when a caller skips the Tester, or calls
`Process` on a declaration that already failed it, and that throw reports a
programming error rather than an expected outcome: a caller that calls
`Process` is expected to have already called `CanProcess` and acted on a
`true` result. `TesterDoerTests.Process_after_a_passing_tester_succeeds`
runs that sequence end to end, asserting `CanProcess` first and then reading
the `ProcessedDeclaration` `Process` returns, while
`Process_without_testing_throws_on_invalid_input` confirms calling `Process`
directly on a declaration with a blank reference throws rather than quietly
returning a half-built result.

The shape carries a caveat the Framework Design Guidelines are explicit
about, and this example's naming keeps it visible: a Tester call and a Doer
call are two separate method calls, not one atomic operation. Nothing in
`CanProcess` holds a lock on the declaration it just approved, so another
thread, or another step in a longer pipeline, can change that declaration, or
the state it depends on, in the gap between the two calls. A reference that
`CanProcess` found non-blank a moment ago can be mutated to blank before
`Process` runs against the same object, and `Process` would then throw where
the caller had every reason, from the Tester's own answer, to expect success.
The Tester-Doer pattern answers a question about a snapshot in time; nothing
about calling the Tester locks that snapshot in place for the Doer that
follows it.

## The community `Result<T>`: a value that carries the reason

`Result<T>` is a shape that is not part of the Framework Design Guidelines
but has become a familiar idiom across C# libraries and application code for
the same job a `Try...` method or a Tester-Doer pair does: reporting an
operation that can fail in an expected way, without an exception anywhere in
the ordinary path. Instead of a `bool` plus an `out` parameter, or a pair of
calls, a `Result<T>` is a single value a caller receives back and inspects:

{% raw %}
```csharp
public readonly record struct Result<T>
{
    public bool IsSuccess { get; }

    public T? Value { get; }

    public string? Error { get; }

    public static Result<T> Success(T value) => new(true, value, null);

    public static Result<T> Failure(string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(false, default, error);
    }

    public Result<TOut> Map<TOut>(Func<T, TOut> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        return IsSuccess
            ? Result<TOut>.Success(selector(Value!))
            : Result<TOut>.Failure(Error!);
    }
}
```
{% endraw %}

A private constructor leaves `Success` and `Failure` as the only two ways to
build one, so a `Result<T>` is always in exactly one of those two states, and
`IsSuccess` is the only way a caller finds out which. `EdiResultParser.Parse`
reports the same parse failures `EdiTryParser.TryParse` does, through this
shape instead:

{% raw %}
```csharp
public static Result<EdiMessage> Parse(string raw)
{
    ArgumentNullException.ThrowIfNull(raw);

    if (string.IsNullOrWhiteSpace(raw))
    {
        return Result<EdiMessage>.Failure("The EDI message is empty.");
    }

    var lines = raw.Trim().Split('\n');
    var segments = new List<EdiSegment>(lines.Length);

    foreach (var line in lines)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return Result<EdiMessage>.Failure("A segment line is blank.");
        }

        var parts = line.Split('*');
        var tag = parts[0].Trim();
        if (tag.Length == 0)
        {
            return Result<EdiMessage>.Failure($"Segment '{line}' has no tag.");
        }

        var elements = new string[parts.Length - 1];
        for (var i = 1; i < parts.Length; i++)
        {
            elements[i - 1] = parts[i].Trim();
        }

        segments.Add(new EdiSegment(tag, elements));
    }

    return Result<EdiMessage>.Success(new EdiMessage(segments[0].Tag, segments));
}
```
{% endraw %}

Where `EdiTryParser` returns `false` for every malformed line without saying
which rule the line broke, `EdiResultParser.Parse` returns a `Failure` naming
the specific reason, "A segment line is blank" or "Segment '*1*2' has no
tag", a string the `out` parameter's shape has nowhere to carry.
`DeclarationValidator.Validate` does the same for a `CustomsDeclaration`,
checking the reference, commodity code, declared value, and origin country
in order and returning the first `Failure` with a message naming that field,
or a `Success` wrapping the declaration unchanged.
`ResultTests.Result_validate_reports_the_specific_validation_failure` is a
`Theory` over all four ways a declaration can fail, asserting each one
produces its own distinct `Error` text.

`Result<T>.Map` is the one capability neither `TryParse` nor `CanProcess`/
`Process` offers directly: a way to chain an operation onto a result without
an intermediate branch. `Map` applies its selector to `Value` when
`IsSuccess` is `true` and returns a new `Result<TOut>` wrapping the
transformed value; when `IsSuccess` is `false`, `Map` returns a new
`Result<TOut>.Failure` carrying the same `Error`, never calling the selector
at all. `ResultTests.Result_map_transforms_a_success_and_short_circuits_a_
failure` checks both halves on a plain `int`, doubling a successful `21`
into `42` and confirming a `Failure`'s `"no value"` message survives a `Map`
call untouched. A chain of several `Map` calls reads top to bottom as the
sequence of steps a declaration goes through, with no `if (!ok) return`
between any two of them; the branch that a `TryParse` caller writes by hand
after every call is the one thing a `Result<T>` chain never needs to repeat.

## Comparing the three styles

`ResultTests.Both_styles_agree_on_whether_an_input_is_valid` is a `Theory`
over four EDI inputs, the well-formed message, the one with a tagless
segment, an empty string, and a whitespace-only string, and it asserts that
`EdiTryParser.TryParse`'s `bool` return and `EdiResultParser.Parse`'s
`IsSuccess` agree on every single one. That test is the chapter's thesis in
one assertion: `TryParse` and `Parse` are not two different answers to
whether a message is well-formed, they are one answer reported through two
different shapes. The same holds between `DeclarationProcessor.CanProcess`
and `DeclarationValidator.Validate`, each checking the same four fields of a
`CustomsDeclaration` in the same order.

{% include excalidraw.html name="31-try-parse-vs-result" caption="Figure 31.2: A boolean and an out parameter force a caller to branch immediately, while a Result value carries its own reason and can be composed before any branching happens. Neither shape wins outright." %}

The difference is entirely in what a caller gets to do with the answer. A
`bool` plus an `out` parameter is the cheapest shape to produce and the
cheapest to read at a single call site: an `if` on the return value, a read
of the `out` variable inside it, nothing allocated beyond the `EdiMessage` or
`EdiSegment` the parse already needed to build. It carries no explanation
for a `false`, and it does not compose; chaining a second `TryParse`-shaped
step onto the result of a first one means writing the branch again by hand.
A Tester-Doer pair costs a second method call and the atomicity caveat
covered above, exposed as the price of letting a caller ask "would this
work" without committing to it, a question a plain `TryParse` cannot answer
without attempting the parse itself. A `Result<T>` costs a type to
define, or a library to depend on, and a value that must be unwrapped before
its payload is usable, `Value` stays an unchecked read if a caller forgets
`IsSuccess`, in exchange for a specific failure reason and composability
through `Map` that neither of the other two shapes offers.

None of the three is a strictly better version of the others; each is the
right answer at a different point in the tradeoff between call-site cost,
explanatory power, and the opportunity to compose. A parser called once per
request at the edge of a pipeline, where the only thing a caller does with a
failure is log it and stop, gets little from `Result<T>`'s extra ceremony
over a `TryParse` call it already knows how to read. A validation pipeline
chaining several field checks before a declaration reaches the duty office,
where the failure's specific reason has to reach a user interface or an
audit log untouched, gets real value from a `Result<T>` that carries that
reason through every step without a branch at each one. The right choice
depends on what the caller needs from the answer, not on which shape is
newer or more fashionable.

{% include excalidraw.html name="31-result-composition" caption="Figure 31.3: Result's Map chains a success through each step and short-circuits on the first failure, while the boolean-and-out shape forces a caller to branch immediately after every call." %}

## Build, run, observe

The example is standalone-testable from its own directory:

```sh
cd examples/31-tester-doer-and-the-result-debate
dotnet test
```

It is also picked up, alongside every other example, by the repo-level
`scripts/verify.sh`. Observed locally on Fedora with the .NET 10 SDK:

```
Test run summary: Passed!
  total: 23
  failed: 0
  succeeded: 23
  skipped: 0
  duration: 699ms
```

The twenty-three cases split across two test classes and twelve named test
methods, four of them `Theory` methods expanding into multiple cases.
`TesterDoerTests` covers the Framework Design Guidelines side: five `Fact`
methods check a well-formed parse, the no-throw guarantee on malformed text,
a passing Tester, a successful Tester-then-Doer sequence, and a Doer call
that throws without a prior Tester call, while two `Theory` methods run
three malformed strings through `TryParse` and four invalid declarations
through `CanProcess`. `ResultTests` covers the community side: three `Fact`
methods check a successful parse, a failed parse with its specific reason,
and `Map`'s transform-and-short-circuit behavior, while two `Theory` methods
run the four ways a declaration can fail through `Validate` and the four
EDI inputs that prove `TryParse` and `Parse` agree on every one of them.

## When not to use it

A `Result<T>` costs more than it returns when nothing downstream of a call
needs the reason a failure carries, or the chance to compose several steps
without branching between them. Logging a parse failure and moving on is the
same amount of work whether the failure arrived as `false` from `TryParse`
or as a `Result<EdiMessage>` whose `Error` is read once and discarded; the
second shape added a type definition and an unwrap step for a reason that
was never going to be inspected past the log line. A Tester-Doer pair pays
for an extra method call and the atomicity caveat even when nothing between
the Tester and the Doer could plausibly change the state either one reads; a
single method that validates and acts in one call, throwing or returning a
result directly, removes the gap a Tester-Doer split exists to expose.

None of the three shapes in this chapter is a substitute for an exception
where an exception is the right tool. A commodity code that arrives as
`null` rather than a string that merely fails a digit check, a connection
that cannot reach the customs database at all, a declaration decoded from a
corrupted byte stream: these are not expected outcomes a caller is meant to
branch on in the ordinary path, they are failures outside what the method's
contract promises to handle, and `ArgumentNullException.ThrowIfNull` in
every method in this chapter's example is exactly that kind of failure
reported the way it should be, by throwing. Wrapping every possible failure
in a `Result<T>`, including the ones that mean a caller broke the contract
rather than merely hit an expected edge case, trades a clear stack trace at
the point something went wrong for a `Value` that silently reads as `null`
or `default` wherever a caller forgot to check `IsSuccess` first.

<blockquote class="ai-sidebar" markdown="1">
Asked to add error handling to a parsing or validation
method, an AI pairing partner will sometimes wrap every return path in a
freshly invented `Result<T>` or `Either<TLeft, TRight>` type, including the
paths that report a caller's own contract violation, a `null` argument, an
out-of-range index, the cases `ArgumentNullException.ThrowIfNull` and its
siblings exist to cover by throwing. A related suggestion reaches for
`Result<T>` on a method that is only ever called once per request with its
failure logged and discarded, paying for a type and an unwrap step that a
`TryParse`-shaped boolean would report just as completely. Check a
generated answer against the three shapes this chapter built: a boolean
and an `out` parameter when a call site reads the answer once and moves on,
a Tester-Doer pair when a caller needs to ask "would this work"
before committing, a `Result<T>` when the specific failure reason has to
travel somewhere, or several steps need to compose without a branch
between them, and a thrown exception, not any of the three, when the
failure means a caller broke the method's contract rather than hit an
outcome the method is built to expect.
</blockquote>

## For deeper coverage

- Microsoft Learn, [Exceptions and Performance](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/exceptions-and-performance): the Framework Design Guidelines authority for both the Tester-Doer Pattern and the Try-Parse Pattern, including the guidance that a `Try...` method must never throw for a predictable failure within its documented purpose.
- Microsoft Learn, [Design Guidelines for Exceptions](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/exceptions): the broader Framework Design Guidelines chapter on which failures belong in an exception at all, the authority behind this chapter's "When not to use it" distinction between an expected outcome and a broken contract.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
