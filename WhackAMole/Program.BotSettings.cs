namespace WhackAMole;

/// <summary>
///  Bot Settings
/// </summary>
public class BotSettings
{
    /// <summary>
    /// Discord Bot Token
    /// </summary>
    public required string Token { get; set; }
        
    /// <summary>
    /// How long are spammers timed out for?
    /// </summary>
    public required int TimeOut { get; set; }
}