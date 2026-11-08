using CarrierManifest.Generation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace CarrierManifest.Tests;

/// <summary>
/// Reads the driver's own tracked steps, not just the generated output.
/// An unrelated change elsewhere in the compilation leaves both stages cached.
/// A re-read manifest with the same content reruns the parse, and
/// <see cref="EquatableArray{T}"/> gives an equal model, so the parse reports
/// unchanged and the combine stays cached.
/// </summary>
public sealed class GeneratorCachingTests
{
    [Fact]
    public void Incremental_pipeline_caches_when_the_manifest_is_unchanged()
    {
        var manifestText = new GeneratorEmitTests.InMemoryAdditionalText(
            "carriers.manifest",
            GeneratorEmitTests.ManifestContent);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new CarrierGatewayGenerator().AsSourceGenerator()],
            additionalTexts: [manifestText],
            parseOptions: null,
            optionsProvider: null,
            driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));

        // Two separately built but content-equivalent compilations stand in
        // for "the manifest didn't change, but something else in the project
        // was recompiled", the ordinary case an incremental generator has to
        // stay cheap for.
        var firstCompilation = GeneratorEmitTests.CreateCompilation(GeneratorEmitTests.CarrierGatewayInterfaceSource);
        driver = driver.RunGenerators(firstCompilation, TestContext.Current.CancellationToken);
        var firstRun = driver.GetRunResult().Results.Single();

        AssertAllReasons(firstRun, "CarrierManifest.Parse", IncrementalStepRunReason.New);
        AssertAllReasons(firstRun, "CarrierManifest.Combine", IncrementalStepRunReason.New);

        var secondCompilation = GeneratorEmitTests.CreateCompilation(GeneratorEmitTests.CarrierGatewayInterfaceSource);
        driver = driver.RunGenerators(secondCompilation, TestContext.Current.CancellationToken);
        var secondRun = driver.GetRunResult().Results.Single();

        AssertAllReasonsAreCachedOrUnchanged(secondRun, "CarrierManifest.Parse");
        AssertAllReasonsAreCachedOrUnchanged(secondRun, "CarrierManifest.Combine");
    }

    [Fact]
    public void Equal_content_manifest_reread_leaves_the_model_unchanged()
    {
        var manifestText = new GeneratorEmitTests.InMemoryAdditionalText(
            "carriers.manifest",
            GeneratorEmitTests.ManifestContent);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new CarrierGatewayGenerator().AsSourceGenerator()],
            additionalTexts: [manifestText],
            parseOptions: null,
            optionsProvider: null,
            driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));

        var compilation = GeneratorEmitTests.CreateCompilation(GeneratorEmitTests.CarrierGatewayInterfaceSource);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);

        var rereadText = new GeneratorEmitTests.InMemoryAdditionalText(
            "carriers.manifest",
            GeneratorEmitTests.ManifestContent);
        driver = driver.ReplaceAdditionalText(manifestText, rereadText);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        var rereadRun = driver.GetRunResult().Results.Single();

        AssertAllReasons(rereadRun, "CarrierManifest.Parse", IncrementalStepRunReason.Unchanged);
        AssertAllReasons(rereadRun, "CarrierManifest.Combine", IncrementalStepRunReason.Cached);
    }

    private static void AssertAllReasons(GeneratorRunResult result, string trackingName, IncrementalStepRunReason expected)
    {
        var steps = result.TrackedSteps[trackingName];
        Assert.NotEmpty(steps);

        foreach (var step in steps)
        {
            foreach (var output in step.Outputs)
            {
                Assert.Equal(expected, output.Reason);
            }
        }
    }

    private static void AssertAllReasonsAreCachedOrUnchanged(GeneratorRunResult result, string trackingName)
    {
        var steps = result.TrackedSteps[trackingName];
        Assert.NotEmpty(steps);

        foreach (var step in steps)
        {
            foreach (var output in step.Outputs)
            {
                Assert.True(
                    output.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                    $"Expected '{trackingName}' to be cached or unchanged on the second run, but it reported {output.Reason}.");
            }
        }
    }
}
