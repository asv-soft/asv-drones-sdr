using System.ComponentModel;
using Asv.Cfg.Json;
using NLog;
using Spectre.Console.Cli;

namespace Asv.Drones.Sdr;

/// Represents a command to start a process with given settings.
/// /
internal class StartCommand : Command<StartCommand.Settings>
{
    /// <summary>
    /// Represents a logger instance.
    /// </summary>
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Represents the settings for the application.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Gets or sets the config file path.
        /// </summary>
        /// <value>
        /// The config file path.
        /// </value>
        /// <remarks>
        /// This property represents the file path of the configuration file.
        /// It can be used to specify the path of the configuration file that will be loaded.
        /// The default value is "config.json".
        /// </remarks>
        [Description("Config file path")]
        [CommandArgument(0, "[config_file]")]
        public string ConfigFilePath { get; init; } = "config.json";
        
    }

    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="settings">The command settings.</param>
    /// <returns>Returns the exit code.</returns>
    public override int Execute(CommandContext context, Settings settings)
    {
        using var cfgSvc = new JsonOneFileConfiguration(settings.ConfigFilePath, true, null,true);
        
        
        var waitForProcessShutdownStart = new ManualResetEventSlim();
        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
        {
            // We got a SIGTERM, signal that graceful shutdown has started
            waitForProcessShutdownStart.Set();
        };
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            _logger.Info($"Cancel key pressed=> shutdown server");
            waitForProcessShutdownStart.Set();
        };

        using var gbsService = new SdrService(cfgSvc);

        // Wait for shutdown to start
        waitForProcessShutdownStart.Wait();


        return 0;
    }
}