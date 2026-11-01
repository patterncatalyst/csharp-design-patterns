using NSubstitute;
using Xunit;

namespace ShipmentWorkflow.Tests;

/// <summary>
/// The default <see cref="ShipmentPipeline"/> wired by
/// <see cref="ShipmentPipelineFactory"/>, plus its individual stages:
/// <see cref="StowageStage"/> allocating a slot through an injectable
/// <see cref="ISlotAllocator"/>, and <see cref="ClearanceStage"/> stopping
/// the pipeline before <see cref="InvoiceStage"/> runs when a booking is
/// rejected.
/// </summary>
public class PipelineTests
{
    private static readonly BookingRequest StandardBooking = new(
        "CN-7001", "USLAX", "SGSIN", DeclaredValue: 10_000.00m, WeightKilograms: 500.00m, Service: ServiceLevel.Standard);

    [Fact]
    public void End_to_end_booking_produces_an_invoice_with_the_rated_total()
    {
        var pipeline = ShipmentPipelineFactory.CreateDefault();

        var result = pipeline.Run(StandardBooking);

        var expectedAmount = RatingStrategies.For(ServiceLevel.Standard).Quote(StandardBooking).Amount;
        Assert.NotNull(result.Invoice);
        Assert.Equal(expectedAmount, result.Invoice!.Total);
    }

    [Fact]
    public void Stowage_stage_assigns_the_first_slot()
    {
        var stage = new StowageStage(new SequentialSlotAllocator());
        var context = new ShipmentContext(StandardBooking);

        var result = stage.Process(context);

        Assert.NotNull(result.Stowage);
        Assert.Equal("SLOT-001", result.Stowage!.SlotId);
    }

    [Fact]
    public void Slot_allocator_can_be_substituted()
    {
        var allocator = Substitute.For<ISlotAllocator>();
        allocator.Allocate(StandardBooking.ConsignmentId).Returns("SLOT-999");
        var stage = new StowageStage(allocator);
        var context = new ShipmentContext(StandardBooking);

        var result = stage.Process(context);

        Assert.NotNull(result.Stowage);
        Assert.Equal("SLOT-999", result.Stowage!.SlotId);
        allocator.Received(1).Allocate(StandardBooking.ConsignmentId);
    }

    [Fact]
    public void Clearance_rejection_halts_the_pipeline_before_invoicing()
    {
        var booking = StandardBooking with { DeclaredValue = 0m };
        var pipeline = ShipmentPipelineFactory.CreateDefault();

        var result = pipeline.Run(booking);

        Assert.NotNull(result.Clearance);
        Assert.False(result.Clearance!.Cleared);
        Assert.Null(result.Invoice);
    }

    [Fact]
    public void Embargoed_destination_is_rejected_at_clearance()
    {
        var booking = StandardBooking with { DestinationPort = "ZZZ" };
        var pipeline = ShipmentPipelineFactory.CreateDefault();

        var result = pipeline.Run(booking);

        Assert.NotNull(result.Clearance);
        Assert.False(result.Clearance!.Cleared);
        Assert.Equal("destination port is embargoed", result.Clearance.Reason);
        Assert.Null(result.Invoice);
    }

    [Fact]
    public void Cleared_booking_proceeds_through_every_stage()
    {
        var pipeline = ShipmentPipelineFactory.CreateDefault();

        var result = pipeline.Run(StandardBooking);

        Assert.NotNull(result.Quote);
        Assert.NotNull(result.Stowage);
        Assert.NotNull(result.Clearance);
        Assert.True(result.Clearance!.Cleared);
        Assert.NotNull(result.Invoice);
    }
}
