namespace CarrierIntegration;

/// <summary>
/// Renders label content as a stand-in for ZPL, the field-delimited command
/// language thermal label printers accept. A real implementation would emit
/// positioned <c>^FO</c>/<c>^FD</c> fields; this example only needs a
/// deterministic, format-distinct string to prove the renderer varies
/// independently of the label kind.
/// </summary>
public sealed class ZplLabelRenderer : ILabelRenderer
{
    public string Render(LabelContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return $"^XA^FD{content.Title}^FS^FD{content.CarrierCode}^FS" +
               $"^FD{content.Destination}^FS^FD{content.Instructions}^FS^XZ";
    }
}
