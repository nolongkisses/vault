using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using static NoxVault.Ui;

namespace NoxVault;

public sealed partial class MainWindow
{
    Button CreateAccountMenu(Account account)
    {
        var button = IconButton("M3,12 L4,12 M11,12 L12,12 M19,12 L20,12", "Weitere Account-Aktionen", () => { });
        button.Width = 32; button.Height = 32;
        button.Margin = new Thickness(4, 0, 0, 4);
        concealSecrets.Add(() => { if (button.ContextMenu != null) button.ContextMenu.IsOpen = false; });
        button.Click += (_, _) =>
        {
            var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom, MinWidth = 210 };
            MenuItem Add(string label, Action action)
            {
                var item = new MenuItem { Header = label };
                item.Click += (_, _) => action(); menu.Items.Add(item); return item;
            }
            Add("Account duplizieren", () => Edit(account, true));
            if (SupportsRiotFill(account))
            {
                menu.Items.Add(new Separator());
                if (account.Autofill != null) Add("In Riot ausfüllen", async () => await FillAccount(account.Id));
                Add(account.Autofill != null ? "Riot-Zuordnung bearbeiten" : "Riot verbinden", () => ConfigureFill(account.Id));
            }
            button.ContextMenu = menu; menu.IsOpen = true;
        };
        return button;
    }
}
