using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace NoxVault;

internal static class AutofillTests
{
    internal static void Run(string folder, Action<bool, string> check, Action<Action, string> reject)
    {
        var profile = new RiotFillProfile(@"C:\Synthetic\Riot Client.exe", new string('A', 64), false);
        int reads = 0, validations = 0, waits = 0;
        check(Autofill.ReadReadyFields(() => ++reads < 3 ? throw new Autofill.FieldsNotReadyException("not ready") : 42,
            () => validations++, () => waits++) == 42 && reads == 3 && validations == 3 && waits == 2,
            "Transient Riot field initialization retries automatically and revalidates each attempt");
        reads = 0; waits = 0;
        reject(() => Autofill.ReadReadyFields<int>(() => { reads++; throw new Autofill.FieldsNotReadyException("not ready"); }, () => { }, () => waits++),
            "Unready Riot fields stop after a bounded wait");
        check(reads == 5 && waits == 4, "Field readiness retry is limited to four short waits");
        waits = 0;
        reject(() => Autofill.ReadReadyFields<int>(() => throw new InvalidOperationException("blocking dialog or wrong process"), () => { }, () => waits++),
            "Dialog and target mismatch fail immediately without retrying");
        check(waits == 0, "Safety failures never wait or bypass target validation");
        reads = 0;
        reject(() => Autofill.ReadReadyFields(() => ++reads, () => throw new InvalidOperationException("target changed"), () => { }),
            "Target mismatch aborts before reading fields on every retry");
        check(reads == 0, "Readiness retry cannot inspect a changed target");
        var target = new FillTarget(1, 1, 1, profile.Path, profile.Hash);
        check(MainWindow.ProfileMatches(profile, target), "Autofill accepts explicitly bound executable identity");
        check(!MainWindow.ProfileMatches(profile, target with { Hash = new string('B', 64) }), "Changed executable hash differs from saved binding");
        var updatedTarget = target with { Hash = new string('B', 64) };
        var renewed = MainWindow.ProfileForVerifiedTarget(profile with { UseEmail = true }, new(true, "verified", updatedTarget));
        check(renewed != null && renewed.Hash == updatedTarget.Hash && renewed.UseEmail && renewed.Path == profile.Path,
            "Verified Riot update renews hash while preserving login source and install path");
        check(MainWindow.ProfileForVerifiedTarget(profile, new(false, "signature or fields rejected", updatedTarget)) == null,
            "Failed verification cannot renew an outdated binding even with a target");
        check(MainWindow.ProfileForVerifiedTarget(profile, new(true, "", target with { Path = @"C:\Other\Riot Client.exe" })) == null,
            "Verified executable at another path still requires explicit reassignment");
        check(MainWindow.ProfileForVerifiedTarget(profile, new(true, "")) == null,
            "Missing verified target cannot renew a binding");
        check(!MainWindow.ProfileMatches(profile, target with { Path = @"C:\Other\Riot Client.exe" }), "Same executable name at another path is rejected");
        check(!Autofill.Execute(new("fill", null, "synthetic", "synthetic")).Ok, "Fill requires a captured target; no foreground fallback");
        check(!Autofill.Execute(new("fill", target, "", "synthetic")).Ok, "Missing username blocks fill before any native operation");
        check(!Autofill.Execute(new("fill", target, "synthetic", "")).Ok, "Missing password blocks fill before any native operation");
        check(!Autofill.Execute(new("fill", target, "synthetic", new string('x', 10001))).Ok, "Oversized secret blocks fill before any native operation");
        check(!Autofill.Execute(new("submit", target, "synthetic", "synthetic")).Ok, "Unknown command cannot submit a login");
        foreach (int failureAt in new[] { 1, 2, 3 })
        {
            int checks = 0, users = 0, passwords = 0;
            reject(() => Autofill.GuardedFill(() => { if (++checks == failureAt) throw new InvalidOperationException(); }, () => users++, () => true, () => passwords++),
                "Target change aborts at checkpoint " + failureAt);
            check(passwords == 0 && users == (failureAt == 1 ? 0 : 1), "Target change never sends password at checkpoint " + failureAt);
        }
        int passwordWrites = 0;
        check(!Autofill.GuardedFill(() => { }, () => { }, () => false, () => passwordWrites++) && passwordWrites == 0,
            "Changed fields or unaccepted username prevent password transmission");
        var order = new StringBuilder();
        check(Autofill.GuardedFill(() => order.Append('V'), () => order.Append('U'), () => { order.Append('F'); return true; }, () => order.Append('P')) && order.ToString() == "VUVFVP",
            "Successful fill revalidates before each direct field write");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        check(!Autofill.RunAsync(new("probe"), canceled.Token).GetAwaiter().GetResult().Ok, "Canceled request does not start an automation worker");
        var prefs = new Preferences { FillShortcutF = -1 }; prefs.Normalize();
        check(prefs.FillShortcutF == 8, "Invalid fill shortcut preference normalizes to F8");

        const string master = "Synthetic migration master phrase!";
        var path = Path.Combine(folder, "autofill.nox");
        using var vault = new Vault(path); var data = vault.Create(master);
        data.Accounts.Add(new Account { Title = "Synthetic fill", Category = data.Categories[0], Username = "Synthetic user", Password = "Synthetic secret", Autofill = profile });
        vault.Save(data);
        check(vault.Open(master).Accounts[0].Autofill == profile, "App binding and username source survive encrypted round trip");
        var updatedData = data.Clone(); updatedData.Accounts[0].Autofill = renewed;
        vault.Save(updatedData);
        check(vault.Open(master).Accounts[0].Autofill == renewed, "Renewed Riot binding survives encrypted save and reopen");
        vault.Save(data);
        check(!Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains("Synthetic"), "Autofill binding is not persisted in plaintext");
        var clone = data.Clone(); clone.Accounts[0].Autofill = profile with { UseEmail = true };
        check(data.Accounts[0].Autofill == profile, "Changing a cloned profile preserves the original account");
        clone.Accounts[0].Autofill = profile with { Hash = "invalid" };
        var before = File.ReadAllBytes(path);
        reject(() => vault.Save(clone), "Invalid profile rejected before encrypted write");
        check(File.ReadAllBytes(path).SequenceEqual(before), "Rejected profile preserves the previous vault");

        // Independent v3 fixture: no autofill field, original Argon2id + AES-GCM layout.
        var json = Encoding.UTF8.GetBytes("{\"Categories\":[\"Old\"],\"Accounts\":[{\"Title\":\"v3 account\",\"Category\":\"Old\",\"Password\":\"Synthetic v3 secret\",\"Fields\":[{\"Label\":\"Recovery\",\"Value\":\"Synthetic code\"}]}]}");
        var bytes = new byte[68 + json.Length]; Encoding.ASCII.GetBytes("NOXVL003").CopyTo(bytes, 0);
        RandomNumberGenerator.Fill(bytes.AsSpan(8, 32)); RandomNumberGenerator.Fill(bytes.AsSpan(40, 12));
        var key = Vault.DeriveKey(master, bytes.AsSpan(8, 32).ToArray());
        using (var aes = new AesGcm(key, 16)) aes.Encrypt(bytes.AsSpan(40, 12), json, bytes.AsSpan(68), bytes.AsSpan(52, 16), bytes.AsSpan(0, 40));
        CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(json);
        var oldPath = Path.Combine(folder, "autofill-v3.nox"); File.WriteAllBytes(oldPath, bytes);
        using var previous = new Vault(oldPath);
        reject(() => previous.Open("Wrong synthetic password"), "v3 migration requires successful authentication");
        check(File.ReadAllBytes(oldPath).SequenceEqual(bytes), "Failed v3 authentication preserves original ciphertext");
        var migrated = previous.Open(master);
        check(migrated.Accounts[0].Autofill == null && migrated.Accounts[0].Password == "Synthetic v3 secret" && migrated.Accounts[0].Fields[0].Value == "Synthetic code",
            "v3 migration preserves credentials and does not auto-assign an app");
        check(Encoding.ASCII.GetString(File.ReadAllBytes(oldPath), 0, 8) == "NOXVL004", "v3 migration writes authenticated v4 format");
    }
}
