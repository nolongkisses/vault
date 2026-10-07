using System;
using System.IO;

namespace NoxVault;

internal static class StartupTrace
{
    internal static void Write(string phase)
    {
        try
        {
            Directory.CreateDirectory(Preferences.Root);
            var path = Path.Combine(Preferences.Root, "startup-history.log");
            if (File.Exists(path) && new FileInfo(path).Length > 65536) File.Move(path, path + ".previous", true);
            File.AppendAllText(path,
                $"{DateTimeOffset.Now:O} PID={Environment.ProcessId} Version={typeof(App).Assembly.GetName().Version} {phase}\n");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
