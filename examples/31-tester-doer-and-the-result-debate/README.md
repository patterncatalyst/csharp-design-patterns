# 31-tester-doer-and-the-result-debate

The same two validation problems, parsing an EDI customs message and
checking a `CustomsDeclaration` before it is processed, solved twice over the
same shipping domain, so the two idioms sit side by side rather than in the
abstract.

- **Framework Design Guidelines: Tester-Doer and `Try...`.**
  `EdiTryParser.TryParse` follows the BCL `Try...` convention: it returns
  `true` with the parsed `EdiMessage` on success, `false` with a `null`
  message on malformed EDI text, and never throws for that expected failure.
  `DeclarationProcessor` splits the same idea into two calls: `CanProcess` is
  the Tester a caller runs first, `Process` is the Doer that assumes the
  tester already passed. `Process` throws `InvalidOperationException` only
  when a caller skips the tester or ignores its result, a programming error,
  not an expected outcome.
- **The community alternative: `Result<T>`.** `EdiResultParser.Parse` and
  `DeclarationValidator.Validate` report the same two outcomes through a
  single `Result<T>` return value instead of an `out` parameter or a second
  method call. `IsSuccess` says which case it is, `Value` carries the payload
  on success, and `Error` names the specific reason on failure.
  `Result<T>.Map` transforms a success's value and passes a failure through
  unchanged, so a chain of operations never has to branch on `IsSuccess`
  itself.

Neither idiom gets the upper hand here: both parse the same EDI text format
(`Tag*Element1*Element2` lines, the first segment's tag as the message type)
and validate the same four `CustomsDeclaration` fields, and
`Both_styles_agree_on_whether_an_input_is_valid` checks they never disagree
on whether a given input is valid.

**What the tests prove.** `TesterDoerTests` shows `TryParse` returning the
parsed message for well-formed EDI text and `false`/`null` for malformed
text without ever throwing, `CanProcess` agreeing and disagreeing with a set
of valid and invalid declarations, and `Process` succeeding after a passing
`CanProcess` call but throwing when called on an untested, invalid
declaration. `ResultTests` shows `Parse` and `Validate` carrying a specific
failure reason rather than a generic `false`, `Map` transforming a success
while short-circuiting a failure, and the two idioms agreeing on every
sample input, well-formed or not.

Pure BCL, no runtime package: both idioms are built entirely from records,
readonly structs, and the .NET string and collection APIs already in the
box.

## Build and test

```sh
cd examples/31-tester-doer-and-the-result-debate
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
