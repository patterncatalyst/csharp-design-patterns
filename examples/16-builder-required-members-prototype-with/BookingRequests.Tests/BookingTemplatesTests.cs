using Xunit;

namespace BookingRequests.Tests;

/// <summary>
/// Proves Prototype via <c>with</c>: cloning and amending
/// <see cref="BookingTemplates.StandardDomestic"/> produces a new,
/// independent instance, and a collection-expression spread copies the
/// template's line items into a fresh list rather than sharing the
/// underlying one.
/// </summary>
public class BookingTemplatesTests
{
    [Fact]
    public void With_expression_clones_and_amends_without_mutating_the_template()
    {
        var template = BookingTemplates.StandardDomestic;

        var amended = template with { ServiceLevel = "express", Destination = "Atlanta, GA" };

        Assert.Equal("express", amended.ServiceLevel);
        Assert.Equal("Atlanta, GA", amended.Destination);
        Assert.Equal(template.Origin, amended.Origin);
        Assert.Equal(template.DeclaredValue, amended.DeclaredValue);
        Assert.Equal(template.CustomerReference, amended.CustomerReference);
        Assert.NotEqual(template, amended);

        Assert.Equal("standard", template.ServiceLevel);
        Assert.Equal("Memphis, TN", template.Destination);
    }

    [Fact]
    public void Collection_spread_amend_copies_rather_than_shares_the_template_line_items()
    {
        var template = BookingTemplates.StandardDomestic;
        var originalCount = template.LineItems.Count;
        var extraItem = new LineItem("Residential delivery fee", Money.Of(15.00m), 1);

        var amended = template with { LineItems = [.. template.LineItems, extraItem] };

        Assert.Equal(originalCount + 1, amended.LineItems.Count);
        Assert.Equal(originalCount, template.LineItems.Count);
        Assert.Contains(extraItem, amended.LineItems);
        Assert.DoesNotContain(extraItem, template.LineItems);
    }
}
