using System.Reflection;
using System.Text;
using NLog;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Asv.Drones.Sdr;

/// <summary>
/// The Program class represents the entry point for the application.
/// </summary>
public class Program
{
    /// <summary>
    /// This variable is an instance of the Logger class. It is used for logging purposes.
    /// </summary>
    /// <remarks>
    /// The Logger class is part of the NLog library and is responsible for logging messages.
    /// This variable is a private field that is declared as static and readonly, ensuring that it can only be assigned once and is accessible within the class it is declared in.
    /// The LogManager.GetCurrentClassLogger() method is used to retrieve the current class logger instance, which can then be used to log messages.
    /// </remarks>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// The entry point for the application. </summary> <param name="args">The command line arguments.</param> <returns>The exit code of the application.</returns>
    /// /
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

    /// Handles any unhandled exceptions that occur in the application.
    /// /
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