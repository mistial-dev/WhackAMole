using JetBrains.Annotations;

namespace WhackAMole;

/// <summary>
/// Settings for message spam detection
/// </summary>
public class MessageSettings
{
    [UsedImplicitly]
    public required int DuplicationThreshold { get; init; }
    public required int TimeSpanMinutes { get; init; }
}