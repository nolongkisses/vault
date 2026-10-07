using System;
using System.Collections.Generic;
using System.Linq;

namespace NoxVault;

// Read-only views over the vault: list filtering, website validation and the local password check.
internal static class AccountQueries
{
    internal const string SortByName = "Name";
    internal const string SortByUpdated = "Zuletzt geändert";

    internal static List<Account> Filter(VaultData data, string? category, bool favorites, string query, string sort)
    {
        var matches = Matches(data, category, favorites, query);
        var ordered = sort == SortByUpdated
            ? matches.OrderByDescending(a => a.Updated).ThenBy(a => a.Title, StringComparer.CurrentCultureIgnoreCase)
            : matches.OrderBy(a => a.Title, StringComparer.CurrentCultureIgnoreCase);
        return ordered.ToList();
    }

    static IEnumerable<Account> Matches(VaultData data, string? category, bool favorites, string query) =>
        data.Accounts.Where(a => a.DeletedUtc == null && (category == null || a.Category == category) && (!favorites || a.Favorite)
            && (string.IsNullOrWhiteSpace(query)
                || new[] { a.Title, a.Email, a.Username, a.Category, a.Website }.Any(v => v.Contains(query,
                    StringComparison.OrdinalIgnoreCase))));

    internal static bool ValidWebsite(string value) =>
        string.IsNullOrWhiteSpace(value)
        || (Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            && !string.IsNullOrWhiteSpace(uri.Host) && string.IsNullOrEmpty(uri.UserInfo));

    internal static List<(Guid Id, string Title, string Issue)> PasswordIssues(VaultData data)
    {
        var accounts = data.Accounts.Where(a => a.DeletedUtc == null).ToList();
        var reused = accounts.Where(a => a.Password.Length > 0).GroupBy(a => a.Password, StringComparer.Ordinal)
            .Where(g => g.Count() > 1).SelectMany(g => g).Select(a => a.Id).ToHashSet();
        return accounts.Where(a => a.Password.Length == 0 || reused.Contains(a.Id))
            .Select(a => (a.Id, a.Title, a.Password.Length == 0 ? "Kein Passwort hinterlegt" : "Passwort mehrfach verwendet")).ToList();
    }

    // Riot autofill is offered only for the two Riot game categories.
    internal static bool SupportsRiotFill(Account account) =>
        string.Equals(account.Category, "Valorant", StringComparison.OrdinalIgnoreCase)
        || string.Equals(account.Category, "League of Legends", StringComparison.OrdinalIgnoreCase);
}
