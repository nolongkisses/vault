using System.Diagnostics;
using VaultCheck;

// One command for every mandatory check; stops at the first failing step.
var root = FindRoot(AppContext.BaseDirectory);
bool skipSelfTest = args.Contains("--skip-self-test");
var project = Path.Combine(root, "NoxVault", "NoxVault.csproj");
// The SDK that runs this tool also checks the app, whatever `dotnet` happens to be first on PATH.
var dotnet = Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..",
    OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));

var steps = new (string Name, Func<bool> Run)[]
{
    ("checker tests", CheckerTests.Run),
    ("format", () => Run(dotnet, "format", project, "--verify-no-changes")),
    ("build", () => Run(dotnet, "build", project, "-c", "Release", "-warnaserror")),
    ("structure", () => Report(Structure.Check(root))),
    ("self-test", () => skipSelfTest ? Skipped() : SelfTest.Run(root)),
};
foreach (var (name, run) in steps)
{
    Console.WriteLine($"== {name}");
    if (!run())
    {
        Console.WriteLine($"FAILED: {name}");
        return 1;
    }
}
Console.WriteLine(skipSelfTest ? "OK (self-test skipped on request)" : "OK");
return 0;

static string FindRoot(string start)
{
    for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "NoxVault", "NoxVault.csproj"))) return dir.FullName;
    throw new DirectoryNotFoundException("NoxVault/NoxVault.csproj not found above " + start);
}

static bool Run(string file, params string[] arguments)
{
    var start = new ProcessStartInfo(file, arguments);
    // `dotnet run` hands its MSBuild settings to this process; a nested build must resolve its own.
    foreach (var name in start.Environment.Keys.Where(k => k.StartsWith("MSBuild", StringComparison.OrdinalIgnoreCase)).ToList())
        start.Environment.Remove(name);
    using var process = Process.Start(start) ?? throw new IOException("Cannot start " + file);
    process.WaitForExit();
    return process.ExitCode == 0;
}

static bool Report(Structure.Result result)
{
    foreach (var violation in result.Violations) Console.WriteLine(violation);
    Console.WriteLine($"checked {result.Files} files in {result.Directories} directories, {result.Violations.Count} violations");
    return result.Violations.Count == 0;
}

static bool Skipped()
{
    Console.WriteLine("SKIPPED: --skip-self-test was given; the result is not a full check.");
    return true;
}
