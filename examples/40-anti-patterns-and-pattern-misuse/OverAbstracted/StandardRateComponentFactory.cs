namespace FreightRating.OverAbstracted;

/// <summary>
/// The sole <see cref="IRateComponentFactory"/> implementation: base rate,
/// then weight surcharge, then fuel levy, in that order.
/// </summary>
public sealed class StandardRateComponentFactory : IRateComponentFactory
{
    public IReadOnlyList<IRateComponent> CreateComponents() =>
        [new BaseRateComponent(), new WeightSurchargeComponent(), new FuelLevyComponent()];
}
