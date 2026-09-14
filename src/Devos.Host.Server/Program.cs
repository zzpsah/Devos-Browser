using Devos.Browser.ChromeCdp;
using Devos.Host.Server;
using Devos.Runtime;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("DEVOS_RUNTIME_URL") ?? "http://127.0.0.1:8787");

var app = builder.Build();
var registry = new InMemoryTaskRegistry();
var browserBridgeState = new BrowserBridgeRuntimeState();
var browserCommandConnection = new ChromeCdpBridgeCommandConnection();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

app.MapGet("/status", () =>
{
    var browser = browserBridgeState.GetSnapshot();
    return Results.Ok(new RuntimeStatus(
        State: "READY",
        AiProvider: "deterministic",
        BrowserProvider: browser.Connected ? "ChromeCDP" : "none"));
});

app.MapGet("/capabilities", () => Results.Ok(new
{
    protocolVersion = "1.0",
    capabilities = new[]
    {
        "runtime.tasks.create",
        "runtime.tasks.read",
        "runtime.tasks.pause",
        "runtime.tasks.resume",
        "runtime.tasks.cancel",
        "browser.bridge.connect",
        "browser.bridge.status"
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

app.MapGet("/browser/status", () => Results.Ok(browserBridgeState.GetSnapshot()));

app.Map("/bridge", context => ChromeCdpBridgeWebSocketEndpoint.HandleAsync(
    context,
    browserBridgeState,
    browserCommandConnection));

app.MapGet("/ai/status", () => Results.Ok(new
{
    provider = "deterministic",
    available = true,
    message = "Deterministic fallback is available."
}));

app.Run();
