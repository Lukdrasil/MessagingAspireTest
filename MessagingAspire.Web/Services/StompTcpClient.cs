using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MessagingAspire.Web.Services;

public class StompTcpClient : IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private Task? _readerTask;

    public event Action<ChatMessage>? MessageReceived;
    public event Action<string>? SystemEvent;
    public bool IsConnected { get; private set; }

    public StompTcpClient(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task ConnectAsync(string userDisplayName, string? hostOverride = null, string? login = null, string? passcode = null, string vHost = "/")
    {
        if (IsConnected) return;
        _cts = new CancellationTokenSource();

        // Parse connection string for defaults
        var connStr = _configuration.GetConnectionString("rabbitmq") ?? "amqp://chatuser:chatpass@localhost:5672";
        var uri = new Uri(connStr);
        var host = hostOverride ?? uri.Host;
        login ??= uri.UserInfo.Split(':')[0];
        passcode ??= uri.UserInfo.Split(':').ElementAtOrDefault(1) ?? "chatpass";
        int stompPort = 61613; // exposed in AppHost

        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(host, stompPort);
        _stream = _tcpClient.GetStream();

        // Send CONNECT frame (offer multiple protocol versions for broader compatibility)
        var connectFrame = new StringBuilder()
            .AppendLine("CONNECT")
            .AppendLine("accept-version:1.0,1.1,1.2")
            .AppendLine($"login:{login}")
            .AppendLine($"passcode:{passcode}")
            .AppendLine($"host:{vHost}")
            .AppendLine("heart-beat:10000,10000")
            .AppendLine() // empty line before body
            .Append('\0');

        await SendRawAsync(connectFrame.ToString());
        SystemEvent?.Invoke("CONNECT frame sent");

        // Start reader
        _readerTask = Task.Run(() => ReadLoopAsync(_cts.Token));
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[1];
        var frameBuilder = new StringBuilder();
        try
        {
            while (!ct.IsCancellationRequested && _stream != null)
            {
                int read = await _stream.ReadAsync(buffer, 0, 1, ct);
                if (read == 0) break; // disconnected
                char ch = (char)buffer[0];
                if (ch == '\0')
                {
                    var frame = frameBuilder.ToString();
                    frameBuilder.Clear();
                    HandleFrame(frame);
                }
                else
                {
                    frameBuilder.Append(ch);
                }
            }
        }
        catch (Exception ex)
        {
            SystemEvent?.Invoke($"Read loop error: {ex.Message}");
        }
    }

    private void HandleFrame(string frame)
    {
        if (string.IsNullOrWhiteSpace(frame)) return;
        var lines = frame.Split('\n');
        var command = lines[0].Trim();

        switch (command)
        {
            case "CONNECTED":
                IsConnected = true;
                SystemEvent?.Invoke("STOMP CONNECTED");
                // Auto subscribe
                _ = SubscribeAsync("/exchange/chat.exchange/chat.#", "sub-0");
                break;
            case "MESSAGE":
                ParseMessageFrame(lines);
                break;
            case "ERROR":
                HandleErrorFrame(lines, frame);
                break;
            default:
                SystemEvent?.Invoke($"Frame {command} received");
                break;
        }
    }

    private void ParseMessageFrame(string[] lines)
    {
        int bodyStart = Array.IndexOf(lines, string.Empty) + 1;
        if (bodyStart <= 0 || bodyStart >= lines.Length) return;
        var body = string.Join('\n', lines.Skip(bodyStart));
        try
        {
            body = body.TrimEnd('\0');
            var msg = JsonSerializer.Deserialize<ChatMessage>(body);
            if (msg != null)
            {
                MessageReceived?.Invoke(msg);
            }
        }
        catch (Exception ex)
        {
            SystemEvent?.Invoke("Failed to parse message: " + ex.Message);
        }
    }

    private void HandleErrorFrame(string[] lines, string rawFrame)
    {
        // Collect header lines until blank line
        var headerLines = new List<string>();
        int i = 1;
        while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
        {
            headerLines.Add(lines[i].Trim());
            i++;
        }
        string? messageHeader = headerLines.FirstOrDefault(h => h.StartsWith("message:"));
        string? receiptHeader = headerLines.FirstOrDefault(h => h.StartsWith("receipt-id:"));
        int bodyStart = i + 1;
        string body = bodyStart < lines.Length ? string.Join('\n', lines.Skip(bodyStart)).TrimEnd('\0').Trim() : string.Empty;
        string message = messageHeader != null ? messageHeader.Substring("message:".Length).Trim() : "STOMP ERROR";
        string receipt = receiptHeader != null ? receiptHeader.Substring("receipt-id:".Length).Trim() : string.Empty;
        var composed = new StringBuilder("STOMP ERROR: ").Append(message);
        if (!string.IsNullOrWhiteSpace(receipt)) composed.Append(" (receipt " + receipt + ")");
        if (!string.IsNullOrWhiteSpace(body)) composed.Append(" | " + body);
        SystemEvent?.Invoke(composed.ToString());
        IsConnected = false;
    }

    public async Task SubscribeAsync(string destination, string id)
    {
        var frame = new StringBuilder()
            .AppendLine("SUBSCRIBE")
            .AppendLine($"id:{id}")
            .AppendLine($"destination:{destination}")
            .AppendLine("ack:auto")
            .AppendLine()
            .Append('\0')
            .ToString();
        await SendRawAsync(frame);
        SystemEvent?.Invoke($"Subscribed {destination}");
    }

    public async Task SendChatMessageAsync(string user, string text)
    {
        if (!IsConnected) return;
        var chat = new ChatMessage
        {
            Id = Guid.NewGuid().ToString(),
            User = user,
            Text = text,
            Timestamp = DateTime.UtcNow
        };
        var body = JsonSerializer.Serialize(chat);
        var frame = new StringBuilder()
            .AppendLine("SEND")
            .AppendLine("destination:/exchange/chat.exchange/chat.message")
            .AppendLine("content-type:application/json")
            .AppendLine($"content-length:{Encoding.UTF8.GetByteCount(body)}")
            .AppendLine()
            .Append(body)
            .Append('\0')
            .ToString();
        await SendRawAsync(frame);
    }

    private async Task SendRawAsync(string frame)
    {
        if (_stream == null) return;
        var bytes = Encoding.UTF8.GetBytes(frame);
        await _stream.WriteAsync(bytes, 0, bytes.Length);
        await _stream.FlushAsync();
    }

    public async Task DisconnectAsync()
    {
        if (!IsConnected || _stream == null) return;
        var frame = new StringBuilder()
            .AppendLine("DISCONNECT")
            .AppendLine()
            .Append('\0')
            .ToString();
        await SendRawAsync(frame);
        await DisposeAsync();
        SystemEvent?.Invoke("Disconnected");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _cts?.Cancel();
            if (_readerTask != null) await Task.WhenAny(_readerTask, Task.Delay(500));
            _stream?.Dispose();
            _tcpClient?.Close();
        }
        catch { }
        finally
        {
            _cts?.Dispose();
            IsConnected = false;
        }
    }
}

public class ChatMessage
{
    public string Id { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsSystem => User == "System";
}
