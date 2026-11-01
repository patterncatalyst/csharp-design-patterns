namespace DocumentDispatch;

/// <summary>
/// The shipping-document types a consignment's paperwork can require, each
/// rendered by its own handler in <see cref="DocumentDispatcher"/>.
/// </summary>
public enum DocumentKind
{
    BillOfLading,
    CommercialInvoice,
    PackingList,
    CertificateOfOrigin,
    CustomsDeclaration,
}
