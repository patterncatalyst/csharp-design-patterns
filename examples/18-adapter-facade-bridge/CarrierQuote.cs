namespace CarrierIntegration;

/// <summary>
/// The one quote shape every carrier gateway in this example returns,
/// regardless of what shape the carrier's own response actually arrived in.
/// </summary>
public sealed record CarrierQuote(string CarrierCode, Money Amount, int TransitDays);
