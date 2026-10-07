using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NoxVault;

// No keyboard/clipboard fallback. The worker receives secrets over redirected stdin,
// never command-line arguments, and can be killed if a foreign UIA provider hangs.
internal sealed record FillTarget(long Window, int Pid, long Started, string Path, string Hash);
internal sealed record FillRequest(string Mode, FillTarget? Target = null, string Username = "", string Password = "");
internal sealed record FillResult(bool Ok, string Message, FillTarget? Target = null, string? Diagnostic = null);

internal static partial class Autofill
{
    internal static async Task<FillResult> RunAsync(FillRequest request, CancellationToken cancellation = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        Process? worker = null;
        try
        {
            timeout.Token.ThrowIfCancellationRequested();
            if (request.Mode == "probe")
            {
                var target = await Task.Run(() => Probe(request.Target), timeout.Token).WaitAsync(timeout.Token);
                if (!await ActivateTarget(target)) return new(false,
                    "Riot konnte nicht aktiviert werden. Login öffnen und erneut ausfüllen.");
                request = request with { Target = target };
            }
            worker = StartWorker();
            return await Exchange(worker, request, timeout.Token);
        }
        catch (OperationCanceledException)
        {
            return new(false, "Ausfüllen abgebrochen oder Zeitlimit erreicht. Bereits ausgefüllte Felder bitte prüfen.");
        }
        catch (Exception) // Any worker or automation failure becomes a message; nothing is retried with secrets.
        {
            return new(false, "Ausfüllen konnte nicht abgeschlossen werden. Bitte Loginfelder prüfen.");
        }
        finally { Stop(worker); }
    }

    static FillTarget Probe(FillTarget? known)
    {
        var candidate = known ?? Discover();
        using var verified = Validate(candidate, known == null, out var hash);
        return candidate with { Hash = hash };
    }

    static Process StartWorker()
    {
        var path = Environment.ProcessPath ?? throw new IOException("Programmpfad unbekannt.");
        var start = new ProcessStartInfo(path)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        start.ArgumentList.Add("--autofill-worker");
        var worker = Process.Start(start) ?? throw new IOException("Ausfüllprozess konnte nicht starten.");
        AllowSetForegroundWindow(worker.Id);
        return worker;
    }

    static async Task<FillResult> Exchange(Process worker, FillRequest request, CancellationToken token)
    {
        using var stop = token.Register(() => Stop(worker, dispose: false));
        await worker.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), token);
        worker.StandardInput.Close();
        var line = await worker.StandardOutput.ReadLineAsync(token);
        await worker.WaitForExitAsync(token);
        return line == null
            ? new(false, "Die Ausfüllprüfung wurde beendet.")
            : JsonSerializer.Deserialize<FillResult>(line) ?? new(false, "Keine gültige Antwort vom Ausfüllprozess.");
    }

    static void Stop(Process? worker, bool dispose = true)
    {
        if (worker == null) return;
        try { if (!worker.HasExited) worker.Kill(); }
        catch (InvalidOperationException) { } // Already exited between the check and the kill.
        if (dispose) worker.Dispose();
    }

    internal static void Worker()
    {
        FillResult result;
        try
        {
            var request = JsonSerializer.Deserialize<FillRequest>(Console.ReadLine() ?? "") ?? throw new InvalidOperationException();
            result = Execute(request);
        }
        catch (Exception) // The worker answers every failure with a generic message instead of crashing silently.
        {
            result = new(false, "Ausfüllen hat nicht geklappt. Öffne den Riot-Login und wähle deinen Account erneut.");
        }
        Console.WriteLine(JsonSerializer.Serialize(result));
    }

    internal static bool ProfileMatches(RiotFillProfile profile, FillTarget target) =>
        string.Equals(profile.Path, target.Path, StringComparison.OrdinalIgnoreCase) && profile.Hash == target.Hash;

    internal static RiotFillProfile? ProfileForVerifiedTarget(RiotFillProfile profile, FillResult verification)
    {
        if (!verification.Ok || verification.Target is not { } target
            || !string.Equals(profile.Path, target.Path, StringComparison.OrdinalIgnoreCase)) return null;
        // Only the successful worker probe authorizes an updated hash. The fill
        // worker pins that new hash again, rejecting changes after the probe.
        return profile with { Hash = target.Hash };
    }
}
