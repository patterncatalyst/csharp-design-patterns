using System.Collections.Frozen;
using Xunit;

namespace DocumentDispatch.Tests;

public class FrozenTableTests
{
    [Fact]
    public void Dispatch_table_has_a_handler_for_every_defined_document_kind()
    {
        var definedKinds = Enum.GetValues<DocumentKind>().ToHashSet();

        var registeredKinds = DocumentDispatcher.Table.Keys.ToHashSet();

        Assert.Equal(definedKinds, registeredKinds);
    }

    [Fact]
    public void Dispatch_table_is_a_single_frozen_instance_shared_across_dispatchers()
    {
        _ = new DocumentDispatcher();
        _ = new DocumentDispatcher();

        var first = DocumentDispatcher.Table;
        var second = DocumentDispatcher.Table;

        Assert.Same(first, second);
        Assert.NotEmpty(first);
    }

    [Fact]
    public void Dispatch_table_is_a_frozen_dictionary()
    {
        Assert.True(DocumentDispatcher.Table is FrozenDictionary<DocumentKind, Func<DocumentRequest, RenderedDocument>>);
    }
}
