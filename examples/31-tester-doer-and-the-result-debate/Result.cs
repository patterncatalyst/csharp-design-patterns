using System.Diagnostics.CodeAnalysis;

namespace DeclarationParsing;

/// <summary>
/// The community side of reporting an expected failure: a value that carries
/// either a success with its payload or a failure with a specific reason,
/// so a caller cannot read <see cref="Value"/> without first checking
/// <see cref="IsSuccess"/>, and no exception is ever used for ordinary
/// control flow.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1000:Do not declare static members on generic types",
    Justification = "Success and Failure are the two named constructors of this discriminated-union-style " +
        "result; a static factory is the idiomatic Result<T> shape across the ecosystem (for example " +
        "Nullable<T>'s own static helpers), and the private constructor below leaves no other way to " +
        "build one.")]
public readonly record struct Result<T>
{
    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    public T? Value { get; }

    public string? Error { get; }

    public static Result<T> Success(T value) => new(true, value, null);

    public static Result<T> Failure(string error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(false, default, error);
    }

    /// <summary>
    /// Transforms a success's value and passes a failure through unchanged,
    /// so a chain of operations never needs to branch on <see cref="IsSuccess"/>
    /// itself.
    /// </summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        return IsSuccess
            ? Result<TOut>.Success(selector(Value!))
            : Result<TOut>.Failure(Error!);
    }
}
