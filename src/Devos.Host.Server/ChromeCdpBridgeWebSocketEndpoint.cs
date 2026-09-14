using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Devos.Browser.ChromeCdp;

namespace Devos.Host.Server;

public static class ChromeCdpBridgeWebSocketEndpoint
{
    private const int MaxMessageBytes = 256 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task HandleAsync(HttpContext context, BrowserBridgeRuntimeState state)
    {
        if (context.Connection.RemoteIpAddress is not { } remoteAddress || !IPAddress.IsLoopback(remoteAddress))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("DEVOS browser bridge accepts loopback connections only.");
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("WebSocket upgrade required.");
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var session = new ChromeCdpBridgeSession();
        state.MarkSocketAccepted();

        try
        {
            while (socket.State == WebSocketState.Open && !context.RequestAborted.IsCancellationRequested)
            {
                var raw = await ReceiveTextMessageAsync(socket, context.RequestAborted);
                if (raw is null)
                {
                    break;
                }

                ChromeCdpBridgeMessage? message;
                try
                {
                    message = JsonSerializer.Deserialize<ChromeCdpBridgeMessage>(raw, JsonOptions);
                }
                catch (JsonException ex)
                {
                    await SendAsync(socket, CreateError("INVALID_JSON", ex.Message), context.RequestAborted);
                    continue;
                }

                var response = session.Handle(message);

                if (session.IsConnected)
                {
                    state.MarkConnected(
                        session.ExtensionId,
                        session.ExtensionVersion,
                        "1.0",
                        session.GrantedCapabilities);
                }

                if (message?.Kind is ChromeCdpBridgeMessageKinds.Event or ChromeCdpBridgeMessageKinds.Error)
                {
                    state.RecordEvent(message.Kind);
                }

                if (response is not null)
                {
                    await SendAsync(socket, response, context.RequestAborted);
                }
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Normal request shutdown.
        }
        catch (WebSocketException ex)
        {
            state.MarkDisconnected($"Chrome bridge WebSocket error: {ex.WebSocketErrorCode}");
        }
        finally
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "DEVOS bridge closing", CancellationToken.None);
                }
                catch (WebSocketException)
                {
                    // Socket may already be gone; state is still reset below.
                }
            }

            state.MarkDisconnected("Chrome bridge disconnected.");
        }
    }

    private static async Task<string?> ReceiveTextMessageAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var stream = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                await socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Text messages only", cancellationToken);
                return null;
            }

            if (stream.Length + result.Count > MaxMessageBytes)
            {
                await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Bridge message too large", cancellationToken);
                return null;
            }

            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }

    private static Task SendAsync(WebSocket socket, ChromeCdpBridgeMessage message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        return socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
    }

    private static ChromeCdpBridgeMessage CreateError(string code, string detail)
    {
        var payload = JsonSerializer.SerializeToElement(new { code, message = detail }, JsonOptions);
        return new ChromeCdpBridgeMessage(
            ProtocolVersion: "1.0",
            MessageId: $"runtime-{Guid.NewGuid():N}",
            Kind: ChromeCdpBridgeMessageKinds.Error,
            Timestamp: DateTimeOffset.UtcNow,
            Payload: payload);
    }
}
