namespace ConsignmentAmendments;

/// <summary>
/// An origin/destination pair for a consignment. A positional record, so
/// <see cref="Consignment"/>'s generated equality recurses structurally into
/// this type: two <see cref="Route"/> values with the same
/// <see cref="Origin"/> and <see cref="Destination"/> compare equal, with no
/// hand-written <c>Equals</c>, <c>GetHashCode</c>, or <c>==</c>/<c>!=</c>.
/// </summary>
public sealed record Route(string Origin, string Destination);
