using Xunit;

namespace TariffRules.Tests;

public class MementoTests
{
    [Fact]
    public void Restore_rolls_back_service_level_and_weight()
    {
        var originator = new BookingOriginator();
        originator.SetServiceLevel(ServiceLevel.Standard);
        originator.SetWeight(12m);

        var memento = originator.Save();

        originator.SetServiceLevel(ServiceLevel.Express);
        originator.SetWeight(20m);

        originator.Restore(memento);

        Assert.Equal(ServiceLevel.Standard, originator.ServiceLevel);
        Assert.Equal(12m, originator.WeightKilograms);
    }

    [Fact]
    public void Rollback_through_caretaker_restores_the_last_checkpoint()
    {
        var originator = new BookingOriginator();
        var caretaker = new AmendmentCaretaker();
        originator.SetServiceLevel(ServiceLevel.Economy);

        caretaker.Checkpoint(originator);
        originator.SetServiceLevel(ServiceLevel.Express);

        caretaker.Rollback(originator);

        Assert.Equal(ServiceLevel.Economy, originator.ServiceLevel);
    }

    [Fact]
    public void Memento_snapshot_is_isolated_from_later_mutation()
    {
        var originator = new BookingOriginator();
        var caretaker = new AmendmentCaretaker();
        originator.AddLine(new BookingLine("SKU-1", 2));

        var snapshot = originator.Save();
        caretaker.Checkpoint(originator);

        originator.AddLine(new BookingLine("SKU-2", 1));

        caretaker.Rollback(originator);

        Assert.Single(originator.Lines);
        Assert.DoesNotContain(originator.Lines, line => line.Sku == "SKU-2");
        Assert.Single(snapshot.Lines);
    }

    [Fact]
    public void Rollback_with_no_checkpoint_throws()
    {
        var originator = new BookingOriginator();
        var caretaker = new AmendmentCaretaker();

        Assert.Throws<InvalidOperationException>(() => caretaker.Rollback(originator));
    }
}
