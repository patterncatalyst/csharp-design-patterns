using CarrierManifest.Generation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace CarrierManifest.Tests;

/// <summary>
/// Proves the incremental pipeline actually caches: the driver's own tracked
/// steps, not just "the output looked the same," show that an unrelated
/// change elsewhere in the compilation does not force the manifest-reading
/// generator to redo its work. This is the behavior <see cref="EquatableArray{T}"/>
/// exists for; without it, <see cref="CarrierManifestModel"/>'s default
/// equality would compare the underlying array by reference and every run
/// would look "changed."
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
