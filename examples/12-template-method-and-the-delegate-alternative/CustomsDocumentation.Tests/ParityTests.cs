using Xunit;

namespace CustomsDocumentation.Tests;

/// <summary>
/// For a given country, the inheritance-based builder and the delegate-based
/// engine must agree: the same shipment produces the same required fields,
/// the same duty, and the same validation messages, regardless of which
/// form computed them. A <see cref="CustomsDeclaration"/> carries two
/// <see cref="IReadOnlyList{T}"/> members, and the compiler-generated record
/// equality for an interface-typed member falls back to reference equality
/// rather than comparing elements, so this test compares field by field
/// instead of the two records as a whole.
/// </summary>
public class ParityTests
{
    private static Shipment CreateShipment(string destinationCountry) =>
        new("Shanghai", destinationCountry, 3000m, 1200m, ["Spare parts"]);

    public static TheoryData<string, CustomsDeclarationBuilder, DeclarationSteps> CountryCases => new()
    {
        { "United States", new UnitedStatesDeclarationBuilder(), CountrySteps.UnitedStates },
        { "Germany", new GermanyDeclarationBuilder(), CountrySteps.Germany },
        { "Japan", new JapanDeclarationBuilder(), CountrySteps.Japan },
    };

    [Theory]
    [MemberData(nameof(CountryCases))]
    public void Inheritance_and_delegate_forms_agree_for_the_same_country(
        string destinationCountry, CustomsDeclarationBuilder builder, DeclarationSteps steps)
    {
        var shipment = CreateShipment(destinationCountry);

        var fromBuilder = builder.Build(shipment);
        var fromEngine = DeclarationEngine.Build(shipment, steps);

        Assert.Equal(fromBuilder.DestinationCountry, fromEngine.DestinationCountry);
        Assert.Equal(fromBuilder.RequiredFields, fromEngine.RequiredFields);
        Assert.Equal(fromBuilder.Duty, fromEngine.Duty);
        Assert.Equal(fromBuilder.ValidationMessages, fromEngine.ValidationMessages);
    }
}
