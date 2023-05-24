using Microsoft.Extensions.Logging;
using TaskRunner.Lib;

namespace TaskRunner.Example;

public record HelloWorldParameters(string name);

public class HelloWorld : ITaskExecutor
{
    private readonly ILogger<HelloWorld> _logger;

    public HelloWorld(ILogger<HelloWorld> logger)
    {
        _logger = logger;
    }

    public Task<object?> ExecuteAsync(object? parameters, CancellationToken cancellationToken)
    {
        var values = parameters as HelloWorldParameters;

        _logger.LogInformation($"Hello World {values?.name}");

        return Task.FromResult<object?>(null);
    }
}
