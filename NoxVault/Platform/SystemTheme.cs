using Microsoft.Win32;

namespace NoxVault;

// Reads the "app mode" from Windows personalization; missing values mean dark, the Windows default for vault.
internal static class SystemTheme
{
    const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    internal static bool PrefersLight()
    {
        using var key = Registry.CurrentUser.OpenSubKey(Personalize);
        return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
    }
}
