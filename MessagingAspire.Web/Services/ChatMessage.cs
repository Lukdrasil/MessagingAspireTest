namespace MessagingAspire.Web.Services;

public class ChatMessage
{
    public string Id { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsSystem => User == "System";
}