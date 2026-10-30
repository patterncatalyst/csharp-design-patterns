using Xunit;

namespace CustomsDocumentation.Tests;

/// <summary>
/// Proves that both forms run their three steps in the same fixed order:
/// collect the required fields, calculate duty, then validate. The
/// inheritance form proves this through a subclass that only overrides the
/// steps (<see cref="CustomsDeclarationBuilder.Build"/> stays untouched and
/// unoverridable); the delegate form proves it through recording
/// <c>Func&lt;&gt;</c> delegates passed into <see cref="DeclarationEngine.Build"/>.
/// </summary>
public class StepOrderTests
{
    private static Shipment CreateShipment() =>
        new("Rotterdam", "Narnia", 500m, 10m, ["Sample goods"]);

    [Fact]
    public void Inheritance_form_runs_steps_in_the_fixed_declared_order()
    {
        var log = new List<string>();
        var builder = new RecordingDeclarationBuilder(log);

        builder.Build(CreateShipment());

        Assert.Equal(["CollectRequiredFields", "CalculateDuty", "Validate"], log);
    }

    [Fact]
    public void Delegate_form_runs_steps_in_the_same_fixed_order()
    {
        var log = new List<string>();
        var steps = new DeclarationSteps(
            CollectRequiredFields: shipment =>
            {
                log.Add("CollectRequiredFields");
                return [];
            },
            CalculateDuty: shipment =>
            {
                log.Add("CalculateDuty");
                return new Money(0m, "USD");
            },
            Validate: shipment =>
            {
                log.Add("Validate");
                return [];
            });

        DeclarationEngine.Build(CreateShipment(), steps);

        Assert.Equal(["CollectRequiredFields", "CalculateDuty", "Validate"], log);
    }

    /// <summary>
    /// A test-only subclass that overrides only the three steps. It never
    /// touches <see cref="CustomsDeclarationBuilder.Build"/>, because that
    /// method is not virtual: there is nothing to override. This is the
    /// compile-time guarantee behind the step-order test above.
    /// </summary>
    private sealed class RecordingDeclarationBuilder(List<string> log) : CustomsDeclarationBuilder
    {
        protected override IReadOnlyList<string> CollectRequiredFields(Shipment shipment)
        {
            log.Add(nameof(CollectRequiredFields));
            return [];
        }

        protected override Money CalculateDuty(Shipment shipment)
        {
            log.Add(nameof(CalculateDuty));
            return new Money(0m, "USD");
        }

        protected override IReadOnlyList<string> Validate(Shipment shipment)
        {
            log.Add(nameof(Validate));
            return [];
        }
    }
}
