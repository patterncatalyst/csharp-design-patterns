namespace CarrierIntegration;

/// <summary>
/// Renders label content as a stand-in for a PDF page body. A real
/// implementation would lay the fields out on a page with a PDF library;
/// this example only needs a deterministic, format-distinct string to prove
/// the renderer varies independently of the label kind.
/// </summary>
public sealed class PdfLabelRenderer : ILabelRenderer
{
    public string Render(LabelContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        return $"%PDF-LABEL% {content.Title} | carrier={content.CarrierCode} | " +
               $"to={content.Destination} | {content.Instructions}";
    }
}
