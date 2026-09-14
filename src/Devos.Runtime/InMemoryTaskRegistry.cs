namespace Devos.Runtime;

public interface ITaskRegistry
{
    RuntimeTask Create(string goal);
    RuntimeTask? Get(string taskId);
    IReadOnlyList<RuntimeTask> List();
    RuntimeTask UpdateState(string taskId, RuntimeTaskState state, string? reason = null);
}

public sealed class InMemoryTaskRegistry : ITaskRegistry
{
    private readonly Dictionary<string, RuntimeTask> _tasks = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeTask Create(string goal)
    {
        if (string.IsNullOrWhiteSpace(goal))
        {
            throw new ArgumentException("Goal cannot be empty.", nameof(goal));
        }

        var now = DateTimeOffset.UtcNow;
        var task = new RuntimeTask(
            TaskId: $"task-{Guid.NewGuid():N}",
            Goal: goal.Trim(),
            State: RuntimeTaskState.Created,
            CreatedAt: now,
            UpdatedAt: now);

        _tasks[task.TaskId] = task;
        return task;
    }

    public RuntimeTask? Get(string taskId)
    {
        _tasks.TryGetValue(taskId, out var task);
        return task;
    }

    public IReadOnlyList<RuntimeTask> List() => _tasks.Values.OrderBy(task => task.CreatedAt).ToArray();

    public RuntimeTask UpdateState(string taskId, RuntimeTaskState state, string? reason = null)
    {
        if (!_tasks.TryGetValue(taskId, out var existing))
        {
            throw new InvalidOperationException($"Unknown task '{taskId}'.");
        }

        var updated = existing with
        {
            State = state,
            Reason = reason,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _tasks[taskId] = updated;
        return updated;
    }
}
