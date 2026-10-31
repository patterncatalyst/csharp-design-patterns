using Xunit;

namespace DeclarationParsing.Tests;

/// <summary>
/// The Framework Design Guidelines side: <see cref="EdiTryParser.TryParse"/>
/// and the <see cref="DeclarationProcessor"/> Tester (<c>CanProcess</c>) /
/// Doer (<c>Process</c>) pair.
/// </summary>
public class TesterDoerTests
{
    private const string WellFormedEdi = "UNH*1*CUSDEC\nLIN*1*847130\nQTY*1*10\nUNT*2*1";

    [Fact]
    public void TryParse_returns_true_and_the_message_for_a_well_formed_edi_string()
    {
        var parsed = EdiTryParser.TryParse(WellFormedEdi, out var message);

        Assert.True(parsed);
        Assert.NotNull(message);
        Assert.Equal("UNH", message.MessageType);
        Assert.Equal(4, message.Segments.Count);
        Assert.Equal("LIN", message.Segments[1].Tag);
        Assert.Equal(["1", "847130"], message.Segments[1].Elements);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("UNH*1*CUSDEC\n*1*2")]
    public void TryParse_returns_false_and_null_for_a_malformed_string(string malformed)
    {
        var parsed = EdiTryParser.TryParse(malformed, out var message);

        Assert.False(parsed);
        Assert.Null(message);
    }

    [Fact]
    public void TryParse_does_not_throw_on_expected_failure()
    {
        const string bad = "UNH*1*CUSDEC\n*1*2";

        var exception = Record.Exception(() => EdiTryParser.TryParse(bad, out _));

        Assert.Null(exception);
    }

    [Fact]
    public void CanProcess_is_true_for_a_valid_declaration()
    {
        var processor = new DeclarationProcessor();
        var declaration = new CustomsDeclaration("DECL-1001", "847130", 1500.00m, "DE");

        Assert.True(processor.CanProcess(declaration));
    }

    public static TheoryData<CustomsDeclaration> InvalidDeclarations() => new()
    {
        new CustomsDeclaration("", "847130", 1500.00m, "DE"),
        new CustomsDeclaration("DECL-1002", "84A130", 1500.00m, "DE"),
        new CustomsDeclaration("DECL-1003", "847130", 0m, "DE"),
        new CustomsDeclaration("DECL-1004", "847130", 1500.00m, ""),
    };

    [Theory]
    [MemberData(nameof(InvalidDeclarations))]
    public void CanProcess_is_false_for_an_invalid_declaration(CustomsDeclaration declaration)
    {
        var processor = new DeclarationProcessor();

        Assert.False(processor.CanProcess(declaration));
    }

    [Fact]
    public void Process_after_a_passing_tester_succeeds()
    {
        var processor = new DeclarationProcessor();
        var declaration = new CustomsDeclaration("DECL-1005", "847130", 2200.00m, "NL");

        Assert.True(processor.CanProcess(declaration));
        var processed = processor.Process(declaration);

        Assert.Equal("DECL-1005", processed.Reference);
        Assert.Equal(2200.00m, processed.DeclaredValue);
    }

    [Fact]
    public void Process_without_testing_throws_on_invalid_input()
    {
        var processor = new DeclarationProcessor();
        var declaration = new CustomsDeclaration("", "847130", 1500.00m, "DE");

        Assert.Throws<InvalidOperationException>(() => processor.Process(declaration));
    }
}
