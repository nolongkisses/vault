namespace VaultCheck;

// Proves each rule fires on a known violation and stays quiet on clean input.
static class CheckerTests
{
    internal static bool Run()
    {
        var failures = new List<string>();
        int total = 0;
        void Expect(bool condition, string name) { total++; if (!condition) failures.Add(name); }

        var longFunction = "class A { void F() {\n" + string.Concat(Enumerable.Repeat("int x = 0;\n", 61)) + "} }";
        Expect(Code(longFunction).Any(v => v.Contains("function F")), "long function detected");
        Expect(Code("class A { void F(int a, int b, int c, int d, int e, int f) { } }").Any(v => v.Contains("6 parameters")),
            "parameter count detected");
        Expect(Code("class A { string F(string? s) => s!; }").Any(v => v.Contains("null-forgiving")), "null-forgiving detected");
        Expect(Code("class A { void F() { try { } catch { } } }").Any(v => v.Contains("catch-all")), "silent catch detected");
        Expect(Code("class A { void F() { try { } catch { /* why */ } } }").Any(), "block comments do not explain");
        Expect(!Code("class A { void F() { try { } catch { // gone\n } } }").Any(), "explained catch accepted");
        Expect(!Code("class A { void F() { try { } catch { throw; } } }").Any(), "rethrow accepted");
        Expect(Code("class A { string C = \"#123456\"; }").Any(v => v.Contains("colour")), "raw colour detected");
        Expect(!Code("class A { string C = \"#123456\"; }", "NoxVault/Interface/Theme/T.cs").Any(), "theme colours allowed");
        Expect(Code("class A { void F(B b) { b.FontSize = 14; } }").Any(v => v.Contains("font size 14")), "off-scale font detected");
        Expect(Code("class A { object T = new Thickness(5); }").Any(v => v.Contains("Thickness value 5")), "off-scale spacing");
        Expect(!Code("class A { object T = new Thickness(-16, 0, 8, 24); }").Any(), "on-scale spacing accepted");
        Expect(Code("class A { object R = new CornerRadius(7); }").Any(), "off-scale radius detected");
        Expect(Markup("<Border Margin=\"3\" Background=\"#101010\" FontSize=\"14\"/>").Count() == 3, "markup rules detected");
        Expect(!Markup("<Border Margin=\"{StaticResource Pad}\" Padding=\"8,4\" CornerRadius=\"6\"/>").Any(), "markup accepted");
        Expect(StructureFails(), "structure limits detected");

        foreach (var failure in failures) Console.WriteLine("checker test failed: " + failure);
        Console.WriteLine($"{total - failures.Count} of {total} checker tests passed");
        return failures.Count == 0;
    }

    static IEnumerable<string> Code(string source, string path = "NoxVault/X.cs") => CodeRules.Check(path, source);
    static IEnumerable<string> Markup(string line) => MarkupRules.Check("NoxVault/X.xaml", [line]);

    static bool StructureFails()
    {
        var root = Path.Combine(Path.GetTempPath(), "vault-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            var source = Directory.CreateDirectory(Path.Combine(root, "NoxVault", "a", "b", "c", "d", "e")).FullName;
            Directory.CreateDirectory(Path.Combine(root, "NoxVault", "Helpers"));
            for (int i = 0; i < 9; i++) File.WriteAllText(Path.Combine(root, "NoxVault", $"F{i}.cs"), "class F {}");
            File.WriteAllText(Path.Combine(source, "Long.cs"), new string('x', 141) + "\n" + string.Concat(Enumerable.Repeat("\n", 500)));
            var violations = Structure.Check(root).Violations;
            return new[] { "required document", "code files", "depth", "catch-all name", "characters", "lines, limit" }
                .All(expected => violations.Any(v => v.Contains(expected)));
        }
        finally { Directory.Delete(root, true); }
    }
}
