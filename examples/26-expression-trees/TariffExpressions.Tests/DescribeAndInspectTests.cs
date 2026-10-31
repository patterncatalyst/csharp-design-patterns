using System.Linq.Expressions;
using Xunit;

namespace TariffExpressions.Tests;

public class DescribeAndInspectTests
{
    [Fact]
    public void Authored_rule_compiles_and_computes_the_rate()
    {
        var request = new RateRequest(10m, 500m, "Denver", false);
        var compiled = TariffExpressionLibrary.WeightRate.Compile();

        var rate = compiled(request);

        Assert.Equal(25.00m, rate);
    }

    [Fact]
    public void Describe_renders_a_readable_string_for_a_weight_rule()
    {
        var describer = new RateDescriber();

        var description = describer.Describe(TariffExpressionLibrary.WeightRate);

        Assert.Equal("(r.WeightKilograms * 2.50)", description);
    }

    [Fact]
    public void Describe_handles_a_composed_binary_tree()
    {
        Expression<Func<RateRequest, decimal>> combined =
            r => (r.WeightKilograms * 2.50m) + (r.DeclaredValue * 0.015m);
        var describer = new RateDescriber();

        var description = describer.Describe(combined);

        Assert.Equal("((r.WeightKilograms * 2.50) + (r.DeclaredValue * 0.015))", description);
    }

    [Fact]
    public void ReferencedPropertyCollector_finds_the_properties_a_rule_touches()
    {
        Expression<Func<RateRequest, decimal>> rule =
            r => (r.WeightKilograms * 2.50m) + (r.DeclaredValue * 0.015m);
        var collector = new ReferencedPropertyCollector();

        var properties = collector.Collect(rule);

        Assert.Equal(2, properties.Count);
        Assert.Contains("WeightKilograms", properties);
        Assert.Contains("DeclaredValue", properties);
    }

    [Fact]
    public void ReferencedPropertyCollector_reads_without_executing()
    {
        Expression<Func<RateRequest, decimal>> divideByZero = r => r.WeightKilograms / 0m;
        var collector = new ReferencedPropertyCollector();

        var properties = collector.Collect(divideByZero);

        Assert.Contains("WeightKilograms", properties);
        Assert.Throws<DivideByZeroException>(
            () => divideByZero.Compile()(new RateRequest(10m, 0m, "Denver", false)));
    }
}
