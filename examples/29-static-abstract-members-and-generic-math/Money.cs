using System.Numerics;

namespace RateMath;

/// <summary>
/// A monetary amount in a single, implicit currency. Carrying a currency code
/// would be the realistic next step; this book keeps the type to one field so
/// the generic math interfaces stay the whole point of the example.
/// </summary>
/// <param name="Amount">The amount, in the implicit currency's minor-free decimal form.</param>
public readonly record struct Money(decimal Amount) :
    IAdditionOperators<Money, Money, Money>,
    IAdditiveIdentity<Money, Money>,
    IMultiplyOperators<Money, decimal, Money>,
    IComparisonOperators<Money, Money, bool>,
    IComparable<Money>
{
    /// <summary>The zero amount, looked up through <see cref="IAdditiveIdentity{TSelf,TResult}"/>.</summary>
    public static Money AdditiveIdentity => new(0m);

    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount);

    public static Money operator *(Money left, decimal factor) => new(left.Amount * factor);

    public static bool operator <(Money left, Money right) => left.Amount < right.Amount;

    public static bool operator <=(Money left, Money right) => left.Amount <= right.Amount;

    public static bool operator >(Money left, Money right) => left.Amount > right.Amount;

    public static bool operator >=(Money left, Money right) => left.Amount >= right.Amount;

    /// <summary>Named alternate for <c>operator +</c> (CA2225).</summary>
    public static Money Add(Money left, Money right) => left + right;

    /// <summary>Named alternate for <c>operator *</c> (CA2225).</summary>
    public static Money Multiply(Money left, decimal factor) => left * factor;

    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

    public override string ToString() => Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
}
