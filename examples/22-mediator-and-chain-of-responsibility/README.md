# 22-mediator-and-chain-of-responsibility

Two behavioral patterns over the same customs domain, each showing a
different way to decouple who handles a request from who issues it.

- **Chain of Responsibility.** A `CustomsDeclaration` moves through a chain of
  `ClearanceStage` links built by `CustomsClearancePipeline.Standard()`:
  `DocumentCheckStage`, `DutyCalculationStage`, `InspectionStage`, and
  `ReleaseStage`. Each stage records itself on the shared `ClearanceContext`
  and either halts the declaration with a reason (missing documents, a failed
  inspection) or lets it fall through to the next stage. A halt is a returned
  `ClearanceResult` with `Cleared: false`, never an exception; `ClearanceStage`
  only calls its successor when the current stage clears, so a halted
  declaration never reaches the stages after it.
- **Mediator.** `DeclarationDesk` and `DutyDesk` are `ClearanceColleague`s that
  hold a reference only to an `IClearanceMediator`, never to each other.
  `CustomsMediator` is the concrete mediator: its `Register` method wires in
  both desks after they are constructed, since each desk's constructor needs
  the mediator first, and its `Send` routes a message to whichever desk did
  not send it. A desk's own `Inbox` never contains a message that desk sent.

**What the tests prove.** `ClearanceChainTests` sends a complete declaration
through the chain and asserts all four stage names appear on
`ClearanceContext.StagesRun` in order, then checks a `Cleared` result carries
the computed duty. Separate cases show a declaration with incomplete
documents halts at `DocumentCheckStage` before duty or inspection ever run,
and a declaration that fails a required inspection halts at `InspectionStage`
before `ReleaseStage` runs. `MediatorTests` sends a message from
`DeclarationDesk` and asserts it lands in `DutyDesk.Inbox`, not back in
`DeclarationDesk.Inbox`, then has `DutyDesk` reply and asserts that reply
reaches `DeclarationDesk.Inbox`, round-tripping through `CustomsMediator`
without either desk holding a reference to the other.

Shipping domain, no containers: both patterns are pure, synchronous
collaborations over in-memory state, no network or I/O to fake out.

## Build and test

```sh
cd examples/22-mediator-and-chain-of-responsibility
dotnet test
```

This also runs as part of the repo-wide `./scripts/verify.sh`.
