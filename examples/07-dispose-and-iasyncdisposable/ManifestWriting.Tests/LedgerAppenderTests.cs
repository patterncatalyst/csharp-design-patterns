using Xunit;

namespace ManifestWriting.Tests;

public class LedgerAppenderTests
{
    [Fact]
    public async Task Await_using_flushes_and_closes_the_ledger_file()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            await using (var appender = new LedgerAppender(path))
            {
                await appender.AppendAsync("SHIP-1 booked for Rotterdam", TestContext.Current.CancellationToken);
            }

            var contents = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            Assert.Contains("SHIP-1 booked for Rotterdam", contents, StringComparison.Ordinal);

            // DisposeAsync already ran when the await using scope exited, so
            // the file is unlocked now: a fresh exclusive open succeeds.
            File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None).Dispose();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Ledger_file_is_locked_until_the_await_using_scope_exits()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            await using (var appender = new LedgerAppender(path))
            {
                await appender.AppendAsync("SHIP-2 booked for Singapore", TestContext.Current.CancellationToken);

                Assert.Throws<IOException>(() =>
                    File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None).Dispose());
            }

            File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None).Dispose();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Appending_after_dispose_async_throws_object_disposed_exception()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var appender = new LedgerAppender(path);
            await appender.DisposeAsync();

            await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                appender.AppendAsync("too late", TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Disposing_async_twice_is_a_harmless_no_op()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var appender = new LedgerAppender(path);
            await appender.AppendAsync("SHIP-3 booked for Santos", TestContext.Current.CancellationToken);

            await appender.DisposeAsync();
            await appender.DisposeAsync();

            var contents = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
            Assert.Contains("SHIP-3 booked for Santos", contents, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
