namespace TrackingHistory;

/// <summary>
/// The stages a consignment passes through on its way to delivery, in the
/// order a well-behaved shipment reaches them. <see cref="ConsignmentHistory"/>
/// and <see cref="CarrierEventFeed"/> do not enforce this order themselves;
/// they report whatever <see cref="TrackingEvent"/>s they were given.
/// </summary>
public enum TrackingStage
{
    Booked,
    PickedUp,
    InTransit,
    CustomsHold,
    OutForDelivery,
    Delivered,
}
