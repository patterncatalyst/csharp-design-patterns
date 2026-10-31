using System.Globalization;
using Microsoft.CodeAnalysis;
using WireContracts.Analysis;
using Xunit;

namespace WireContracts.Tests;

/// <summary>
/// Drives <see cref="MoneyAsDecimalAnalyzer"/> through <see cref="AnalyzerTestHarness"/>
/// against small, self-contained snippets. Each snippet declares its own
/// <c>WireContracts.WireContractAttribute</c> so no snippet depends on the
/// real <c>WireContracts</c> assembly; this keeps every test hermetic.
/// </summary>
public sealed class MoneyAsDecimalAnalyzerTests
{
    private const string MarkerAttribute = """
        namespace WireContracts
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
            public sealed class WireContractAttribute : System.Attribute
            {
            }
        }
        """;

    [Fact]
    public async Task Fires_on_decimal_money_in_a_wire_contract()
    {
        var source = MarkerAttribute + """
            namespace WireContracts
            {
                [WireContract]
                public class RateQuote
                {
                    public decimal Amount { get; set; }
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(source, TestContext.Current.CancellationToken);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Quiet_on_a_compliant_wire_contract()
    {
        var source = MarkerAttribute + """
            namespace WireContracts
            {
                [WireContract]
                public class RateQuote
                {
                    public long AmountMinorUnits { get; set; }
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(source, TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Quiet_on_decimal_outside_a_wire_contract()
    {
        var source = MarkerAttribute + """
            namespace WireContracts
            {
                public class InternalRateCalculation
                {
                    public decimal Amount { get; set; }
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(source, TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Reports_correct_id_severity_and_location()
    {
        var source = MarkerAttribute + """
            namespace WireContracts
            {
                [WireContract]
                public class RateQuote
                {
                    public decimal Amount { get; set; }
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(source, TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(MoneyAsDecimalAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.True(diagnostic.Location.IsInSource);
        Assert.Contains("Amount", diagnostic.Location.SourceTree!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Flags_every_offending_property_on_a_type_with_multiple_violations()
    {
        var source = MarkerAttribute + """
            namespace WireContracts
            {
                [WireContract]
                public class BookingCharge
                {
                    public decimal Price { get; set; }

                    public decimal Tax { get; set; }
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(source, TestContext.Current.CancellationToken);

        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public async Task Message_names_the_property_and_type()
    {
        var source = MarkerAttribute + """
            namespace WireContracts
            {
                [WireContract]
                public class BookingCharge
                {
                    public decimal Price { get; set; }
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(source, TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(diagnostics);
        var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
        Assert.Contains("Price", message, StringComparison.Ordinal);
        Assert.Contains("BookingCharge", message, StringComparison.Ordinal);
    }
}
