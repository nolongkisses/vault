using System;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace NoxVault;

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
        var groups = Groups(); var alphabet = string.Concat(groups); var chars = new char[Length];
        do { for (int i = 0; i < chars.Length; i++) chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]; }
        while (groups.Any(group => !chars.Any(group.Contains)));
        try { return new string(chars); } finally { Array.Clear(chars); }
    }
}

internal static class AccountQueries
{
    internal static List<Account> Filter(VaultData data, string? category, bool favorites, string query, string sort) =>
        (sort == "Zuletzt geändert" ? Matches(data, category, favorites, query).OrderByDescending(a => a.Updated).ThenBy(a => a.Title, StringComparer.CurrentCultureIgnoreCase)
            : Matches(data, category, favorites, query).OrderBy(a => a.Title, StringComparer.CurrentCultureIgnoreCase)).ToList();
    static IEnumerable<Account> Matches(VaultData data, string? category, bool favorites, string query) => data.Accounts.Where(a => a.DeletedUtc == null &&
        (category == null || a.Category == category) && (!favorites || a.Favorite) && (string.IsNullOrWhiteSpace(query) ||
        new[] { a.Title, a.Email, a.Username, a.Category, a.Website }.Any(v => v.Contains(query, StringComparison.OrdinalIgnoreCase))));
    internal static bool ValidWebsite(string value) => string.IsNullOrWhiteSpace(value) ||
        (Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) && !string.IsNullOrWhiteSpace(uri.Host) && string.IsNullOrEmpty(uri.UserInfo));
    internal static List<(Guid id, string title, string issue)> PasswordIssues(VaultData data)
    {
        var accounts = data.Accounts.Where(a => a.DeletedUtc == null).ToList();
        var reused = accounts.Where(a => a.Password.Length > 0).GroupBy(a => a.Password, StringComparer.Ordinal).Where(g => g.Count() > 1).SelectMany(g => g).Select(a => a.Id).ToHashSet();
        return accounts.Where(a => a.Password.Length == 0 || reused.Contains(a.Id)).Select(a => (a.Id, a.Title, a.Password.Length == 0 ? "Kein Passwort hinterlegt" : "Passwort mehrfach verwendet")).ToList();
    }
}
