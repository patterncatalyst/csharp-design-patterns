namespace FreightRating.OverAbstracted;

/// <summary>
/// Builds the ordered list of components an <see cref="RateEngine"/> folds
/// over. There has only ever been one rating scheme, so this factory has
/// exactly one implementation.
/// </summary>
public interface IRateComponentFactory
{
    IReadOnlyList<IRateComponent> CreateComponents();
}
