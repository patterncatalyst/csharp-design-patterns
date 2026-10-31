namespace WireContracts;

/// <summary>
/// A carrier's rate quote as it crosses the wire. Money is an integer count of
/// minor units (cents), never a <see cref="decimal"/>, so the
/// <c>MoneyAsDecimalAnalyzer</c> has nothing to flag here.
/// </summary>
[WireContract]
public sealed record RateQuoteContract(string CarrierCode, long AmountMinorUnits, string Currency);

/// <summary>
/// A booking charge line as it crosses the wire. Same rule: money travels as
/// minor units, not <see cref="decimal"/>.
/// </summary>
[WireContract]
public sealed record BookingChargeContract(long PriceMinorUnits, int Quantity);

/// <summary>
/// An in-process rate calculation that never serializes across a boundary.
/// It carries no <see cref="WireContractAttribute"/>, so a <see cref="decimal"/>
/// here is unremarkable and the analyzer stays quiet.
/// </summary>
public sealed record InternalRateCalculation(decimal Amount);
