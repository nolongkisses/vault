using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using static NoxVault.Elements;

namespace NoxVault;

// What the window shows while the vault is closed: open it through Windows, or explain why that is not possible.
internal sealed partial class MainWindow
{
    void BuildGate(string? notice = null)
    {
        captionContent.Content = CaptionBrand(Theme.Sidebar);
        ClearRoot();
        bool creating = false;
        try
        {
            creating = !session.Vault.Exists;
            gateState = creating ? "Create" : "Unlock";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            gateState = "Unavailable";
            session.AccessError = ex.Message;
        }
        bool available = !creating && gateState != "Unavailable" && session.WindowsAccessStored;
        if (available && !preview) gateState = "WindowsAccess";
        var form = new StackPanel
        {
            MaxWidth = 480, VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var title = Text(available ? "Deine Zugangsdaten sind bereit." : "Windows-Zugriff nicht verfügbar", TextRole.Display, bold: true);
        form.Children.Add(title);
        if (!available)
        {
            var fallback = "Der Windows-Schlüssel fehlt. Dein vorhandener Tresor bleibt erhalten.";
            var message = Text(notice ?? (session.AccessError.Length > 0 ? session.AccessError : fallback), TextRole.Body, Theme.Sub);
            message.Margin = new Thickness(0, Theme.S4, 0, 0);
            form.Children.Add(message);
        }
        var open = Primary(available ? "Zugangsdaten öffnen" : "Erneut versuchen", ShowVault);
        open.HorizontalAlignment = HorizontalAlignment.Left;
        open.Margin = new Thickness(0, Theme.S7, 0, 0);
        form.Children.Add(open);
        var stage = Stage(form);
        stage.Margin = new Thickness(Theme.Inset, 0, Theme.Inset, Theme.Inset);
        root.Children.Add(stage);
    }

    Border Stage(UIElement content)
    {
        var stage = Surface(content, Theme.Page, Theme.S7, Theme.Radius);
        stage.Margin = new Thickness(0, 0, Theme.Inset, Theme.Inset);
        return stage;
    }

    // App icon and name, in the caption band above the sidebar.
    FrameworkElement CaptionBrand(double width)
    {
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(Theme.S6, 0, 0, 0) };
        brand.Children.Add(Brand.Icon(Theme.IconSize));
        var name = Text("vault", TextRole.Head, bold: true);
        name.Margin = new Thickness(Theme.S4, 0, 0, 0);
        brand.Children.Add(name);
        return new Grid { Width = width, Children = { brand } };
    }
}
