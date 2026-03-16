using System.Diagnostics;

namespace MonitorSwitcher;

internal static class DisplaySwitcher
{
    public static bool Switch(DisplayTarget target)
    {
        var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var executablePath = Path.Combine(systemDirectory, "DisplaySwitch.exe");
        var argument = target == DisplayTarget.Internal ? "/internal" : "/external";

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = argument,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        return process is not null;
    }
}
