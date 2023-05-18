using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;
using log4net;
using Microsoft.Extensions.Hosting;

namespace WhackAMole;

/// <summary>
/// Discord bot for dealing with spam links and messages.
/// </summary>
public class BotService : IHostedService
{
    /// <summary>
    /// Log4net logger
    /// </summary>
    private readonly ILog _log = LogManager.GetLogger(typeof(BotService));
    
    /// <summary>
    /// Discord client
    /// </summary>
    private readonly DiscordSocketClient _client;
    
    /// <summary>
    /// Application settings
    /// </summary>
    private readonly Program.AppSettings _settings;

    /// <summary>
    /// Dictionary of users and their last messages
    /// </summary>
    private readonly Dictionary<ulong, List<Message>> _lastMessages = new();
    
    /// <summary>
    /// Dictionary of recent URLs
    /// </summary>
    private readonly List<Message> _recentUrls = new();
    
    /// <summary>
    /// Timer for garbage collecting inactive users
    /// </summary>
    private readonly System.Timers.Timer _cleanupTimer;

    /// <summary>
    /// Discord bot for dealing with spam links and messages
    /// </summary>
    /// <param name="client"></param>
    /// <param name="settings"></param>
    public BotService(DiscordSocketClient client, Program.AppSettings settings)
    {
        _client = client;
        _settings = settings;
        
        _cleanupTimer = new System.Timers.Timer(60 * 1000); // Run every minute
        _cleanupTimer.Elapsed += (_, _) => GarbageCollectInactiveUsers();
        _cleanupTimer.Start();
    }

    /// <summary>
    /// Handle the bot service starting
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _client.Log += Log;
        _client.MessageReceived += MessageReceived;

        await _client.LoginAsync(TokenType.Bot, _settings.Bot.Token);
        await _client.StartAsync();
    }

    /// <summary>
    /// Handle the bot service stopping
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Log a message
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    private Task Log(LogMessage message)
    {
        _log.Info(message.ToString());
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when a message is received
    /// </summary>
    /// <param name="message"></param>
    private async Task MessageReceived(SocketMessage message)
    {
        if (message is not SocketUserMessage msg) return;
        if (msg.Author.IsBot) return;

        GarbageCollectOldMessages(_recentUrls);

        if (!_lastMessages.ContainsKey(msg.Author.Id))
            _lastMessages[msg.Author.Id] = new List<Message>();

        GarbageCollectOldMessages(_lastMessages[msg.Author.Id]);

        await ProcessMessage(msg);
    }

    /// <summary>
    /// Garbage collect old messages
    /// </summary>
    /// <param name="messages"></param>
    private void GarbageCollectOldMessages(List<Message> messages)
    {
        messages.RemoveAll(m => (DateTime.UtcNow - m.Timestamp).TotalMinutes > _settings.Message.TimeSpanMinutes);
    }

    /// <summary>
    /// Process a message
    /// </summary>
    /// <param name="msg"></param>
    private async Task ProcessMessage(SocketMessage msg)
    {
        // Extract URLs from the message
        var urls = Regex.Matches(msg.Content, @"(http|https):\/\/[^ ]*").Select(m => m.Value).ToList();


        // Check URLs against recent URLs
        if (urls.Any(url => _recentUrls.Count(m => m.Content == url) >= _settings.Message.DuplicationThreshold))
        {
            await DeleteAndWarn(msg);
            return;
        }
        _recentUrls.AddRange(urls.Select(url => new Message { Content = url, Timestamp = DateTime.UtcNow, MessageReference = msg}));

        // Count duplicates for non-URL messages
        var duplicateCount = _lastMessages[msg.Author.Id].Count(m => m.Content == msg.Content);

        // Add current message to the history
        _lastMessages[msg.Author.Id].Add(new Message { Content = msg.Content, Timestamp = DateTime.UtcNow, MessageReference = msg});

        if (duplicateCount >= _settings.Message.DuplicationThreshold)
        {
            await DeleteAndWarn(msg);
        }
    }

    /// <summary>
    /// Delete a message and warn the user
    /// </summary>
    /// <param name="msg"></param>
    private async Task DeleteAndWarn(SocketMessage msg)
    {
        // Delete all copies of the spam message
        var duplicateMessages = _lastMessages[msg.Author.Id].Where(m => m.Content == msg.Content).ToList();
        foreach (var duplicateMessage in duplicateMessages)
        {
            await duplicateMessage.MessageReference.DeleteAsync();
            _lastMessages[msg.Author.Id].Remove(duplicateMessage);
        }
        
        await msg.DeleteAsync();
        var channel = msg.Channel as IMessageChannel;
        await channel.SendMessageAsync($"{msg.Author.Mention}, please do not spam in this channel.");
        if (msg.Author is not IGuildUser user) return;
        await user.SetTimeOutAsync(TimeSpan.FromMinutes(_settings.Bot.TimeOut));
        await user.RemoveRoleAsync(user.Guild.Roles.First(r => r.Name == "Muted"));
    }
    
    /// <summary>
    /// Periodically garbage collect inactive users.
    /// </summary>
    private void GarbageCollectInactiveUsers()
    {
        var keysToRemove = _lastMessages.Where(kvp =>
                kvp.Value.Count == 0 ||
                (DateTime.UtcNow - kvp.Value.Max(m => m.Timestamp)).TotalMinutes > _settings.Message.TimeSpanMinutes)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _lastMessages.Remove(key);
        }
    }
}