using Xunit;

namespace GatewayPooling.Tests;

public sealed class ConnectionPoolTests
{
    private static GatewayConnectionPool CreatePool(int size) =>
        new(size, id => new GatewayConnection(id));

    [Fact]
    public async Task A_rented_connection_comes_from_the_pool()
    {
        await using var pool = CreatePool(3);

        var connection = await pool.RentAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(connection);
        Assert.InRange(connection.Id, 0, 2);
        Assert.Equal(2, pool.Available);
    }

    [Fact]
    public async Task Every_pooled_connection_has_a_distinct_identity()
    {
        await using var pool = CreatePool(3);
        var ct = TestContext.Current.CancellationToken;

        var first = await pool.RentAsync(ct);
        var second = await pool.RentAsync(ct);
        var third = await pool.RentAsync(ct);

        var ids = new HashSet<int> { first.Id, second.Id, third.Id };
        Assert.Equal(new HashSet<int> { 0, 1, 2 }, ids);
    }

    [Fact]
    public async Task An_exhausted_pool_blocks_the_renter_until_a_connection_is_returned()
    {
        await using var pool = CreatePool(2);
        var ct = TestContext.Current.CancellationToken;

        var a = await pool.RentAsync(ct);
        _ = await pool.RentAsync(ct);
        Assert.Equal(0, pool.Available);

        var pendingC = pool.RentAsync(ct).AsTask();
        Assert.False(pendingC.IsCompleted);

        pool.Return(a);
        var c = await pendingC;

        Assert.NotNull(c);
    }

    [Fact]
    public async Task A_returned_connection_is_handed_to_the_next_waiting_renter()
    {
        await using var pool = CreatePool(2);
        var ct = TestContext.Current.CancellationToken;

        var a = await pool.RentAsync(ct);
        _ = await pool.RentAsync(ct);

        var pendingC = pool.RentAsync(ct).AsTask();
        Assert.False(pendingC.IsCompleted);

        pool.Return(a);

        Assert.Same(a, await pendingC);
    }

    [Fact]
    public async Task A_cancelled_rent_token_is_honoured()
    {
        await using var pool = CreatePool(1);
        var ct = TestContext.Current.CancellationToken;

        _ = await pool.RentAsync(ct);

        using var cts = new CancellationTokenSource();
        var pending = pool.RentAsync(cts.Token).AsTask();
        Assert.False(pending.IsCompleted);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task A_disposed_lease_returns_its_connection_to_the_pool()
    {
        await using var pool = CreatePool(1);
        var ct = TestContext.Current.CancellationToken;

        using (var lease = await pool.RentLeaseAsync(ct))
        {
            Assert.Equal(0, pool.Available);
        }

        Assert.Equal(1, pool.Available);
    }

    [Fact]
    public async Task A_pooled_connection_exchanges_a_manifest()
    {
        await using var pool = CreatePool(1);
        var ct = TestContext.Current.CancellationToken;

        var connection = await pool.RentAsync(ct);

        Assert.Equal("ACK:12", await connection.ExchangeAsync("MANIFEST-XYZ", ct));
    }
}
