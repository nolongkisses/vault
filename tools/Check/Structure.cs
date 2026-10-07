namespace VaultCheck;

// File and directory limits from docs/conventions.md. Code-level rules live in CodeRules.
static class Structure
{
    internal sealed record Result(int Files, int Directories, List<string> Violations);

    const int MaxFileLines = 500;
    const int MaxLineLength = 140;
    const int MaxCodeFilesPerDirectory = 8;
    const int MaxMarkdownPerDocsDirectory = 8;
    const int MaxSourceDepth = 4;
    static readonly string[] SkippedDirectories = [".git", "bin", "obj", "artifacts", "dist", ".vs", ".idea", "TestResults"];
    static readonly string[] TextExtensions = [".cs", ".xaml", ".md", ".ps1", ".csproj", ".json", ".editorconfig", ".gitignore"];
    static readonly string[] CodeExtensions = [".cs", ".xaml"];
    static readonly string[] EntryFiles = ["App.xaml", "App.xaml.cs", "EntryPoint.cs", "Program.cs"];
    static readonly string[] ForbiddenNames = ["utils", "util", "helpers", "helper", "misc", "common", "stuff", "shared"];
    static readonly string[] RequiredFiles = ["README.md", "AGENTS.md", "docs/README.md", "docs/conventions.md", "NoxVault/README.md"];

    internal static Result Check(string root)
    {
        var result = new Result(0, 0, []);
        foreach (var required in RequiredFiles)
            if (!File.Exists(Path.Combine(root, required))) result.Violations.Add($"{required}: required document missing");
        return Walk(root, root, result);
    }

    static Result Walk(string root, string directory, Result result)
    {
        var relative = Relative(root, directory);
        result = result with { Directories = result.Directories + 1 };
        CheckName(Path.GetFileName(directory), relative, result.Violations);
        CheckDepth(relative, result.Violations);
        var files = Directory.GetFiles(directory).Where(f => !IsLink(f)).ToArray();
        CheckCounts(relative, files, result.Violations);
        foreach (var file in files.Where(f => TextExtensions.Contains(Extension(f))))
        {
            result = result with { Files = result.Files + 1 };
            CheckFile(root, file, result.Violations);
        }
        foreach (var child in Directory.GetDirectories(directory).Order(StringComparer.Ordinal))
        {
            if (SkippedDirectories.Contains(Path.GetFileName(child))) continue;
            if (IsLink(child)) { result.Violations.Add($"{Relative(root, child)}: symbolic link not followed"); continue; }
            result = Walk(root, child, result);
        }
        return result;
    }

    static void CheckCounts(string relative, string[] files, List<string> violations)
    {
        int code = files.Count(f => CodeExtensions.Contains(Extension(f)) && !EntryFiles.Contains(Path.GetFileName(f)));
        if (code > MaxCodeFilesPerDirectory)
            violations.Add($"{relative}: {code} code files, limit {MaxCodeFilesPerDirectory}");
        bool docs = relative == "docs" || relative.StartsWith("docs/", StringComparison.Ordinal);
        int markdown = files.Count(f => Extension(f) == ".md");
        if (docs && markdown > MaxMarkdownPerDocsDirectory)
            violations.Add($"{relative}: {markdown} markdown files, limit {MaxMarkdownPerDocsDirectory}");
    }

    static void CheckDepth(string relative, List<string> violations)
    {
        if (!relative.StartsWith("NoxVault/", StringComparison.Ordinal)) return;
        int depth = relative.Count(c => c == '/');
        if (depth > MaxSourceDepth) violations.Add($"{relative}: depth {depth} below NoxVault, limit {MaxSourceDepth}");
    }

    static void CheckFile(string root, string file, List<string> violations)
    {
        var relative = Relative(root, file);
        CheckName(Path.GetFileNameWithoutExtension(file), relative, violations);
        var lines = File.ReadAllLines(file);
        if (lines.Length > MaxFileLines) violations.Add($"{relative}: {lines.Length} lines, limit {MaxFileLines}");
        if (CodeExtensions.Contains(Extension(file)))
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Length > MaxLineLength)
                    violations.Add($"{relative}:{i + 1}: {lines[i].Length} characters, limit {MaxLineLength}");
        if (Extension(file) == ".cs") violations.AddRange(CodeRules.Check(relative, File.ReadAllText(file)));
        if (Extension(file) == ".xaml") violations.AddRange(MarkupRules.Check(relative, lines));
    }

    static void CheckName(string name, string relative, List<string> violations)
    {
        var stem = name.Split('.')[0];
        if (ForbiddenNames.Contains(stem, StringComparer.OrdinalIgnoreCase)) violations.Add($"{relative}: catch-all name '{stem}'");
    }

    static bool IsLink(string path) => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    static string Extension(string file) => Path.GetFileName(file).StartsWith('.') ? Path.GetFileName(file) : Path.GetExtension(file);
    static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
}
