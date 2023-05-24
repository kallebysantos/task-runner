namespace TaskRunner.Lib;

public interface ITaskExecutor
{
    Task<object?> ExecuteAsync(object? parameters, CancellationToken cancellationToken);
}
