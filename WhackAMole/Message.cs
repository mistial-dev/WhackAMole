using Discord.WebSocket;

namespace WhackAMole;

/// <summary>
/// Stores the content and timestamp of a message.
/// </summary>
internal class Message
{
    /// <summary>
    /// Represents the content of the message.
    /// </summary>
    public required string Content { get; init; }
        
    /// <summary>
    /// Represents the time the message was sent.
    /// </summary>
    public required DateTime Timestamp { get; init; }
    
    /// <summary>
    /// Represents the message that this message is a reply to.
    /// </summary>
    public required SocketMessage MessageReference { get; init; }
}