using System.Numerics;

namespace RateMath;

/// <summary>
/// A generic summation algorithm written once against the BCL's generic math
/// interfaces. It folds from the type's own additive identity rather than a
/// literal zero, so it works equally over <see cref="Money"/> and over any
/// built-in numeric type that implements <see cref="INumber{TSelf}"/>
/// (<c>int</c>, <c>decimal</c>, and the rest all satisfy the constraint
/// below), proving the method is not specific to this book's own types.
/// </summary>
public static class Totals
{
    /// <summary>Sums a sequence of values by repeated addition, starting from the additive identity.</summary>
    public static T Sum<T>(IEnumerable<T> values) where T : IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>
    {
        ArgumentNullException.ThrowIfNull(values);

        var total = T.AdditiveIdentity;
        foreach (var value in values)
        {
            total += value;
        }

        return total;
    }
}
