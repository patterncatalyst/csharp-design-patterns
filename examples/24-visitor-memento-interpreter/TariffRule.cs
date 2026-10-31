namespace TariffRules;

/// <summary>
/// The Interpreter pattern's abstract expression. This hierarchy is closed:
/// every concrete case lives in this file, so both <see cref="TariffEvaluator"/>
/// and <see cref="TariffCompiler"/> can walk it exhaustively with a pattern
/// match, provided a discard arm for the compiler's own exhaustiveness proof.
/// </summary>
public abstract record TariffRule;

/// <summary>A fixed fee, independent of the shipment.</summary>
public sealed record FlatFee(decimal Amount) : TariffRule;

/// <summary>A rate charged per kilogram of the shipment's weight.</summary>
public sealed record PerKilogram(decimal Rate) : TariffRule;

/// <summary>An inner rule scaled by a percentage surcharge.</summary>
public sealed record Surcharge(TariffRule Inner, decimal Percent) : TariffRule;

/// <summary>The total of a set of component rules.</summary>
public sealed record Sum(IReadOnlyList<TariffRule> Rules) : TariffRule;
