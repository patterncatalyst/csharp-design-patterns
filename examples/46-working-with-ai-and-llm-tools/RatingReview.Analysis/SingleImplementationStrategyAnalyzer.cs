using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RatingReview.Analysis;

/// <summary>
/// Flags a Strategy interface, by convention one whose name ends in
/// "Strategy", that has exactly one implementing type in the compilation:
/// the "strategy tower with one strategy" an LLM commonly emits when it
/// reaches for a design pattern before the problem has earned it.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SingleImplementationStrategyAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "SHIP0002";

    private const string StrategySuffix = "Strategy";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Strategy interface has a single implementation",
        messageFormat: "Strategy interface '{0}' has a single implementation '{1}'; confirm the abstraction earns its place",
        category: "ShippingReview",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A Strategy interface with only one implementation is a common shape for " +
            "pattern-shaped output from an LLM: review it before it merges to confirm the " +
            "abstraction is pulling its weight, rather than anticipating a second strategy " +
            "that never arrives.",
        // Counting implementers across the whole compilation can only be
        // judged once every type has been seen, so this diagnostic is
        // reported from a CompilationEndAction; RS1037 requires the tag say
        // so on the descriptor itself.
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            // Keyed by ISymbol (not the more specific INamedTypeSymbol) so the
            // dictionary comparer constructor overload resolves unambiguously
            // against SymbolEqualityComparer.Default, which implements
            // IEqualityComparer<ISymbol>.
            var candidateInterfaces = new ConcurrentDictionary<ISymbol, byte>(SymbolEqualityComparer.Default);
            var implementersByInterface = new ConcurrentDictionary<ISymbol, ConcurrentBag<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

            start.RegisterSymbolAction(
                ctx => CollectType(ctx, candidateInterfaces, implementersByInterface),
                SymbolKind.NamedType);

            start.RegisterCompilationEndAction(
                end => ReportSingleImplementations(end, candidateInterfaces, implementersByInterface));
        });
    }

    private static bool IsStrategyInterface(INamedTypeSymbol type) =>
        type.TypeKind == TypeKind.Interface && type.Name.EndsWith(StrategySuffix, StringComparison.Ordinal);

    private static void CollectType(
        SymbolAnalysisContext context,
        ConcurrentDictionary<ISymbol, byte> candidateInterfaces,
        ConcurrentDictionary<ISymbol, ConcurrentBag<INamedTypeSymbol>> implementersByInterface)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (IsStrategyInterface(type))
        {
            candidateInterfaces.TryAdd(type, 0);
            return;
        }

        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            return;
        }

        foreach (var implemented in type.AllInterfaces)
        {
            if (!IsStrategyInterface(implemented))
            {
                continue;
            }

            candidateInterfaces.TryAdd(implemented, 0);
            var implementers = implementersByInterface.GetOrAdd(implemented, static _ => []);
            implementers.Add(type);
        }
    }

    private static void ReportSingleImplementations(
        CompilationAnalysisContext context,
        ConcurrentDictionary<ISymbol, byte> candidateInterfaces,
        ConcurrentDictionary<ISymbol, ConcurrentBag<INamedTypeSymbol>> implementersByInterface)
    {
        foreach (var candidate in candidateInterfaces.Keys)
        {
            var interfaceSymbol = (INamedTypeSymbol)candidate;

            if (!implementersByInterface.TryGetValue(interfaceSymbol, out var implementers) || implementers.Count != 1)
            {
                continue;
            }

            var implementation = implementers.Single();
            var location = interfaceSymbol.Locations.FirstOrDefault(static location => location.IsInSource)
                ?? Location.None;

            context.ReportDiagnostic(Diagnostic.Create(Rule, location, interfaceSymbol.Name, implementation.Name));
        }
    }
}
