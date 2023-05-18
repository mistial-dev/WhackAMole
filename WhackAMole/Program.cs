using System.Reflection;
using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Discord.WebSocket;
using log4net;
using log4net.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Yaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WhackAMole;

/// <summary>
/// Discord bot for dealing with spam links and messages
/// </summary>
public partial class Program
{
    /// <summary>
    /// Stores the content and timestamp of user messages.
    /// </summary>
    private Dictionary<ulong, List<Message>> _lastMessages = new();

    /// <summary>
    /// Application settings
    /// </summary>
    public class AppSettings
    {
        public required BotSettings Bot { get; set; }
        public required MessageSettings Message { get; set; }
        public required LogSettings Log { get; set; }
    }

    /// <summary>
    /// Program entry point
    /// </summary>
    /// <param name="args"></param>
    /// <exception cref="ArgumentNullException"></exception>
    private static async Task Main(string[] args)
    {
        var builder = new HostBuilder()
            .ConfigureAppConfiguration(x =>
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddYamlFile("config.yaml", optional: false)
                    .Build();

                x.AddConfiguration(configuration);
            })
            .ConfigureLogging(logging =>
            {
                var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
                XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));
            })
            .ConfigureDiscordHost((context, config) =>
            {
                var botSettings = context.Configuration
                    .GetSection("Bot")
                    .Get<BotSettings>();
                
                config.SocketConfig = new DiscordSocketConfig
                {
                    LogLevel = LogSeverity.Verbose,
                    MessageCacheSize = 200,
                };

                if (botSettings != null) config.Token = botSettings.Token;
            })
            .UseCommandService((_, config) =>
            {
                if (config == null) throw new ArgumentNullException(nameof(config));
                
                config = new CommandServiceConfig
                {
                    LogLevel = LogSeverity.Verbose,
                    CaseSensitiveCommands = false,
                };
            })
            .ConfigureServices(ConfigureServices)
            .UseConsoleLifetime();

        var host = builder.Build();
        using (host)
        {
            await host.RunAsync();
        }
    }

    /// <summary>
    /// Configure services for the bot
    /// </summary>
    /// <param name="context"></param>
    /// <param name="services"></param>
    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        var appSettings = context.Configuration.Get<AppSettings>();
        services.AddSingleton(appSettings);
        services.AddHostedService<BotService>();
    }
}