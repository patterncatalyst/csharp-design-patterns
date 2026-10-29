using Xunit;

namespace ManifestWriting.Tests;

public class ManifestWriterTests
{
    [Fact]
    public void Handle_is_released_as_soon_as_the_using_scope_exits()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            using (var writer = new ManifestWriter(path))
            {
                writer.WriteLine(new ManifestEntry("SHIP-1", "Rotterdam", 820m));

                // While the writer is still alive it holds the handle
                // exclusively: a second exclusive open must fail.
                Assert.Throws<IOException>(() =>
                    File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None).Dispose());
            }

            // Dispose already ran when the using scope exited above, so the
            // handle is free right now, not at some later, unpredictable GC
            // pass. FileShare.None proves nobody else still holds it.
            File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None).Dispose();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Writing_after_dispose_throws_object_disposed_exception()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var writer = new ManifestWriter(path);
            writer.Dispose();

            Assert.Throws<ObjectDisposedException>(() =>
                writer.WriteLine(new ManifestEntry("SHIP-2", "Singapore", 410m)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Disposing_twice_is_a_harmless_no_op()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var writer = new ManifestWriter(path);
            writer.WriteLine(new ManifestEntry("SHIP-3", "Santos", 150m));

            writer.Dispose();
            writer.Dispose();

            var contents = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            Assert.Contains("SHIP-3", contents, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
