using System.Diagnostics.CodeAnalysis;

namespace DeclarationParsing;

/// <summary>
/// The Framework Design Guidelines side of validating a declaration: a
/// Tester (<see cref="CanProcess"/>) a caller is expected to run first, and a
/// Doer (<see cref="Process"/>) that assumes the tester already passed and
/// throws if it did not. Neither method ever throws for a merely invalid
/// declaration; <see cref="CanProcess"/> reports that with its boolean
/// result, and <see cref="Process"/> throws only when a caller skipped the
/// tester, a programming error rather than an expected outcome.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "CanProcess and Process are instance methods by design: this processor is the stable " +
        "collaborator a caller holds onto across a Tester call and a later Doer call, with the declaration " +
        "passed as an argument rather than held as a field; neither method happens to touch instance state, " +
        "but a static method would not read as the Tester-Doer collaborator shape this chapter demonstrates.")]
public sealed class DeclarationProcessor
{
    public bool CanProcess(CustomsDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        return !string.IsNullOrWhiteSpace(declaration.Reference)
            && IsWellFormedCommodityCode(declaration.CommodityCode)
            && declaration.DeclaredValue > 0m
            && !string.IsNullOrWhiteSpace(declaration.OriginCountry);
    }

    public ProcessedDeclaration Process(CustomsDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        if (!CanProcess(declaration))
        {
            throw new InvalidOperationException(
                $"Declaration '{declaration.Reference}' was not tested with {nameof(CanProcess)} " +
                "before processing, or it failed that test.");
        }

        return new ProcessedDeclaration(declaration.Reference, declaration.DeclaredValue);
    }

    private static bool IsWellFormedCommodityCode(string commodityCode)
    {
        if (string.IsNullOrEmpty(commodityCode) || commodityCode.Length != 6)
        {
            return false;
        }

        foreach (var digit in commodityCode)
        {
            if (!char.IsDigit(digit))
            {
                return false;
            }
        }

        return true;
    }
}
