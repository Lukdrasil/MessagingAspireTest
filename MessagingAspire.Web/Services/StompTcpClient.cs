using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace MessagingAspire.Web.Services;

public class StompChatClient : IAsyncDisposable
{
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private string _currentUser = "Anonymous";
    private string _stompLogin = "guest";
    private string _stompPasscode = "guest";
    private string _stompVHost = "/";

    public event Action<ChatMessage>? MessageReceived;
    public event Action<string>? SystemEvent;
    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public async Task ConnectAsync(string url, string userName, string login = "guest", string passcode = "guest", string vhost = "/")
    {
        if (IsConnected) return;

        _currentUser = userName ?? "Anonymous";
        _stompLogin = login;
        _stompPasscode = passcode;
        _stompVHost = vhost;

        try
        {
            _ws = new ClientWebSocket();
            _cts = new CancellationTokenSource();

            await _ws.ConnectAsync(new Uri(url), _cts.Token);
            SystemEvent?.Invoke("WebSocket connected");

            // Send STOMP CONNECT frame
            await SendStompConnectAsync();

            // Start receiving messages
            _receiveTask = ReceiveLoopAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            SystemEvent?.Invoke($"Connection error: {ex.Message}");
            await CleanupAsync();
            throw;
        }
    }

    private async Task SendStompConnectAsync()
    {
        var connectFrame = $"CONNECT\n" +
                          $"accept-version:1.2\n" +
                          $"login:{_stompLogin}\n" +
                          $"passcode:{_stompPasscode}\n" +
                          $"host:{_stompVHost}\n" +
                          $"heart-beat:10000,10000\n" +
                          $"\n" +
                          $"\0";

        await SendFrameAsync(connectFrame);
        SystemEvent?.Invoke("Sent CONNECT frame with auth");
    }

    private async Task SendFrameAsync(string frame)
    {
        if (_ws == null || !IsConnected) return;

        var bytes = Encoding.UTF8.GetBytes(frame);
        await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts?.Token ?? CancellationToken.None);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var messageBuilder = new StringBuilder();

        try
        {
            while (_ws != null && _ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    SystemEvent?.Invoke("WebSocket closed by server");
                    await CleanupAsync();
                    break;
                }

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                messageBuilder.Append(message);

                if (result.EndOfMessage)
                {
                    var frame = messageBuilder.ToString();
                    messageBuilder.Clear();
                    HandleStompFrame(frame);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            SystemEvent?.Invoke($"Receive error: {ex.Message}");
        }
    }

    private void HandleStompFrame(string data)
    {
        var lines = data.Split('\n');
        if (lines.Length == 0) return;

        var command = lines[0];
        SystemEvent?.Invoke($"STOMP command: {command}");

        switch (command)
        {
            case "CONNECTED":
                SystemEvent?.Invoke("STOMP CONNECTED");
                _ = SubscribeToQueueAsync();
                break;

            case "MESSAGE":
                HandleMessageFrame(data);
                break;

            case "ERROR":
                var errorMessage = lines.Length > 1 ? lines[^2] : "Unknown error";
                SystemEvent?.Invoke($"STOMP ERROR: {errorMessage}");
                break;
        }
    }

    private async Task SubscribeToQueueAsync()
    {
        if (!IsConnected) return;

        var subscribeFrame = "SUBSCRIBE\n" +
                           "id:sub-0\n" +
                           "destination:/exchange/chat.exchange/chat.#\n" +
                           "ack:auto\n" +
                           "\n" +
                           "\0";

        await SendFrameAsync(subscribeFrame);
        SystemEvent?.Invoke("Subscribed to chat.exchange");
    }

    private void HandleMessageFrame(string frame)
    {
        // Split by null terminator first to separate frame content from terminator
        var frameContent = frame.Split('\0')[0];
        var lines = frameContent.Split('\n');
        var bodyStartIndex = -1;

        // Find where headers end (empty line)
        for (int i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i]))
            {
                bodyStartIndex = i + 1;
                break;
            }
        }

        if (bodyStartIndex < 0 || bodyStartIndex >= lines.Length) return;

        // Get message body (everything after headers)
        var body = string.Join("\n", lines.Skip(bodyStartIndex)).Trim();

        if (string.IsNullOrWhiteSpace(body)) return;

        try
        {
            var message = JsonSerializer.Deserialize<ChatMessage>(body);
            if (message != null)
            {
                MessageReceived?.Invoke(message);
            }
        }
        catch (Exception ex)
        {
            SystemEvent?.Invoke($"Failed to parse message: {ex.Message}");
        }
    }

    public async Task SendChatMessageAsync(string user, string text)
    {
        if (!IsConnected) return;

        var message = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            User = user,
            Text = text,
            Timestamp = DateTime.UtcNow
        };

        var body = JsonSerializer.Serialize(message);
        var bodyBytes = Encoding.UTF8.GetByteCount(body);

        var sendFrame = $"SEND\n" +
                       $"destination:/exchange/chat.exchange/chat.message\n" +
                       $"content-type:application/json\n" +
                       $"content-length:{bodyBytes}\n" +
                       $"\n" +
                       $"{body}\0";

        await SendFrameAsync(sendFrame);
        SystemEvent?.Invoke($"Sent message: {text}");
    }

    public async Task DisconnectAsync()
    {
        if (!IsConnected) return;

        try
        {
            // Send STOMP DISCONNECT frame
            var disconnectFrame = "DISCONNECT\n\n\0";
            await SendFrameAsync(disconnectFrame);
            await Task.Delay(100); // Give time for frame to send
        }
        catch { }
        finally
        {
            await CleanupAsync();
        }
    }

    private async Task CleanupAsync()
    {
        _cts?.Cancel();

        if (_receiveTask != null)
        {
            try { await _receiveTask; } catch { }
        }

        if (_ws != null)
        {
            if (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.CloseReceived)
            {
                try
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
                }
                catch { }
            }
            _ws.Dispose();
            _ws = null;
        }

        _cts?.Dispose();
        _cts = null;
        _receiveTask = null;

        SystemEvent?.Invoke("Disconnected");
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
