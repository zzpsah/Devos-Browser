using Devos.Browser.ChromeCdp;
using Devos.Host.Server;
using Devos.Runtime;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("DEVOS_RUNTIME_URL") ?? "http://127.0.0.1:8787");

var app = builder.Build();
var registry = new InMemoryTaskRegistry();
var browserBridgeState = new BrowserBridgeRuntimeState();
var browserCommandConnection = new ChromeCdpBridgeCommandConnection();
var chromeObservationClient = new ChromeCdpObservationClient(browserCommandConnection);

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
        "browser.bridge.status",
        "browser.observe.normalized"
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

app.MapGet("/browser/tabs/{tabId}/observation", async (string tabId, CancellationToken cancellationToken) =>
{
    if (!browserCommandConnection.IsConnected)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Chrome bridge unavailable",
            detail: "No authenticated DEVOS Chrome extension bridge is currently connected.");
    }

    try
    {
        var observation = await chromeObservationClient.AttachAndObserveAsync(tabId, cancellationToken);
        return Results.Ok(observation);
    }
    catch (ArgumentException ex)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid Chrome tab id",
            detail: ex.Message);
    }
    catch (ChromeCdpBridgeDisconnectedException ex)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Chrome bridge disconnected",
            detail: ex.Message);
    }
    catch (TimeoutException ex)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status504GatewayTimeout,
            title: "Chrome bridge timeout",
            detail: ex.Message);
    }
    catch (ChromeCdpBridgeCommandException ex)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: $"Chrome bridge command failed: {ex.Code}",
            detail: ex.Message);
    }
    catch (ChromeCdpObservationException ex)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "Chrome observation was invalid",
            detail: ex.Message);
    }
});

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
