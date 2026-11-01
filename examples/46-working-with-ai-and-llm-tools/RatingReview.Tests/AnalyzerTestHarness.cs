using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RatingReview.Analysis;

namespace RatingReview.Tests;

/// <summary>
/// Compiles a snippet of source through the raw Roslyn compiler API and runs
/// <see cref="SingleImplementationStrategyAnalyzer"/> over it, with no
/// third-party analyzer testing package involved. Each caller's snippet is
/// self-contained, needing nothing beyond the .NET runtime's own reference
/// assemblies to compile.
/// </summary>
internal static class AnalyzerTestHarness
{
    private static readonly ImmutableArray<MetadataReference> FrameworkReferences = BuildFrameworkReferences();

    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source, CancellationToken cancellationToken)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);

        var compilation = CSharpCompilation.Create(
            assemblyName: "RatingReview.Tests.Snippet",
            syntaxTrees: [syntaxTree],
            references: FrameworkReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compilationErrors = compilation.GetDiagnostics(cancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
        if (!compilationErrors.IsEmpty)
        {
            var messages = string.Join(Environment.NewLine, compilationErrors.Select(diagnostic => diagnostic.ToString()));
            throw new InvalidOperationException($"Snippet failed to compile:{Environment.NewLine}{messages}");
        }

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new SingleImplementationStrategyAnalyzer());
        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);

        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(cancellationToken);
        return diagnostics
            .Where(diagnostic => diagnostic.Id == SingleImplementationStrategyAnalyzer.DiagnosticId)
            .ToImmutableArray();
    }

    /// <summary>
    /// The trusted-platform-assemblies list for the running .NET host, which
    /// is the standard way to get a complete, in-box set of reference
    /// assemblies (System.Runtime, System.Private.CoreLib, and so on) for an
    /// in-memory compilation without pulling in a reference-assembly NuGet
    /// package.
    /// </summary>
    private static ImmutableArray<MetadataReference> BuildFrameworkReferences()
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;

        if (string.IsNullOrEmpty(trustedPlatformAssemblies))
        {
            // Defensive fallback for a host that does not publish a TPA list;
            // typeof(object)'s assembly is always loadable from the running
            // process and covers the handful of core types these snippets need.
            return [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)];
        }

        var paths = trustedPlatformAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        return [.. paths.Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))];
    }
}
