using System.Globalization;
using Microsoft.CodeAnalysis;
using RatingReview.Analysis;
using Xunit;

namespace RatingReview.Tests;

/// <summary>
/// Drives <see cref="SingleImplementationStrategyAnalyzer"/> through
/// <see cref="AnalyzerTestHarness"/> against small, self-contained snippets.
/// Every snippet is a complete compilation unit, so no test depends on the
/// real <c>RatingReview</c> assembly.
/// </summary>
public sealed class SingleImplementationStrategyAnalyzerTests
{
    [Fact]
    public async Task Fires_on_a_strategy_interface_with_one_implementation()
    {
        const string source = """
            namespace RatingReview
            {
                public interface ISurchargeStrategy
                {
                    decimal Compute(decimal baseAmount);
                }

                public sealed class FuelSurchargeStrategy : ISurchargeStrategy
                {
                    public decimal Compute(decimal baseAmount) => baseAmount * 0.12m;
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(source, TestContext.Current.CancellationToken);

        Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Quiet_on_a_strategy_interface_with_two_implementations()
    {
        const string source = """
            namespace RatingReview
            {
                public interface IDemurrageStrategy
                {
                    decimal Compute(int daysLate);
                }

                public sealed class FlatDemurrageStrategy : IDemurrageStrategy
                {
                    public decimal Compute(int daysLate) => daysLate * 25m;
                }

                public sealed class TieredDemurrageStrategy : IDemurrageStrategy
                {
                    public decimal Compute(int daysLate) => daysLate > 5 ? daysLate * 40m : daysLate * 20m;
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(source, TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Quiet_on_a_single_implementation_interface_not_named_strategy()
    {
        const string source = """
            namespace RatingReview
            {
                public interface ISlotAllocator
                {
                    int Allocate(int containerCount);
                }

                public sealed class FirstFitSlotAllocator : ISlotAllocator
                {
                    public int Allocate(int containerCount) => containerCount;
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(source, TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Quiet_when_a_strategy_interface_has_no_implementations()
    {
        const string source = """
            namespace RatingReview
            {
                public interface IPalletizationStrategy
                {
                    int PalletsNeeded(int unitCount);
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(source, TestContext.Current.CancellationToken);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Reports_correct_id_severity_and_location()
    {
        const string source = """
            namespace RatingReview
            {
                public interface IRoutingStrategy
                {
                    string SelectRoute(string origin, string destination);
                }

                public sealed class ShortestPathRoutingStrategy : IRoutingStrategy
                {
                    public string SelectRoute(string origin, string destination) => $"{origin}->{destination}";
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(source, TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(SingleImplementationStrategyAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.True(diagnostic.Location.IsInSource);
        var spanText = diagnostic.Location.SourceTree!.ToString()
            .Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length);
        Assert.Equal("IRoutingStrategy", spanText);
    }

    [Fact]
    public async Task Message_names_the_interface_and_its_sole_implementation()
    {
        const string source = """
            namespace RatingReview
            {
                public interface IConsolidationStrategy
                {
                    bool ShouldConsolidate(int shipmentCount);
                }

                public sealed class VolumeConsolidationStrategy : IConsolidationStrategy
                {
                    public bool ShouldConsolidate(int shipmentCount) => shipmentCount > 3;
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(source, TestContext.Current.CancellationToken);

        var diagnostic = Assert.Single(diagnostics);
        var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
        Assert.Contains("IConsolidationStrategy", message, StringComparison.Ordinal);
        Assert.Contains("VolumeConsolidationStrategy", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Flags_each_single_implementation_strategy_interface_independently()
    {
        const string source = """
            namespace RatingReview
            {
                public interface ICustomsStrategy
                {
                    decimal Duty(decimal declaredValue);
                }

                public sealed class FlatRateCustomsStrategy : ICustomsStrategy
                {
                    public decimal Duty(decimal declaredValue) => declaredValue * 0.05m;
                }

                public interface IInsuranceStrategy
                {
                    decimal Premium(decimal declaredValue);
                }

                public sealed class StandardInsuranceStrategy : IInsuranceStrategy
                {
                    public decimal Premium(decimal declaredValue) => declaredValue * 0.01m;
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(source, TestContext.Current.CancellationToken);

        Assert.Equal(2, diagnostics.Length);
    }
}
