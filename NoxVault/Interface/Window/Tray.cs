using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace NoxVault;

// The notification-area icon keeps vault reachable while the window is hidden.
internal sealed partial class MainWindow
{
    NotifyIcon CreateTray()
    {
        using var iconStream = System.Windows.Application.GetResourceStream(Brand.IconUri)?.Stream
            ?? throw new IOException("Das vault-Symbol fehlt in der Anwendung.");
        var icon = new NotifyIcon { Icon = new System.Drawing.Icon(iconStream), Text = "vault · " + hotkeys.OpenLabel, Visible = true };
        var menu = new ContextMenuStrip();
        menu.Items.Add("vault öffnen", null, (_, _) => Dispatcher.Invoke(ShowVault));
        menu.Items.Add("vault ausblenden", null, (_, _) => Dispatcher.Invoke(Lock));
        menu.Items.Add("Beenden", null, (_, _) => Dispatcher.Invoke(() => { quitting = true; Close(); }));
        icon.ContextMenuStrip = menu;
        icon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowVault);
        return icon;
    }
}
