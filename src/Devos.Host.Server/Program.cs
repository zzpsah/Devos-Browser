using Devos.Runtime;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var registry = new InMemoryTaskRegistry();

app.MapGet("/status", () => Results.Ok(new RuntimeStatus(
    State: "READY",
    AiProvider: "deterministic",
    BrowserProvider: "none")));

app.MapGet("/capabilities", () => Results.Ok(new
{
    protocolVersion = "1.0",
    capabilities = new[]
    {
        "runtime.tasks.create",
        "runtime.tasks.read",
        "runtime.tasks.pause",
        "runtime.tasks.resume",
        "runtime.tasks.cancel"
    }
}));

app.MapPost("/tasks", (TaskCreateRequest request) =>
{
    var task = registry.Create(request.Goal);
    return Results.Created($"/tasks/{task.TaskId}", task);
});

app.MapGet("/tasks", () => Results.Ok(registry.List()));

app.MapGet("/tasks/{taskId}", (string taskId) =>
{
    var task = registry.Get(taskId);
    return task is null ? Results.NotFound() : Results.Ok(task);
});

app.MapPost("/tasks/{taskId}/pause", (string taskId) =>
{
    var task = registry.UpdateState(taskId, RuntimeTaskState.Paused, "paused-by-user");
    return Results.Ok(task);
});

app.MapPost("/tasks/{taskId}/resume", (string taskId) =>
{
    var task = registry.UpdateState(taskId, RuntimeTaskState.Running, "resumed-by-user");
    return Results.Ok(task);
});

app.MapPost("/tasks/{taskId}/cancel", (string taskId) =>
{
    var task = registry.UpdateState(taskId, RuntimeTaskState.Cancelled, "cancelled-by-user");
    return Results.Ok(task);
});

app.MapGet("/browser/status", () => Results.Ok(new
{
    connected = false,
    provider = "none",
    message = "Browser bridge not implemented yet."
}));

app.MapGet("/ai/status", () => Results.Ok(new
{
    provider = "deterministic",
    available = true,
    message = "Deterministic fallback is available."
}));

app.Run();
