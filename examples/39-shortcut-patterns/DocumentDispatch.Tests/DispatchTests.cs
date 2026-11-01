using Xunit;

namespace DocumentDispatch.Tests;

public class DispatchTests
{
    [Fact]
    public void Render_dispatches_a_bill_of_lading_to_its_handler()
    {
        var dispatcher = new DocumentDispatcher();
        var request = new DocumentRequest(
            DocumentKind.BillOfLading,
            "CN-2001",
            new Dictionary<string, string> { ["carrier"] = "Maersk", ["vessel"] = "Ever Ace" });

        var rendered = dispatcher.Render(request);

        Assert.Equal(DocumentKind.BillOfLading, rendered.Kind);
        Assert.Contains("Maersk", rendered.Body);
        Assert.Contains("Ever Ace", rendered.Body);
    }

    public static TheoryData<DocumentKind> AllDocumentKinds => new(Enum.GetValues<DocumentKind>());

    [Theory]
    [MemberData(nameof(AllDocumentKinds))]
    public void Render_dispatches_each_document_kind_to_the_matching_handler(DocumentKind kind)
    {
        var dispatcher = new DocumentDispatcher();
        var request = new DocumentRequest(kind, "CN-2002", new Dictionary<string, string>());

        var rendered = dispatcher.Render(request);

        Assert.Equal(kind, rendered.Kind);
    }

    [Fact]
    public void Render_preserves_the_consignment_id_on_the_rendered_document()
    {
        var dispatcher = new DocumentDispatcher();
        var request = new DocumentRequest(DocumentKind.PackingList, "CN-2003", new Dictionary<string, string>());

        var rendered = dispatcher.Render(request);

        Assert.Equal("CN-2003", rendered.ConsignmentId);
    }

    [Fact]
    public void Render_throws_for_an_unregistered_document_kind()
    {
        var dispatcher = new DocumentDispatcher();
        var request = new DocumentRequest((DocumentKind)999, "CN-2004", new Dictionary<string, string>());

        Assert.Throws<UnknownDocumentKindException>(() => dispatcher.Render(request));
    }

    [Fact]
    public void TryRender_returns_true_and_the_document_for_a_registered_kind()
    {
        var dispatcher = new DocumentDispatcher();
        var request = new DocumentRequest(
            DocumentKind.CertificateOfOrigin,
            "CN-2005",
            new Dictionary<string, string> { ["originCountry"] = "Vietnam" });

        var succeeded = dispatcher.TryRender(request, out var document);

        Assert.True(succeeded);
        Assert.NotNull(document);
        Assert.Equal(DocumentKind.CertificateOfOrigin, document!.Kind);
        Assert.Contains("Vietnam", document.Body);
    }

    [Fact]
    public void TryRender_returns_false_and_null_for_an_unregistered_document_kind()
    {
        var dispatcher = new DocumentDispatcher();
        var request = new DocumentRequest((DocumentKind)999, "CN-2006", new Dictionary<string, string>());

        var succeeded = dispatcher.TryRender(request, out var document);

        Assert.False(succeeded);
        Assert.Null(document);
    }
}
