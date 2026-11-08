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

- Report an expected parse failure through a `bool` return and an `out` parameter, and throw only for a broken contract.
- Pair a Tester with a Doer, and state the gap between the two calls.
- Return a `Result<T>` that carries the failure reason, and chain a success through `Map` without branching.
- Show that the three shapes report the same verdict on the same input.
- Choose among them by what the caller needs from the answer.

## Why this matters

A customs declaration with a malformed commodity code, an EDI message truncated mid-transfer, a declared value of zero, a blank origin country: a documentation pipeline expects all of these. Every shape here handles the bad case. The question is how a method reports it.

The Framework Design Guidelines name two conventions: `Try...` methods that return a `bool` and hand back the result through `out`, and a Tester-Doer pair where one method answers yes or no and another acts on a yes. A `Result<T>` that wraps success or failure in one returned value has become common in C# code without joining the guidelines. All three report the same fact in different shapes. A codebase that mixes them without a convention makes every call site a small decision about which contract it honors.

{% include excalidraw.html name="31-tester-doer-and-the-result-debate" caption="Figure 31.1: The same parse-and-validate decision appears twice: Try-Parse style methods report failure through a boolean, while Result wraps the same outcome as a value a caller cannot ignore." %}

## An EDI message and a declaration

Parsing turns raw EDI text into segments:

```csharp
public sealed record EdiSegment(string Tag, IReadOnlyList<string> Elements);

public sealed record EdiMessage(string MessageType, IReadOnlyList<EdiSegment> Segments);
```

A message is one segment per line, a tag followed by `*`-separated elements: `UNH*1*CUSDEC` is well-formed, while a blank line or a line with no tag before its first `*` isn't. Validation then accepts or rejects a declaration:

```csharp
public sealed record CustomsDeclaration(
    string Reference,
    string CommodityCode,
    decimal DeclaredValue,
    string OriginCountry);

public sealed record ProcessedDeclaration(string Reference, decimal DeclaredValue);
```

A declaration is valid when its reference and origin country aren't blank, its declared value is positive, and its commodity code is a six-digit Harmonized System number. Both checks fail routinely, for reasons the caller has to act on.

## The Try-Parse convention: a boolean and an out parameter

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

The convention nests: `TryParseSegment` has the same shape, and one bad line fails the whole message. Malformed text, empty or with a tagless segment, returns `false` and never throws. `ThrowIfNull` is the one throw, because a `null` argument is a programming error, not a value the method exists to reject. The caller checks the `bool` and reads `message` only on `true`.

## The Tester-Doer convention: ask first, then act

`CanProcess` is the Tester, a yes-or-no question with no side effect. `Process` is the Doer:

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

An invalid declaration is reported by `CanProcess` returning `false`. `Process` throws `InvalidOperationException` only when a caller skipped the Tester or ignored its answer, which is a programming error. Calling `Process` directly on a declaration with a blank reference throws rather than returning a half-built result.

### The gap between the two calls

A Tester call and a Doer call are two calls, not one atomic operation. `CanProcess` holds no lock, so state it read can change before `Process` runs: another thread, or another pipeline step, gets in between. `CustomsDeclaration` is an immutable record, so its own fields can't change in that gap, but anything mutable or shared that a Tester consults can. The Tester answers about a moment in time, and the Doer must still guard, as `Process` does.

## The community `Result<T>`: a value that carries the reason

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

A private constructor leaves `Success` and `Failure` as the only ways to build one, so a result is always in one of the two states and `IsSuccess` says which. The parser reports the same failures through this shape:

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

Where `TryParse` returns a bare `false`, `Parse` names the rule: "A segment line is blank" or "Segment '*1*2' has no tag", text the `out` shape has nowhere to put. `DeclarationValidator.Validate` checks reference, commodity code, declared value and origin country in order, and returns the first failure with a message naming that field, or a success wrapping the declaration. Each of the four failures has its own message.

### Composing with Map

