using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NoxVault;

// The decrypted vault contents. Property names are the JSON schema of the encrypted payload; never rename them.
internal sealed class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public string Email { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Website { get; set; } = "";
    public RiotFillProfile? Autofill { get; set; }
    public List<SecretField> Fields { get; set; } = new();
    public DateTime? DeletedUtc { get; set; }
    public bool Favorite { get; set; }
    public DateTime Updated { get; set; } = DateTime.Now;

    internal Account Copy() => new()
    {
        Id = Id, Title = Title, Category = Category, Email = Email, Username = Username, Password = Password, Notes = Notes,
        Website = Website, Autofill = Autofill, DeletedUtc = DeletedUtc, Favorite = Favorite, Updated = Updated,
        Fields = Fields.Select(f => new SecretField { Label = f.Label, Value = f.Value }).ToList(),
    };
}

internal sealed record RiotFillProfile(string Path, string Hash, bool UseEmail);

internal sealed class SecretField
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
}

internal sealed class VaultData
{
    const int MaxCategories = 200;
    const int MaxAccounts = 100000;
    const int MaxFields = 30;

    public List<string> Categories { get; set; } = new() { "Valorant", "League of Legends", "Andere Spiele", "Sonstige Accounts" };
    public Dictionary<string, string> CategoryColors { get; set; } = new();
    public List<Account> Accounts { get; set; } = new();

    internal VaultData Clone() => new()
    {
        Categories = new List<string>(Categories),
        CategoryColors = new Dictionary<string, string>(CategoryColors),
        Accounts = Accounts.Select(a => a.Copy()).ToList(),
    };

    internal int PurgeExpired(DateTime utcNow) =>
        Accounts.RemoveAll(a => a.DeletedUtc.HasValue && a.DeletedUtc.Value <= utcNow.AddDays(-30));

    // Runs before every encrypted write and after every decryption: invalid data never reaches disk or the UI.
    internal void Validate()
    {
        CategoryColors ??= new();
        if (CategoryColors.Count > MaxCategories || CategoryColors.Any(p => !ValidColor(p.Key, p.Value)))
            throw new InvalidDataException("Ungültige Kategoriefarbe.");
        if (Accounts != null && Accounts.Any(a => a?.Autofill is { } fill && !ValidFill(fill)))
            throw new InvalidDataException("Ungültige Ausfüllzuordnung.");
        if (!ValidCategories() || Accounts == null || Accounts.Count > MaxAccounts || Accounts.Any(a => !ValidAccount(a))
            || Accounts.Select(a => a.Id).Distinct().Count() != Accounts.Count)
            throw new InvalidDataException("Der Tresor enthält ungültige Einträge.");
    }

    bool ValidColor(string category, string? value) =>
        Categories != null && Categories.Contains(category) && value != null && value.Length == 7 && value[0] == '#'
        && value.Skip(1).All(Uri.IsHexDigit);

    static bool ValidFill(RiotFillProfile fill) =>
        !string.IsNullOrWhiteSpace(fill.Path) && Path.IsPathFullyQualified(fill.Path) && fill.Path.Length <= 1024
        && fill.Hash != null && fill.Hash.Length == 64 && fill.Hash.All(Uri.IsHexDigit);

    bool ValidCategories() =>
        Categories != null && Categories.Count > 0 && Categories.Count <= MaxCategories
        && Categories.All(c => !string.IsNullOrWhiteSpace(c) && c.Length <= 80)
        && Categories.Distinct(StringComparer.OrdinalIgnoreCase).Count() == Categories.Count;

    bool ValidAccount(Account? a) =>
        a != null && !string.IsNullOrWhiteSpace(a.Title) && Categories.Contains(a.Category)
        && a.Email != null && a.Username != null && a.Password != null && a.Notes != null && a.Website != null
        && a.Fields != null && a.Fields.Count <= MaxFields && a.Fields.All(ValidField);

    static bool ValidField(SecretField? f) =>
        f != null && !string.IsNullOrWhiteSpace(f.Label) && f.Label.Length <= 80 && f.Value != null && f.Value.Length <= 10000;
}
