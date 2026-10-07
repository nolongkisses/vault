using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace NoxVault;

// Uniform random passwords from the chosen groups; every chosen group appears at least once.
internal sealed class GeneratorOptions
{
    internal int Length { get; set; } = 24;
    internal bool Upper { get; set; } = true;
    internal bool Lower { get; set; } = true;
    internal bool Digits { get; set; } = true;
    internal bool Symbols { get; set; } = true;
    internal bool AvoidSimilar { get; set; } = true;

    internal string[] Groups()
    {
        if (Length < 8 || Length > 128) throw new ArgumentException("Bitte 8 bis 128 Zeichen wählen.");
        var groups = new List<string>();
        if (Upper) groups.Add(AvoidSimilar ? "ABCDEFGHJKLMNPQRSTUVWXYZ" : "ABCDEFGHIJKLMNOPQRSTUVWXYZ");
        if (Lower) groups.Add(AvoidSimilar ? "abcdefghijkmnopqrstuvwxyz" : "abcdefghijklmnopqrstuvwxyz");
        if (Digits) groups.Add(AvoidSimilar ? "23456789" : "0123456789");
        if (Symbols) groups.Add("!@#$%&*+-?");
        if (groups.Count == 0) throw new ArgumentException("Mindestens eine Zeichengruppe auswählen.");
        return groups.ToArray();
    }

    internal string Generate()
    {
        var groups = Groups();
        var alphabet = string.Concat(groups);
        var chars = new char[Length];
        do
        {
            for (int i = 0; i < chars.Length; i++) chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }
        while (groups.Any(group => !chars.Any(group.Contains)));
        try { return new string(chars); }
        finally { Array.Clear(chars); }
    }
}