`Map` applies its selector only on success. On failure it returns a new failure with the same `Error` and never calls the selector. A successful `21` mapped through doubling gives `42`; a failure with `"no value"` comes through `Map` unchanged. A chain of `Map` calls reads top to bottom as the steps a declaration goes through, with no `if (!ok) return` between them.

## Comparing the three styles

Run the same four EDI inputs (well-formed, a tagless segment, empty, whitespace only) through `EdiTryParser.TryParse` and `EdiResultParser.Parse`, and the `bool` and `IsSuccess` agree on every one. They're one verdict in two shapes. `CanProcess` and `Validate` likewise check the same four fields in the same order.

{% include excalidraw.html name="31-try-parse-vs-result" caption="Figure 31.2: A boolean and an out parameter force a caller to branch immediately, while a Result value carries its own reason and can be composed before any branching happens. Neither shape wins outright." %}

| | `bool` + `out` | Tester-Doer | `Result<T>` |
| --- | --- | --- | --- |
| Call-site cost | one `if` | two calls | unwrap before use |
| Failure reason | none | none | `Error` text |
| Composes | no; branch after every call | no | yes, through `Map` |
| Ask without acting | no | yes | no |
| Extra cost | none | the gap between calls | a type to define or a library; `Value` is an unchecked read if `IsSuccess` is skipped |

None is a better version of the others:

- **`bool` + `out`** suits a parser at the edge of a pipeline whose caller logs a failure and stops.
- **Tester-Doer** suits a caller that must ask "would this work" before committing.
- **`Result<T>`** suits a validation pipeline that chains several checks and must carry the specific reason to a user or an audit log.

{% include excalidraw.html name="31-result-composition" caption="Figure 31.3: Result's Map chains a success through each step and short-circuits on the first failure, while the boolean-and-out shape forces a caller to branch immediately after every call." %}

## When not to use it

- **`Result<T>` whose reason nobody reads.** If a failure is logged once and dropped, `false` from `TryParse` does the same work without a type and an unwrap step.
- **Tester-Doer with nothing in the gap.** If nothing can change between the calls, one method that validates and acts, throwing or returning a result, removes the gap the split creates.
- **Any of the three for a broken contract.** A `null` argument, an unreachable customs database, a corrupted byte stream: these aren't outcomes a caller branches on, and `ThrowIfNull` in every method here reports them by throwing. Wrapping them in `Result<T>` trades a stack trace at the fault for a `Value` that reads as `null` or `default` wherever `IsSuccess` went unchecked.

<blockquote class="ai-sidebar" markdown="1">
**Specifying error handling.** "Add error handling to the declaration parser" leaves open which shape reports an expected failure and what still throws. State it:

> Keep `ArgumentNullException.ThrowIfNull` for null arguments. Report a malformed EDI message from `EdiResultParser.Parse` as `Result<EdiMessage>.Failure` with a message naming the broken rule, and validate declarations with `DeclarationValidator.Validate` returning the first failing field. Use `TryParse` with a `bool` and `out` only where the caller logs and stops.

If the reply wraps null-argument checks in a `Result<T>` or invents an `Either` type, ask which caller branches on that case.
</blockquote>

## Build, run, observe

```sh
cd examples/31-tester-doer-and-the-result-debate
dotnet test
```

All 23 tests pass; `scripts/verify.sh` runs them with every other example. One experiment:

- **Drop the Doer's guard.** Delete the `if (!CanProcess(declaration))` block from `Process`. A declaration with a blank reference now processes into a result, and the guard's test fails.

## For deeper coverage

- Microsoft Learn, [Exceptions and Performance](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/exceptions-and-performance): the Framework Design Guidelines on the Tester-Doer and Try-Parse patterns, including that a `Try...` method must not throw for a predictable failure within its purpose.
- Microsoft Learn, [Design Guidelines for Exceptions](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/exceptions): which failures belong in an exception, the basis for separating an expected outcome from a broken contract.
- The full reading list lives on the [bibliography page]({{ '/bibliography/' | relative_url }}).
