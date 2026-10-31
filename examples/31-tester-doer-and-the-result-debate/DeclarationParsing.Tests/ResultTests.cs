using Xunit;

namespace DeclarationParsing.Tests;

/// <summary>
/// The community side: <see cref="EdiResultParser.Parse"/> and
/// <see cref="DeclarationValidator.Validate"/>, both returning a
/// <see cref="Result{T}"/> instead of a boolean out-parameter or an
/// exception. <see cref="Both_styles_agree_on_whether_an_input_is_valid"/>
/// checks the two idioms never disagree on the same EDI text.
/// </summary>
public class ResultTests
{
    private const string WellFormedEdi = "UNH*1*CUSDEC\nLIN*1*847130\nQTY*1*10\nUNT*2*1";
    private const string MalformedEdi = "UNH*1*CUSDEC\n*1*2";

    [Fact]
    public void Result_parse_succeeds_and_carries_the_message()
    {
        var result = EdiResultParser.Parse(WellFormedEdi);
        var message = result.Value;

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.NotNull(message);
        Assert.Equal("UNH", message.MessageType);
        Assert.Equal(4, message.Segments.Count);
    }

    [Fact]
    public void Result_parse_fails_and_carries_a_reason()
    {
        var result = EdiResultParser.Parse(MalformedEdi);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal("Segment '*1*2' has no tag.", result.Error);
    }

    [Theory]
    [InlineData("", "847130", 1500.00, "DE", "The declaration reference is blank.")]
    [InlineData("DECL-1002", "84A130", 1500.00, "DE", "Commodity code '84A130' is not a six-digit HS code.")]
    [InlineData("DECL-1003", "847130", 0.0, "DE", "The declared value must be greater than zero.")]
    [InlineData("DECL-1004", "847130", 1500.00, "", "The origin country is blank.")]
    public void Result_validate_reports_the_specific_validation_failure(
        string reference, string commodityCode, double declaredValue, string originCountry, string expectedError)
    {
        var validator = new DeclarationValidator();
        var candidate = new CustomsDeclaration(reference, commodityCode, (decimal)declaredValue, originCountry);

        var result = validator.Validate(candidate);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.Error);
    }

    [Fact]
    public void Result_map_transforms_a_success_and_short_circuits_a_failure()
    {
        var success = Result<int>.Success(21);
        var failure = Result<int>.Failure("no value");

        var mappedSuccess = success.Map(value => value * 2);
        var mappedFailure = failure.Map(value => value * 2);

        Assert.True(mappedSuccess.IsSuccess);
        Assert.Equal(42, mappedSuccess.Value);
        Assert.False(mappedFailure.IsSuccess);
        Assert.Equal("no value", mappedFailure.Error);
    }

    public static TheoryData<string> EdiInputs() => new()
    {
        WellFormedEdi,
        MalformedEdi,
        "",
        "   ",
    };

    [Theory]
    [MemberData(nameof(EdiInputs))]
    public void Both_styles_agree_on_whether_an_input_is_valid(string raw)
    {
        var triedParse = EdiTryParser.TryParse(raw, out _);
        var resultParse = EdiResultParser.Parse(raw);

        Assert.Equal(triedParse, resultParse.IsSuccess);
    }
}
