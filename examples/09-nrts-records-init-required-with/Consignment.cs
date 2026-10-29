namespace ConsignmentAmendments;

/// <summary>
/// A consignment moving through the shipping network.
/// </summary>
/// <remarks>
/// This is a non-positional record class: every property is declared
/// explicitly with <c>required</c> and <c>init</c> instead of through a
/// primary constructor, so construction and amendment read as separate
/// concerns. <c>required</c> is the piece nullable reference types cannot
/// enforce on their own: a non-nullable property can be flagged by the
/// compiler as "possibly unassigned", but nothing stops a caller from
/// ignoring the warning. A <c>required</c> property removes the escape
/// hatch, because the compiler refuses to compile an object initializer
/// that omits it (<c>CS9035</c>), and a record class with only
/// property-initializer syntax has no parameterless constructor to fall
/// back on.
///
/// A record class also gets compiler-generated structural equality, exactly
/// like a record struct: two independently constructed
/// <see cref="Consignment"/> values with identical property values are
/// <c>Equals</c>, share a hash code, and compare equal through <c>==</c>.
/// Equality recurses into <see cref="Route"/> (itself a record) and
/// <see cref="Weight"/> and <see cref="Money"/> (both record structs).
/// </remarks>
public sealed record Consignment
{
    /// <summary>The carrier-assigned identifier for this consignment.</summary>
    public required string ConsignmentId { get; init; }

    /// <summary>The origin/destination pair this consignment is routed through.</summary>
    public required Route Route { get; init; }

    /// <summary>The consignment's weight.</summary>
    public required Weight Weight { get; init; }

    /// <summary>
    /// A customer-supplied reference number. Legitimately absent on a newly
    /// booked consignment, so this member is nullable rather than
    /// <c>required</c>.
    /// </summary>
    public string? ReferenceNumber { get; init; }

    /// <summary>
    /// The declared value for customs and insurance purposes. Non-nullable
    /// but not <c>required</c>: it defaults to <see cref="Money.Zero"/>,
    /// showing that a default value and a <c>required</c> member on the
    /// same type do not interfere with one another.
    /// </summary>
    public Money DeclaredValue { get; init; } = Money.Zero;

    /// <summary>
    /// Produces an amended consignment re-routed to a new destination,
    /// leaving this instance unchanged. <c>with</c> performs a shallow
    /// copy: every property keeps its current value except the one the
    /// expression overrides, here one level into <see cref="Route"/>.
    /// </summary>
    public Consignment ReRouteTo(string destination) =>
        this with { Route = Route with { Destination = destination } };

    /// <summary>
    /// Produces an amended consignment with a corrected weight, leaving
    /// this instance unchanged.
    /// </summary>
    public Consignment WithCorrectedWeight(Weight weight) =>
        this with { Weight = weight };

    /// <summary>
    /// Produces an amended consignment with a reference number attached,
    /// leaving this instance unchanged.
    /// </summary>
    public Consignment WithReferenceNumber(string referenceNumber) =>
        this with { ReferenceNumber = referenceNumber };

    public override string ToString() =>
        $"{ConsignmentId}: {Route.Origin} -> {Route.Destination}, {Weight}, ref={ReferenceNumber ?? "(none)"}";
}
