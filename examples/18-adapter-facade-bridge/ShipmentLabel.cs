namespace CarrierIntegration;

/// <summary>
/// The Bridge abstraction: a label kind that knows what data it carries, but
/// delegates turning that data into a printable string to whichever
/// <see cref="ILabelRenderer"/> it was constructed with. A label kind can be
/// added without a matching new renderer, and a renderer can be added
/// without a matching new label kind; the two hierarchies vary
/// independently.
/// </summary>
public abstract class ShipmentLabel(ILabelRenderer renderer)
{
    protected abstract LabelContent BuildContent();

    public string Render() => renderer.Render(BuildContent());
}
