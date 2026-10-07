using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using static NoxVault.Elements;

namespace NoxVault;

// The "more" menu of an account: duplicate, Riot fill, and moving it to the trash.
internal sealed partial class MainWindow
{
    Button CreateAccountMenu(Account account)
    {
        var button = IconButton(Icons.More, "Weitere Account-Aktionen", () => { });
        concealSecrets.Add(() => { if (button.ContextMenu != null) button.ContextMenu.IsOpen = false; });
        button.Click += (_, _) =>
        {
            var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom, MinWidth = 210 };
            MenuItem Add(string label, Action action)
            {
                var item = new MenuItem { Header = label };
                item.Click += (_, _) => action();
                menu.Items.Add(item);
                return item;
            }
            Add("Account duplizieren", () => Edit(account, true));
            if (AccountQueries.SupportsRiotFill(account))
            {
                menu.Items.Add(new Separator());
                if (account.Autofill != null) Add("In Riot ausfüllen", async () => await FillAccount(account.Id));
                Add(account.Autofill != null ? "Riot-Zuordnung bearbeiten" : "Riot verbinden", () => ConfigureFill(account.Id));
            }
            menu.Items.Add(new Separator());
            Visuals.SetIsDestructive(Add("In den Papierkorb", () => DeleteAccount(account)), true);
            button.ContextMenu = menu;
            menu.IsOpen = true;
        };
        return button;
    }
}
