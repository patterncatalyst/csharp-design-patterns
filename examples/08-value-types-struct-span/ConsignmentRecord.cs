namespace FreightValueTypes;

/// <summary>
/// The typed result of parsing one fixed-width EDI-style consignment
/// segment. See <see cref="EdiRecordParser"/> for the field layout this is
/// assembled from.
/// </summary>
public readonly record struct ConsignmentRecord(int ConsignmentId, SlotId Slot, Weight Weight, Money Charge);
