using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace DocumentDispatch;

/// <summary>
/// Dispatches a <see cref="DocumentRequest"/> to the one rendering method
/// registered for its <see cref="DocumentKind"/>: a Shortcut-pattern
/// dispatch table, a plain <see cref="FrozenDictionary{TKey,TValue}"/> of
/// method-group delegates built once, in place of a Visitor hierarchy or a
/// switch expression over the enum.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "Render, TryRender, and SupportedKinds are instance members by design: this dispatcher is " +
        "the stable collaborator a caller holds onto. They read the shared static table rather than per-instance " +
        "state, but a static method would not read as the same kind of collaborator.")]
public sealed class DocumentDispatcher
{
    internal static readonly FrozenDictionary<DocumentKind, Func<DocumentRequest, RenderedDocument>> Handlers =
        new Dictionary<DocumentKind, Func<DocumentRequest, RenderedDocument>>
        {
            [DocumentKind.BillOfLading] = DocumentRenderers.RenderBillOfLading,
            [DocumentKind.CommercialInvoice] = DocumentRenderers.RenderCommercialInvoice,
            [DocumentKind.PackingList] = DocumentRenderers.RenderPackingList,
            [DocumentKind.CertificateOfOrigin] = DocumentRenderers.RenderCertificateOfOrigin,
            [DocumentKind.CustomsDeclaration] = DocumentRenderers.RenderCustomsDeclaration,
        }.ToFrozenDictionary();

    /// <summary>
    /// Renders <paramref name="request"/> with its kind's registered handler,
    /// or throws <see cref="UnknownDocumentKindException"/> if no handler is
    /// registered for that kind.
    /// </summary>
    public RenderedDocument Render(DocumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Handlers.TryGetValue(request.Kind, out var handler)
            ? handler(request)
            : throw new UnknownDocumentKindException($"No renderer is registered for document kind '{request.Kind}'.");
    }

    /// <summary>
    /// The non-throwing counterpart to <see cref="Render"/>, for a caller
    /// that expects an unregistered kind to be a routine case rather than a
    /// fault.
    /// </summary>
    public bool TryRender(DocumentRequest request, out RenderedDocument? document)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Handlers.TryGetValue(request.Kind, out var handler))
        {
            document = handler(request);
            return true;
        }

        document = null;
        return false;
    }

    /// <summary>
    /// The document kinds this dispatcher can render.
    /// </summary>
    public IReadOnlyCollection<DocumentKind> SupportedKinds => Handlers.Keys;

    /// <summary>
    /// The shared frozen dispatch table, exposed internally so tests can
    /// confirm it is built once and shared across every
    /// <see cref="DocumentDispatcher"/> instance.
    /// </summary>
    internal static FrozenDictionary<DocumentKind, Func<DocumentRequest, RenderedDocument>> Table => Handlers;
}
