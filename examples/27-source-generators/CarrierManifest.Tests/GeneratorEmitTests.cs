using System.Collections.Immutable;
using System.Text;
using System.Threading;
using CarrierManifest.Generation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace CarrierManifest.Tests;

/// <summary>
/// Drives <see cref="CarrierGatewayGenerator"/> directly with a raw
/// <see cref="CSharpGeneratorDriver"/> instead of a pre-built xunit-v2-era
/// verifier package, proving the generator's output and wiring every claim
/// the chapter makes about it.
/// </summary>
public sealed class GeneratorEmitTests
{
    internal const string ManifestContent =
        "DHLX,DHL Express,https://api.dhlx.example\n" +
        "FDXG,FedEx Ground,https://api.fdxg.example\n" +
        "UPSX,UPS Worldwide,https://api.upsx.example\n" +
        "USPS,USPS Priority,https://api.usps.example\n";

    internal const string CarrierGatewayInterfaceSource = """
        namespace CarrierManifest;

        public interface ICarrierGateway
        {
            string Code { get; }

            string DisplayName { get; }

            string Endpoint { get; }
        }
        """;

    [Fact]
    public void Generator_emits_an_adapter_for_each_manifest_entry()
    {
        var manifestText = new InMemoryAdditionalText("carriers.manifest", ManifestContent);
        var compilation = CreateCompilation(CarrierGatewayInterfaceSource);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new CarrierGatewayGenerator().AsSourceGenerator()],
            additionalTexts: [manifestText]);

        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        var runResult = driver.GetRunResult();

        var generatedTree = Assert.Single(runResult.GeneratedTrees);
        var generatedSource = generatedTree.GetText(TestContext.Current.CancellationToken).ToString();

        Assert.Contains("public sealed class DHLXCarrierGateway : ICarrierGateway", generatedSource, StringComparison.Ordinal);
        Assert.Contains("public sealed class FDXGCarrierGateway : ICarrierGateway", generatedSource, StringComparison.Ordinal);
        Assert.Contains("public sealed class UPSXCarrierGateway : ICarrierGateway", generatedSource, StringComparison.Ordinal);
        Assert.Contains("public sealed class USPSCarrierGateway : ICarrierGateway", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generated_registry_wires_all_manifest_entries()
    {
        var manifestText = new InMemoryAdditionalText("carriers.manifest", ManifestContent);
        var compilation = CreateCompilation(CarrierGatewayInterfaceSource);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new CarrierGatewayGenerator().AsSourceGenerator()],
            additionalTexts: [manifestText]);

        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        var runResult = driver.GetRunResult();

        var generatedTree = Assert.Single(runResult.GeneratedTrees);
        var generatedSource = generatedTree.GetText(TestContext.Current.CancellationToken).ToString();

        Assert.Contains("public static class GeneratedCarrierRegistry", generatedSource, StringComparison.Ordinal);
        Assert.Contains("new DHLXCarrierGateway(),", generatedSource, StringComparison.Ordinal);
        Assert.Contains("new FDXGCarrierGateway(),", generatedSource, StringComparison.Ordinal);
        Assert.Contains("new UPSXCarrierGateway(),", generatedSource, StringComparison.Ordinal);
        Assert.Contains("new USPSCarrierGateway(),", generatedSource, StringComparison.Ordinal);
        Assert.Contains("public static ICarrierGateway? Find(string code)", generatedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Generated_code_compiles_with_no_errors()
    {
        var manifestText = new InMemoryAdditionalText("carriers.manifest", ManifestContent);

        // Includes the hand-written ICarrierGateway contract so the generated
        // adapters below have something to compile against, the same way the
        // real CarrierManifest.csproj does.
        var compilation = CreateCompilation(CarrierGatewayInterfaceSource);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new CarrierGatewayGenerator().AsSourceGenerator()],
            additionalTexts: [manifestText]);

        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var updatedCompilation,
            out var generatorDiagnostics,
            TestContext.Current.CancellationToken);

        Assert.Empty(generatorDiagnostics);

        var compileErrors = updatedCompilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.Empty(compileErrors);
    }

    [Fact]
    public void Generator_produces_no_adapter_output_without_a_manifest()
    {
        var compilation = CreateCompilation(CarrierGatewayInterfaceSource);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new CarrierGatewayGenerator().AsSourceGenerator()],
            additionalTexts: []);

        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        var runResult = driver.GetRunResult();

        Assert.Empty(runResult.GeneratedTrees);
    }

    /// <summary>
    /// A fully in-memory <see cref="AdditionalText"/>, standing in for a file
    /// on disk so the generator can be driven without a real project on
    /// disk.
    /// </summary>
    internal sealed class InMemoryAdditionalText(string path, string content) : AdditionalText
    {
        private readonly SourceText _text = SourceText.From(content, Encoding.UTF8);

        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }

    internal static CSharpCompilation CreateCompilation(params string[] sources)
    {
        var syntaxTrees = sources.Select(source => CSharpSyntaxTree.ParseText(source)).ToArray();

        return CSharpCompilation.Create(
            assemblyName: "CarrierManifest.GeneratorSpike",
            syntaxTrees: syntaxTrees,
            references: PlatformReferences(),
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    private static List<MetadataReference> PlatformReferences()
    {
        var trustedPlatformAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(System.IO.Path.PathSeparator);

        return trustedPlatformAssemblies
            .Select(assemblyPath => (MetadataReference)MetadataReference.CreateFromFile(assemblyPath))
            .ToList();
    }
}
