namespace OmniDoc.Application.Common.Models;

public interface IFailableResult<TSelf> where TSelf : IFailableResult<TSelf>
{
    static abstract TSelf Failure(IReadOnlyList<string> errors, int statusCode);
}

public class Result : IFailableResult<Result>
{
    protected Result(bool isSuccess, IReadOnlyList<string> errors, int statusCode)
    {
        IsSuccess = isSuccess;
        Errors = errors;
        StatusCode = statusCode;
    }

    public bool IsSuccess { get; }

    public IReadOnlyList<string> Errors { get; }

    public string? Error => Errors.Count > 0 ? Errors[0] : null;

    public int StatusCode { get; }

    public static Result Success(int statusCode = 200) => new(true, [], statusCode);

    public static Result Failure(IReadOnlyList<string> errors, int statusCode = 400) => new(false, errors, statusCode);

    public static Result Failure(string error, int statusCode = 400) => new(false, [error], statusCode);
}

public class Result<T> : Result, IFailableResult<Result<T>>
{
    protected Result(bool isSuccess, T? data, IReadOnlyList<string> errors, int statusCode)
        : base(isSuccess, errors, statusCode)
    {
        Data = data;
    }

    public T? Data { get; }

    public static Result<T> Success(T data, int statusCode = 200) => new(true, data, [], statusCode);

    public static new Result<T> Failure(IReadOnlyList<string> errors, int statusCode = 400) => new(false, default, errors, statusCode);

    public static new Result<T> Failure(string error, int statusCode = 400) => new(false, default, [error], statusCode);
}
