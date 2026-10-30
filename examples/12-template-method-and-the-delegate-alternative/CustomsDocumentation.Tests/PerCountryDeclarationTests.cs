using Xunit;

namespace CustomsDocumentation.Tests;

/// <summary>
/// The same shipment, run through the same skeleton, produces a different
/// declaration for each destination country. These tests use the delegate
/// form (<see cref="DeclarationEngine"/> plus <see cref="CountrySteps"/>);
/// <see cref="ParityTests"/> proves the inheritance form agrees with every
/// one of these outcomes.
/// </summary>
public class PerCountryDeclarationTests
{
    private static Shipment CreateShipment(string destinationCountry) =>
        new("Shanghai", destinationCountry, 3000m, 1200m, ["Spare parts"]);

    [Fact]
    public void United_States_declaration_requires_formal_entry_above_the_value_threshold()
    {
        var declaration = DeclarationEngine.Build(CreateShipment("United States"), CountrySteps.UnitedStates);

        Assert.Equal(["Importer of Record", "HTS Code", "Country of Origin"], declaration.RequiredFields);
        Assert.Equal(new Money(75.00m, "USD"), declaration.Duty);
        Assert.Equal(["Formal entry required for shipments over 2500.00 USD"], declaration.ValidationMessages);
    }

    [Fact]
    public void Germany_declaration_requires_a_broker_above_the_weight_threshold()
    {
        var declaration = DeclarationEngine.Build(CreateShipment("Germany"), CountrySteps.Germany);

        Assert.Equal(["EORI Number", "Commodity Code", "Incoterms"], declaration.RequiredFields);
        Assert.Equal(new Money(570.00m, "EUR"), declaration.Duty);
        Assert.Equal(["Shipments over 1000 kg require a customs broker"], declaration.ValidationMessages);
    }

    [Fact]
    public void Japan_declaration_adds_a_flat_handling_fee_and_raises_no_warnings()
    {
        var declaration = DeclarationEngine.Build(CreateShipment("Japan"), CountrySteps.Japan);

        Assert.Equal(["Importer Tax ID", "HS Code", "Certificate of Origin"], declaration.RequiredFields);
        Assert.Equal(new Money(2150.00m, "JPY"), declaration.Duty);
        Assert.Empty(declaration.ValidationMessages);
    }

    [Fact]
    public void The_same_shipment_produces_three_distinct_declarations()
    {
        var shipment = CreateShipment("Worldwide");

        var unitedStates = DeclarationEngine.Build(shipment, CountrySteps.UnitedStates);
        var germany = DeclarationEngine.Build(shipment, CountrySteps.Germany);
        var japan = DeclarationEngine.Build(shipment, CountrySteps.Japan);

        Assert.NotEqual(unitedStates.RequiredFields, germany.RequiredFields);
        Assert.NotEqual(germany.RequiredFields, japan.RequiredFields);
        Assert.NotEqual(unitedStates.Duty, germany.Duty);
        Assert.NotEqual(germany.Duty, japan.Duty);
        Assert.NotEqual(unitedStates.ValidationMessages, germany.ValidationMessages);
    }
}
