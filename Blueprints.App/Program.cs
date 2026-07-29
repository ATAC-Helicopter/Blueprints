using System;
using Avalonia;

namespace Blueprints.App;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception) when (IsLinuxDisplayStartupFailure(exception))
        {
            Console.Error.WriteLine("Blueprints could not connect to a Linux desktop display.");
            Console.Error.WriteLine("Avalonia currently needs an X11/XWayland display for this app.");
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")))
            {
                Console.Error.WriteLine("You appear to be running from a Wayland session without DISPLAY set.");
                Console.Error.WriteLine("Start from a terminal that exports DISPLAY, install/enable XWayland, or run through an XWayland helper.");
            }
            else
            {
                Console.Error.WriteLine("DISPLAY is set, but Avalonia still could not connect to X11/XWayland.");
                Console.Error.WriteLine("This usually means Xauthority/access is missing in this shell or container.");
            }

            Console.Error.WriteLine($"WAYLAND_DISPLAY={Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") ?? "(unset)"}");
            Console.Error.WriteLine($"DISPLAY={Environment.GetEnvironmentVariable("DISPLAY") ?? "(unset)"}");
            Console.Error.WriteLine($"XAUTHORITY={Environment.GetEnvironmentVariable("XAUTHORITY") ?? "(unset)"}");
            Environment.ExitCode = 78;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static bool IsLinuxDisplayStartupFailure(Exception exception) =>
        OperatingSystem.IsLinux()
        && exception.Message.Contains("XOpenDisplay failed", StringComparison.OrdinalIgnoreCase);
}
