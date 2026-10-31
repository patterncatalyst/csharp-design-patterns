using System.Linq.Expressions;
using Xunit;

namespace TariffExpressions.Tests;

public class RewriteAndComposeTests
{
    public static TheoryData<bool, bool, bool> Combinations => new()
    {
        { true, true, true },
        { true, false, false },
        { false, true, false },
        { false, false, false },
    };

    [Fact]
    public void Inflate_scales_decimal_constants_and_the_rewritten_tree_compiles_to_the_scaled_result()
    {
        var inflator = new RateInflator();

        var inflated = inflator.Inflate(TariffExpressionLibrary.WeightRate, 1.10m);
        var compiled = inflated.Compile();
        var rate = compiled(new RateRequest(10m, 0m, "Denver", false));

        Assert.Equal(27.5m, rate);
    }

    [Fact]
    public void Inflate_leaves_non_decimal_constants_untouched()
    {
        Expression<Func<RateRequest, decimal>> rule = r =>
            r.Destination == "Remote" ? r.WeightKilograms * 2.50m : r.WeightKilograms * 2.00m;
        var inflator = new RateInflator();

        var inflated = inflator.Inflate(rule, 2m);
        var compiled = inflated.Compile();
        var remoteRate = compiled(new RateRequest(10m, 0m, "Remote", false));
        var otherRate = compiled(new RateRequest(10m, 0m, "Denver", false));

        Assert.Equal(50m, remoteRate);
        Assert.Equal(40m, otherRate);
    }

    [Fact]
    public void ParameterReplacer_rebinds_a_lambda_onto_a_new_parameter()
    {
        var original = TariffExpressionLibrary.IsHeavy;
        var newParameter = Expression.Parameter(typeof(RateRequest), "shipment");
        var replacer = new ParameterReplacer(original.Parameters[0], newParameter);

        var reboundBody = replacer.Visit(original.Body)!;
        var rebuilt = Expression.Lambda<Func<RateRequest, bool>>(reboundBody, newParameter);

        Assert.Equal("shipment", rebuilt.Parameters[0].Name);
        var compiled = rebuilt.Compile();
        Assert.True(compiled(new RateRequest(25m, 0m, "Denver", false)));
        Assert.False(compiled(new RateRequest(5m, 0m, "Denver", false)));
    }

    [Fact]
    public void And_composes_two_predicates_over_one_parameter()
    {
        var heavy = TariffExpressionLibrary.IsHeavy;
        Expression<Func<RateRequest, bool>> hazardous = r => r.Hazardous;

        var combined = PredicateComposer.And(heavy, hazardous);

        Assert.Same(heavy.Parameters[0], combined.Parameters[0]);
        Assert.Equal(ExpressionType.AndAlso, combined.Body.NodeType);
    }

    [Theory]
    [MemberData(nameof(Combinations))]
    public void And_composition_requires_both_conditions(bool heavy, bool hazardous, bool expected)
    {
        var combined = PredicateComposer.And(TariffExpressionLibrary.IsHeavy, r => r.Hazardous);
        var request = new RateRequest(heavy ? 25m : 5m, 0m, "Denver", hazardous);

        var result = combined.Compile()(request);

        Assert.Equal(expected, result);
    }
}
