using Xunit;

namespace BookingAmendments.Tests;

/// <summary>
/// Proves the queue's two directions: <see cref="AmendmentQueue.Run"/>
/// applies enqueued commands first-in-first-out, and
/// <see cref="AmendmentQueue.Undo"/> walks the executed history back
/// last-executed-first.
/// </summary>
public class AmendmentQueueTests
{
    [Fact]
    public void Run_executes_enqueued_commands_in_fifo_order()
    {
        var booking = new Booking("BK-2001", "Standard", 500m);
        var queue = new AmendmentQueue();
        queue.Enqueue(new ChangeServiceLevelCommand(booking, "Priority"));
        queue.Enqueue(new ChangeServiceLevelCommand(booking, "Express"));
        queue.Enqueue(new AddLineItemCommand(booking, new LineItem("Express surcharge", Money.Of(45.00m))));

        queue.Run();

        // If the queue ran last-enqueued-first, ServiceLevel would be
        // "Priority" instead: this value only comes out right under FIFO.
        Assert.Equal("Express", booking.ServiceLevel);
        Assert.Single(booking.LineItems);
        Assert.Equal("Express surcharge", booking.LineItems[0].Description);
    }

    [Fact]
    public void Undo_walks_the_executed_history_back_in_lifo_order()
    {
        var booking = new Booking("BK-2002", "Standard", 500m);
        var queue = new AmendmentQueue();
        queue.Enqueue(new ChangeServiceLevelCommand(booking, "Priority"));
        queue.Enqueue(new ChangeServiceLevelCommand(booking, "Express"));
        queue.Enqueue(new ChangeServiceLevelCommand(booking, "Overnight"));
        queue.Run();

        Assert.Equal("Overnight", booking.ServiceLevel);

        queue.Undo();

        Assert.Equal("Express", booking.ServiceLevel);

        queue.Undo();

        Assert.Equal("Priority", booking.ServiceLevel);
    }

    [Fact]
    public void Undo_restores_the_exact_snapshot_even_after_later_commands_changed_the_value()
    {
        var booking = new Booking("BK-2003", "Standard", 10m);
        var queue = new AmendmentQueue();
        queue.Enqueue(new ChangeWeightCommand(booking, 20m));
        queue.Enqueue(new ChangeWeightCommand(booking, 30m));
        queue.Run();

        Assert.Equal(30m, booking.WeightKilograms);

        queue.Undo();

        Assert.Equal(20m, booking.WeightKilograms);

        queue.Undo();

        Assert.Equal(10m, booking.WeightKilograms);
    }

    [Fact]
    public void Undo_with_no_executed_history_throws()
    {
        var queue = new AmendmentQueue();

        Assert.Throws<InvalidOperationException>(queue.Undo);
    }

    [Fact]
    public void Redo_re_executes_the_most_recently_undone_command()
    {
        var booking = new Booking("BK-2004", "Standard", 500m);
        var queue = new AmendmentQueue();
        queue.Enqueue(new ChangeServiceLevelCommand(booking, "Express"));
        queue.Run();
        queue.Undo();

        Assert.Equal("Standard", booking.ServiceLevel);

        queue.Redo();

        Assert.Equal("Express", booking.ServiceLevel);
    }
}
