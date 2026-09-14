namespace HiringPlatform.Application.Common;

public enum ErrorKind
{
    Validation, NotFound, Forbidden, Conflict, Unauthorized
}

public sealed record ApplicationError(
    ErrorKind Kind,
    string Message
)
{
    public static ApplicationError Validation(string message) => new(ErrorKind.Validation, message);
    public static ApplicationError NotFound(string message) => new(ErrorKind.NotFound, message);
    public static ApplicationError Forbidden(string message) => new(ErrorKind.Forbidden, message);
    public static ApplicationError Conflict(string message) => new(ErrorKind.Conflict, message);
    public static ApplicationError Unauthorized(string message) => new(ErrorKind.Unauthorized, message);
}

/// <summary>Same shape as cv-sv <c>Result&lt;T&gt;</c>: success with data, or failure with an error.</summary>
public readonly record struct Result<T>
{
    private Result(T? value, ApplicationError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value
    {
        get;
    }
    public ApplicationError? Error
    {
        get;
    }
    public bool IsSuccess => Error is null;

    public static Result<T> Ok(T value) => new(value, null);
    public static Result<T> Fail(ApplicationError error) => new(default, error);

    public static implicit operator Result<T>(T value) => Ok(value);
    public static implicit operator Result<T>(ApplicationError error) => Fail(error);
}

/// <summary>Stand-in for void results.</summary>
public readonly record struct Unit
{
    public static readonly Unit Value;
}
