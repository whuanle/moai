namespace MoAI.App.AIAssistant.Constants;

/// <summary>
/// Constants for chat history caching.
/// </summary>
public static class ChatCacheConstants
{
    /// <summary>
    /// Maximum number of messages to cache before triggering compression.
    /// When history reaches this count, compression will be triggered.
    /// </summary>
    public const int MaxCacheMessages = 20;

    /// <summary>
    /// Number of recent messages to keep after compression.
    /// After compression: 1 summary + CompressToMessages recent messages = CompressToMessages + 1 total.
    /// Example: 1 summary + 4 recent = 5 total messages.
    /// </summary>
    public const int CompressToMessages = 4;
}
