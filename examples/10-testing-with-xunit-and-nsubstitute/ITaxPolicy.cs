namespace RatingEngine;

/// <summary>
/// Applies tax to a quoted subtotal. Synchronous and side-effect free, so
/// substituting it needs nothing more than
/// <c>Substitute.For&lt;ITaxPolicy&gt;()</c> and a <c>.Returns(...)</c> stub.
/// </summary>
public interface ITaxPolicy
{
    Money ApplyTax(Money subtotal);
}
