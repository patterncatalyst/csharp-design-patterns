namespace FreightRating.OverAbstracted;

/// <summary>
/// Folds a factory-built list of <see cref="IRateComponent"/> instances over
/// a request to produce the same quote <see cref="RateCalculator.Quote"/>
/// computes directly. The Strategy and Factory machinery exists so a second
/// rating scheme could be swapped in, but this book has never needed one.
/// </summary>
public sealed class RateEngine(IRateComponentFactory factory)
{
    public decimal Quote(RateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var subtotal = 0m;
        foreach (var component in factory.CreateComponents())
        {
            subtotal += component.Apply(request, subtotal);
        }

        return subtotal;
    }
}
