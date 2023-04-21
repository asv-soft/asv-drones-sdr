using System.ComponentModel;
using Asv.Cfg.Json;
using NLog;
using Spectre.Console.Cli;

namespace Asv.Drones.Sdr;

internal class StartCommand : Command<StartCommand.Settings>
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    public sealed class Settings : CommandSettings
    {
        [Description("Config file path")]
        [CommandArgument(0, "[config_file]")]
        public string ConfigFilePath { get; init; } = "config.json";
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        using var cfgSvc = new JsonOneFileConfiguration(settings.ConfigFilePath, true, null);
        
        
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