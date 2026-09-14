namespace Devos.Checkpoints;

public interface ICheckpointStore
{
    Task SaveAsync(TaskCheckpoint checkpoint, CancellationToken cancellationToken = default);
    Task<TaskCheckpoint?> LoadAsync(string taskId, CancellationToken cancellationToken = default);
}

public sealed class InMemoryCheckpointStore : ICheckpointStore
{
    private readonly Dictionary<string, TaskCheckpoint> _checkpoints = new(StringComparer.OrdinalIgnoreCase);

    public Task SaveAsync(TaskCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        _checkpoints[checkpoint.TaskId] = checkpoint;
        return Task.CompletedTask;
    }

    public Task<TaskCheckpoint?> LoadAsync(string taskId, CancellationToken cancellationToken = default)
    {
        _checkpoints.TryGetValue(taskId, out var checkpoint);
        return Task.FromResult(checkpoint);
    }
}
