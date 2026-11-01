using Xunit;

namespace GatewayPooling.Tests;

public sealed class ManifestBufferTests
{
    private static readonly IReadOnlyList<ManifestLine> SampleLines =
    [
        new ManifestLine("CARRIER", "ACME-FREIGHT"),
        new ManifestLine("WEIGHT_KG", "1540"),
        new ManifestLine("DEST", "ROTTERDAM")
    ];

    [Fact]
    public void Serializing_a_manifest_returns_the_rented_buffer_to_the_pool_and_clears_it()
    {
        var pool = new RecordingArrayPool();
        var writer = new ManifestWriter(pool);

        _ = writer.Serialize(SampleLines);

        Assert.Equal(1, pool.RentCount);
        Assert.Equal(1, pool.ReturnCount);
        Assert.True(pool.LastReturnedCleared);
    }

    [Fact]
    public void A_returned_buffer_is_reused_by_the_next_serialization()
    {
        var pool = new RecordingArrayPool();
        var writer = new ManifestWriter(pool);

        var firstBytes = writer.Serialize(SampleLines);
        var firstReturnedArray = pool.LastReturned;

        var secondBytes = writer.Serialize(SampleLines);
        var secondRentedArray = pool.LastRented;

        Assert.Same(firstReturnedArray, secondRentedArray);
        Assert.Equal(firstBytes, secondBytes);

        var decoded = ManifestWriter.Parse(secondBytes);
        Assert.Equal(SampleLines.Count, decoded.Count);
        for (var i = 0; i < SampleLines.Count; i++)
        {
            Assert.Equal(SampleLines[i].Code, decoded[i].Code);
            Assert.Equal(SampleLines[i].Value, decoded[i].Value);
        }
    }
}
