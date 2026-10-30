# 12-template-method-and-the-delegate-alternative

A customs-declaration workflow built two ways: the classic Template Method
pattern as an abstract base class, and the modern alternative as a static
skeleton paired with a record of delegates. Both forms run the same three
steps, in the same fixed order, over the same `Shipment` input, and both
produce the same `CustomsDeclaration` output for a given country.

**The domain.** `Shipment` carries a declared value, a weight, an origin, a
destination country, and a list of line items. Assembling a
`CustomsDeclaration` for it always takes the same three steps: collect the
required fields, calculate duty, then validate. What differs by country is
the content of each step, not the order they run in.

## The inheritance form

`CustomsDeclarationBuilder` is an abstract class with a protected constructor
(an abstract type has no business being instantiated directly) and one public
method, `Build`. `Build` is a plain, non-virtual method: there is no `virtual`
keyword to override, so no subclass can reorder the three steps or skip one.
A subclass supplies the country's rules by overriding three protected steps:
`CollectRequiredFields`, `CalculateDuty`, and `Validate`.
`UnitedStatesDeclarationBuilder`, `GermanyDeclarationBuilder`, and
`JapanDeclarationBuilder` each override those three steps with distinct
rules; none of them touches `Build`.

## The delegate form

`DeclarationSteps` is a record of three `Func<>` values, one per step.
`DeclarationEngine.Build` is a static method that runs those three functions
in the same fixed order the abstract base class uses. `CountrySteps` exposes
a `DeclarationSteps` value per country, reproducing the same rules as the
three subclasses. Swapping a country's rules means passing a different
`DeclarationSteps` value, not writing a new type.

## Per-country rules

| Country | Required fields | Duty | Validation trigger |
| --- | --- | --- | --- |
| United States | Importer of Record, HTS Code, Country of Origin | 2.5% of declared value, USD | Declared value over 2500.00 USD |
| Germany | EORI Number, Commodity Code, Incoterms | 19% of declared value, EUR | Weight over 1000 kg |
| Japan | Importer Tax ID, HS Code, Certificate of Origin | 5% of declared value plus a flat 2000 JPY handling fee | Declared value over 200000 JPY |

Every country also flags a shipment with no line items.

## What the tests prove

`StepOrderTests` proves the fixed step order in both forms: a test-only
subclass of `CustomsDeclarationBuilder` appends each step's name to a shared
log, and a set of recording `Func<>` delegates does the same for
`DeclarationEngine`. Both logs come back in the same order:
`CollectRequiredFields`, `CalculateDuty`, `Validate`.

`PerCountryDeclarationTests` runs the same shipment through all three
countries via the delegate form and asserts the required fields, the duty,
and the validation messages all differ, proving the skeleton is not just
producing a fixed answer regardless of input.

`ParityTests` runs the same shipment through both forms for each country and
asserts the two declarations agree field by field. A `CustomsDeclaration`
carries two `IReadOnlyList<string>` members, and the compiler-generated
record equality for an interface-typed member falls back to reference
equality instead of comparing elements, so the test compares
`RequiredFields`, `Duty`, and `ValidationMessages` individually rather than
asserting equality on the two records as a whole.

## Build and test

```sh
cd examples/12-template-method-and-the-delegate-alternative
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
