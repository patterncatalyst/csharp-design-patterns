using System.Diagnostics.CodeAnalysis;

namespace DeclarationParsing;

/// <summary>
/// The <see cref="Result{T}"/> side of validating the same declaration that
/// <see cref="DeclarationProcessor"/> validates: one method that either
/// passes the declaration through unchanged or names the specific field that
/// failed, with no separate Tester call for a caller to forget.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "Validate is an instance method by design: this validator is the stable collaborator a " +
        "caller holds onto, mirroring DeclarationProcessor on the Tester-Doer side; it happens not to touch " +
        "instance state, but a static method would not read as the same kind of collaborator.")]
public sealed class DeclarationValidator
{
    public Result<CustomsDeclaration> Validate(CustomsDeclaration candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (string.IsNullOrWhiteSpace(candidate.Reference))
        {
            return Result<CustomsDeclaration>.Failure("The declaration reference is blank.");
        }

        if (!IsWellFormedCommodityCode(candidate.CommodityCode))
        {
            return Result<CustomsDeclaration>.Failure(
                $"Commodity code '{candidate.CommodityCode}' is not a six-digit HS code.");
        }

        if (candidate.DeclaredValue <= 0m)
        {
            return Result<CustomsDeclaration>.Failure("The declared value must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(candidate.OriginCountry))
        {
            return Result<CustomsDeclaration>.Failure("The origin country is blank.");
        }

        return Result<CustomsDeclaration>.Success(candidate);
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
