namespace RateCalculation;

/// <summary>
/// The tax contract: apply a jurisdiction's tax to a subtotal. A tax policy
/// depends only on <see cref="Money"/>, never on <see cref="Consignment"/> or
/// any pricing type, so a tax-rate change never touches pricing code (SRP).
/// </summary>
public interface ITaxPolicy
{
    Money ApplyTax(Money subtotal);
}
