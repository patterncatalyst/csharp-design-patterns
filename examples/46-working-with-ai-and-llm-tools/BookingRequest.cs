namespace RatingReview;

/// <summary>
/// A freight booking request priced by a rating strategy: the declared value
/// used for insurance and customs, and the gross weight used to compute the
/// carrier's per-kilogram charge.
/// </summary>
public sealed record BookingRequest(decimal DeclaredValue, decimal WeightKilograms);
