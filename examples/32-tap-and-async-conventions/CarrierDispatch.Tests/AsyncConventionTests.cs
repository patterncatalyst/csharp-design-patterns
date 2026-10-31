using System.Reflection;
using Xunit;

namespace CarrierDispatch.Tests;

/// <summary>
/// Proves the Task-based Asynchronous Pattern (TAP) conventions this chapter
/// is about actually hold for <see cref="ICarrierGateway"/>: the fast
/// <see cref="ValueTask{TResult}"/> path completes without ever going
/// asynchronous, every asynchronous member is named with the "Async" suffix,
/// and every one of them accepts a <see cref="CancellationToken"/>.
/// </summary>
public class AsyncConventionTests
{
    [Fact]
    public async Task IsReachableAsync_completes_synchronously()
    {
        var gateway = new FakeCarrierGateway(DateTimeOffset.UnixEpoch);

        var reachable = gateway.IsReachableAsync(TestContext.Current.CancellationToken);

        Assert.True(reachable.IsCompleted);
        Assert.True(await reachable);
    }

    [Fact]
    public void Every_task_returning_member_uses_the_async_suffix()
    {
        var taskReturningMethods = typeof(ICarrierGateway).GetMethods()
            .Where(IsTaskLike);

        Assert.All(taskReturningMethods, method => Assert.EndsWith("Async", method.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void Every_async_member_accepts_a_cancellation_token()
    {
        var taskReturningMethods = typeof(ICarrierGateway).GetMethods()
            .Where(IsTaskLike);

        Assert.All(
            taskReturningMethods,
            method => Assert.Contains(
                method.GetParameters(),
                parameter => parameter.ParameterType == typeof(CancellationToken)));
    }

    private static bool IsTaskLike(MethodInfo method)
    {
        var returnType = method.ReturnType;
        if (returnType == typeof(Task) || returnType == typeof(ValueTask))
        {
            return true;
        }

        return returnType.IsGenericType
            && (returnType.GetGenericTypeDefinition() == typeof(Task<>)
                || returnType.GetGenericTypeDefinition() == typeof(ValueTask<>));
    }
}
