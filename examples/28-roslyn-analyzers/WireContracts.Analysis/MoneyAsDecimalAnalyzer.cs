using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace WireContracts.Analysis;

/// <summary>
/// Flags a <see cref="decimal"/>-typed property on any type carrying
/// <c>WireContracts.WireContractAttribute</c>: decimal has no fixed wire
/// representation across serializers and languages, so money that crosses a
/// process boundary belongs in integer minor units instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MoneyAsDecimalAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "SHIP0001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Money must not be modeled as decimal on a wire contract",
        messageFormat: "Property '{0}' on wire-contract type '{1}' uses decimal for money; use integer minor units",
        category: "ShippingContracts",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Wire-contract types should represent money as an integer count of minor units " +
            "(cents) rather than decimal, which has no fixed cross-platform wire representation.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var marker = start.Compilation.GetTypeByMetadataName("WireContracts.WireContractAttribute");
            if (marker is null)
            {
                return;
            }

            start.RegisterSymbolAction(ctx => AnalyzeType(ctx, marker), SymbolKind.NamedType);
        });
    }

    private static void AnalyzeType(SymbolAnalysisContext context, INamedTypeSymbol marker)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        var hasWireContractAttribute = type.GetAttributes()
            .Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, marker));
        if (!hasWireContractAttribute)
        {
            return;
        }

        foreach (var member in type.GetMembers())
        {
            if (member is not IPropertySymbol { Type.SpecialType: SpecialType.System_Decimal } property)
            {
                continue;
            }

            var location = property.Locations.FirstOrDefault() ?? type.Locations.First();
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, property.Name, type.Name));
        }
    }
}
