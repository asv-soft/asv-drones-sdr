using System.Reflection;
using System.Text;
using NLog;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Asv.Drones.Sdr;

public class Program
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    static int Main(string[] args)
    {
        HandleExceptions();
        Assembly.GetExecutingAssembly().PrintWelcomeToConsole();
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;
        var app = new CommandApp<StartCommand>();
        app.Configure(config =>
        {
            config.PropagateExceptions();
#if DEBUG
            config.ValidateExamples();
#endif
        });
        try
        {
            return app.Run(args);
        }
        catch (Exception ex)
        {
            Logger.Fatal(ex);
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            return -99;
        }
    }
    
    private static void HandleExceptions()
    {

        TaskScheduler.UnobservedTaskException +=
            (sender, args) =>
            {
                Logger.Fatal(args.Exception, $"Task scheduler unobserver task exception from '{sender}': {args.Exception.Message}");
            };

        AppDomain.CurrentDomain.UnhandledException +=
            (sender, eventArgs) =>
            {
                Logger.Fatal($"Unhandled AppDomain exception. Sender '{sender}'. Args: {eventArgs.ExceptionObject}");
            };
    }
}