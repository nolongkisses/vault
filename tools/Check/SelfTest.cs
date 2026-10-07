using System.Diagnostics;

namespace VaultCheck;

// Runs the app's built-in self-test; WPF and DPAPI exist only on Windows, so elsewhere it fails loudly.
static class SelfTest
{
    internal static bool Run(string root)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("Self-test needs Windows (WPF, DPAPI). Run it there, or pass --skip-self-test.");
            return false;
        }
        var exe = Path.Combine(root, "NoxVault", "bin", "Release", "net10.0-windows", "NoxVault.exe");
        var report = Path.Combine(Path.GetTempPath(), "vault-check-" + Guid.NewGuid().ToString("N") + ".txt");
        using var process = Process.Start(new ProcessStartInfo(exe) { ArgumentList = { "--self-test", report } })
            ?? throw new IOException("Cannot start " + exe);
        if (!process.WaitForExit(TimeSpan.FromMinutes(3)))
        {
            process.Kill();
            Console.WriteLine("Self-test exceeded three minutes.");
            return false;
        }
        var lines = File.Exists(report) ? File.ReadAllLines(report) : [];
        var failures = lines.Where(line => !line.StartsWith("PASS: ", StringComparison.Ordinal)).ToArray();
        foreach (var failure in failures) Console.WriteLine(failure);
        Console.WriteLine($"{lines.Length - failures.Length} passed, {failures.Length} other lines, exit {process.ExitCode}");
        return process.ExitCode == 0 && lines.Length > 0 && failures.Length == 0;
    }
}
