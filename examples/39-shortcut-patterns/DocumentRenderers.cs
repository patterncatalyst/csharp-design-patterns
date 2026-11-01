namespace DocumentDispatch;

/// <summary>
/// One rendering method per <see cref="DocumentKind"/>, each used as a method
/// group value in <see cref="DocumentDispatcher"/>'s frozen dispatch table.
/// A dedicated method per kind, rather than one generic renderer branching on
/// the enum, keeps each document's field selection and wording next to its
/// own kind instead of folded into a shared switch.
/// </summary>
public static class DocumentRenderers
{
    public static RenderedDocument RenderBillOfLading(DocumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = $"Bill of lading for {request.ConsignmentId}: carrier={Field(request, "carrier")}, vessel={Field(request, "vessel")}.";
        return new RenderedDocument(DocumentKind.BillOfLading, request.ConsignmentId, body);
    }

    public static RenderedDocument RenderCommercialInvoice(DocumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = $"Commercial invoice for {request.ConsignmentId}: buyer={Field(request, "buyer")}, totalValue={Field(request, "totalValue")}.";
        return new RenderedDocument(DocumentKind.CommercialInvoice, request.ConsignmentId, body);
    }

    public static RenderedDocument RenderPackingList(DocumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = $"Packing list for {request.ConsignmentId}: packageCount={Field(request, "packageCount")}, grossWeightKg={Field(request, "grossWeightKg")}.";
        return new RenderedDocument(DocumentKind.PackingList, request.ConsignmentId, body);
    }

    public static RenderedDocument RenderCertificateOfOrigin(DocumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = $"Certificate of origin for {request.ConsignmentId}: originCountry={Field(request, "originCountry")}.";
        return new RenderedDocument(DocumentKind.CertificateOfOrigin, request.ConsignmentId, body);
    }

    public static RenderedDocument RenderCustomsDeclaration(DocumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = $"Customs declaration for {request.ConsignmentId}: tariffCode={Field(request, "tariffCode")}, declaredValue={Field(request, "declaredValue")}.";
        return new RenderedDocument(DocumentKind.CustomsDeclaration, request.ConsignmentId, body);
    }

    private static string Field(DocumentRequest request, string key) =>
        request.Fields.TryGetValue(key, out var value) ? value : "unspecified";
}
