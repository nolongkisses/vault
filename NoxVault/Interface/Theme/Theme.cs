using System;
using System.Windows;
using System.Windows.Media;

namespace NoxVault;

internal enum TextRole { Micro, Small, Body, Title, Head, Display }

// Every token of the interface. Components use these names only; values live in Dark.xaml and Light.xaml.
internal static class Theme
{
    internal const string Bg = "Bg";
    internal const string Page = "Page";
    internal const string Raise1 = "Raise1";
    internal const string Raise2 = "Raise2";
    internal const string Raise3 = "Raise3";
    internal const string Fg = "Fg";
    internal const string Sub = "Sub";
    internal const string Faint = "Faint";
    internal const string ChipOn = "ChipOn";
    internal const string ChipOnFg = "ChipOnFg";
    internal const string Scrim = "Scrim";

    internal const double S1 = 2, S2 = 4, S3 = 6, S4 = 8, S5 = 12, S6 = 16, S7 = 24, S8 = 32, S9 = 40;
    internal const double Radius = 10, ControlRadius = 6, PopoverRadius = 12, DialogRadius = 16, SmallRadius = 4;
    internal const double ControlHeight = 28, SmallControl = 24, IconSize = 16;
    internal const double Sidebar = 224, SidebarFolded = 64, Caption = 40, Inset = 8;

    internal static readonly FontFamily Font = new("Segoe UI Variable Text, Segoe UI");
    internal static readonly FontFamily DisplayFont = new("Segoe UI Variable Display, Segoe UI");

    // Category colours are content, never chrome: they appear only as a small dot next to the category.
    internal static readonly (string Name, string Hex)[] CategoryPalette =
    {
        ("Blau", "#3B82F6"), ("Indigo", "#6E77D6"), ("Violett", "#9E8CFC"), ("Pink", "#D474B0"), ("Rot", "#E5484D"),
        ("Orange", "#F09A50"), ("Gold", "#D6B56E"), ("Grün", "#65BA74"), ("Türkis", "#53B9AB"), ("Grau", "#8B8D98"),
    };
    internal const string DefaultCategoryColor = "#3B82F6";

    internal static bool Dark { get; private set; } = true;
    internal static event Action? Changed;

    internal static double Size(TextRole role) => role switch
    {
        TextRole.Micro => 11,
        TextRole.Small => 12,
        TextRole.Body => 13,
        TextRole.Title => 15,
        TextRole.Head => 17,
        _ => 26,
    };

    // "System" follows Windows' app mode; the choice is stored in Preferences.Theme.
    internal static void Apply(string mode)
    {
        Dark = mode switch { "Dunkel" => true, "Hell" => false, _ => !SystemTheme.PrefersLight() };
        var source = new Uri($"pack://application:,,,/Interface/Theme/{(Dark ? "Dark" : "Light")}.xaml");
        var palette = new ResourceDictionary { Source = source };
        Application.Current.Resources.MergedDictionaries[0] = palette;
        foreach (Window window in Application.Current.Windows) Native.Round(window, Dark);
        Changed?.Invoke();
    }

    // A snapshot for drawing code (adorners, icons rendered to bitmaps) that cannot hold a resource reference.
    internal static Brush Brush(string token) => (Brush)Application.Current.Resources[token];

    internal static SolidColorBrush Color(string hex) =>
        new BrushConverter().ConvertFromString(hex) as SolidColorBrush ?? throw new FormatException("Keine Farbe: " + hex);
}
