namespace CustomsClearance;

/// <summary>
/// The outcome of running a declaration through a <see cref="ClearanceStage"/>
/// chain: either cleared with the computed duty, or halted at a named stage
/// with the reason that stage gave.
/// </summary>
public sealed record ClearanceResult(bool Cleared, string? HaltedStage, string? Reason, decimal Duty);
