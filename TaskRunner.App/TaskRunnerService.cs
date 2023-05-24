using System.Dynamic;
using System.Text.Json;
using System.Reflection;
using Microsoft.Extensions.Options;
using TaskRunner.Lib;

namespace TaskRunner.App;

public record TaskMetadata(
    string Id,
    string Assembly,
    string ExecutorName,
    int Interval,
    ExpandoObject? Parameters
);

public record TaskItem(ITaskExecutor Executor, TaskMetadata Metadata);

public class TaskRunnerService : BackgroundService
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ILogger<TaskRunnerService> _logger;
    private readonly SettingsProvider _settings;

    private HashSet<TaskItem> TaskExecutors = new HashSet<TaskItem>();

    public TaskRunnerService(
        ServiceProvider serviceProvider,
        IOptions<SettingsProvider> settings,
        ILogger<TaskRunnerService> logger
    )
    {
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
        _logger = logger;
    }

    private TaskItem CreateTaskItem(Type instanceType, TaskMetadata metadata) =>
        new TaskItem(
            Executor: (ITaskExecutor)
                ActivatorUtilities.CreateInstance(
                    provider: _serviceProvider.CreateAsyncScope().ServiceProvider,
                    instanceType
                ),
            Metadata: metadata
        );

    private async Task LoadTasks(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"Loading tasks from File: {_settings.TasksFile}.");

        using var fileStream = File.OpenRead(_settings.TasksFile);
        var taskMetadataList = JsonSerializer.DeserializeAsyncEnumerable<TaskMetadata>(fileStream);

        await foreach (var taskMetadata in taskMetadataList)
        {
            if (taskMetadata is null)
            {
                continue;
            }

            var assemblyPath = Path.ChangeExtension(
                path: Path.Combine(AppContext.BaseDirectory, taskMetadata.Assembly),
                extension: "dll"
            );

            TaskExecutors = TaskExecutors
                .Concat(
                    Assembly
                        .LoadFrom(assemblyPath)
                        .GetTypes()
                        .Where(type => type.GetInterfaces().Contains(typeof(ITaskExecutor)))
                        .Select(instanceType => CreateTaskItem(instanceType, taskMetadata))
                )
                .ToHashSet();
        }
    }

    private async Task Run(TaskItem taskExecutor, CancellationToken cancellationToken)
    {
        var (executor, metadata) = taskExecutor;

        _logger.LogInformation($"Executor [{metadata.Id}] - Process started.");
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation($"Executor [{metadata.Id}] - Task started.");

                object? parameters = metadata.Parameters;
                do
                {
                    parameters = await executor.ExecuteAsync(parameters, cancellationToken);

                    _logger.LogInformation(
                        $"Executor [{metadata.Id}] - Task Executed {parameters}."
                    );
                } while (parameters is not null);

                _logger.LogInformation(
                    $"Executor [{metadata.Id}] - Task finished, waiting for task interval."
                );
            }
            catch (Exception ex)
            {
                _logger.LogCritical(
                    ex,
                    $"Executor [{metadata.Id}] - Process failed during execution, waiting for task interval."
                );
            }

            await Task.Delay(TimeSpan.FromSeconds(metadata.Interval), cancellationToken);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await LoadTasks(cancellationToken);

        var executionTasks = new List<Task>();

        foreach (var taskExecutor in TaskExecutors)
        {
            executionTasks.Add(Run(taskExecutor, cancellationToken));
        }

        await Task.WhenAll(executionTasks);
    }
}
