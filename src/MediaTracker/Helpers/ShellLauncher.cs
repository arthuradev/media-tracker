using System.Diagnostics;

namespace MediaTracker.Helpers;

public static class ShellLauncher
{
    public static bool TryOpen(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return false;

        try
        {
            Process.Start(new ProcessStartInfo(location)
            {
                UseShellExecute = true
            });

            return true;
        }
        catch
        {
            return false;
        }
    }
}
