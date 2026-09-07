using System.Diagnostics.CodeAnalysis;

namespace ProcurementCopilot.Domain.Common;

/// <summary>
/// The outcome of an operation that can succeed with a value or fail with an <see cref="Error"/>.
/// Business rules return <see cref="Result{T}"/> instead of throwing exceptions.
/// </summary>
/// <typeparam name="T">The success value type.</typeparam>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        _value = value;
        Error = Error.None;
    }

    private Result(Error error)
    {
        Error = error;
    }

    /// <summary>Gets the error, or <see cref="Error.None"/> on success.</summary>
    public Error Error { get; }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess => Error == Error.None;

    /// <summary>Gets a value indicating whether the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Gets the success value. Throws when accessed on a failed result.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read Value of a failed result ({Error}).");

    /// <summary>Creates a successful result.</summary>
    public static Result<T> Success(T value) => new(value);

    /// <summary>Creates a failed result.</summary>
    public static Result<T> Failure(Error error) => new(error);

    /// <summary>Implicitly wraps a value in a successful result.</summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>Implicitly wraps an error in a failed result.</summary>
    public static implicit operator Result<T>(Error error) => Failure(error);

    /// <summary>Projects the success value or the error to a single output.</summary>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(Error);

    /// <summary>Transforms the success value, propagating failures unchanged.</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> map) =>
        IsSuccess ? Result<TOut>.Success(map(_value!)) : Result<TOut>.Failure(Error);

    /// <summary>Chains another result-producing operation, propagating failures unchanged.</summary>
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> bind) =>
        IsSuccess ? bind(_value!) : Result<TOut>.Failure(Error);

    /// <inheritdoc />
    public override string ToString() => IsSuccess ? $"Success({_value})" : $"Failure({Error})";
}
