namespace Devos.Runtime;

public enum RuntimeTaskState
{
    Created,
    Running,
    AwaitingApproval,
    Paused,
    Completed,
    Failed,
    Cancelled
}

public sealed record RuntimeStatus(
    string State,
    string AiProvider,
    string BrowserProvider,
    string ProtocolVersion = "1.0");

public sealed record TaskCreateRequest(string Goal);

public sealed record RuntimeTask(
    string TaskId,
    string Goal,
    RuntimeTaskState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? Reason = null);
