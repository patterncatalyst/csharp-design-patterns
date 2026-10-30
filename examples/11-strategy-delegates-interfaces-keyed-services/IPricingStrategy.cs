namespace RateQuoting;

/// <summary>
/// The GoF Strategy role: an interchangeable pricing algorithm for a
/// <see cref="RateRequest"/>. Pricing is pure, synchronous computation, so
/// this is a plain method, not a <c>Task</c>-returning one.
/// </summary>
public interface IPricingStrategy
{
    Money Price(RateRequest request);
}
