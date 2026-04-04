namespace CatalogService.Common;

public enum ResultStatus
{
    Success,
    NotFound,
    Conflict,
    ValidationError
}

public class ServiceResult<T>
{
    public ResultStatus Status { get; init; }
    public string? Error { get; init; }
    public T? Data { get; init; }

    public static ServiceResult<T> SuccessResult(T data) =>
        new() { Status = ResultStatus.Success, Data = data };

    public static ServiceResult<T> NotFound(string? error = null) =>
        new() { Status = ResultStatus.NotFound, Error = error };

    public static ServiceResult<T> Conflict(string error) =>
        new() { Status = ResultStatus.Conflict, Error = error };

    public static ServiceResult<T> Validation(string error) =>
        new() { Status = ResultStatus.ValidationError, Error = error };
}