namespace CarrierIntegration;

/// <summary>
/// The Bridge implementor: turns <see cref="LabelContent"/> into a printable
/// string in whatever output format the printer on hand expects. A new
/// renderer can be added without touching any <see cref="ShipmentLabel"/>
/// subclass, and a new label kind can be added without touching any
/// renderer.
/// </summary>
public interface ILabelRenderer
{
    string Render(LabelContent content);
}
