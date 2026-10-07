using System;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using static NoxVault.Ui;

namespace NoxVault;

public sealed partial class MainWindow
{
    Button CreateSortButton()
    {
        var button = Button("", () => { });
        button.Style = (Style)FindResource("SecondaryButton");
        button.Background = Brush("#0D0F12"); button.Height = 38;
        button.HorizontalAlignment = HorizontalAlignment.Right;
        button.Padding = new Thickness(8, 6, 8, 6);
        button.Margin = new Thickness(6, 0, 0, 0);
        button.VerticalAlignment = VerticalAlignment.Center;
        button.MaxWidth = 112;
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M1,3 L13,3 M1,7 L9,7 M1,11 L5,11"),
            Width = 12, Height = 12, Stretch = Stretch.Uniform,
            Stroke = Brush("#929292"), StrokeThickness = 1.2,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0)
        });
        var label = Text("", 11, "#AAAAAA"); label.TextWrapping = TextWrapping.NoWrap; row.Children.Add(label);
        var arrow = Text("⌄", 12, "#858585"); arrow.Margin = new Thickness(7, -2, 0, 0); row.Children.Add(arrow);
        button.Content = row;
        void RefreshLabel()
        {
            label.Text = prefs.AccountSort == "Name" ? "Name A–Z" : "Neueste";
            button.ToolTip = "Accounts sortieren";
            AutomationProperties.SetName(button, "Sortieren: " + label.Text);
        }
        RefreshLabel();
        button.Click += (_, _) =>
        {
            var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom, MinWidth = 185 };
            foreach (var option in new[] { "Name", "Zuletzt geändert" })
            {
                var line = new Grid(); line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) }); line.ColumnDefinitions.Add(new ColumnDefinition());
                line.Children.Add(Text(prefs.AccountSort == option ? "✓" : "", 12, "#CCCCCC"));
                var text = Text(option == "Name" ? "Name A–Z" : option, 12); Grid.SetColumn(text, 1); line.Children.Add(text);
                var item = new MenuItem { Header = line };
                AutomationProperties.SetName(item, option == "Name" ? "Name A–Z" : option);
                AutomationProperties.SetItemStatus(item, prefs.AccountSort == option ? "Ausgewählt" : "");
                item.Click += (_, _) =>
                {
                    var old = prefs.AccountSort; prefs.AccountSort = option;
                    try { if (!preview) prefs.Save(); RefreshLabel(); selected = null; accountPage = 0; RefreshAccounts(); accountScroller?.ScrollToTop(); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { prefs.AccountSort = old; status.Text = "Sortierung nicht gespeichert."; }
                };
                menu.Items.Add(item);
            }
            button.ContextMenu = menu; menu.IsOpen = true;
        };
        return button;
    }
}
