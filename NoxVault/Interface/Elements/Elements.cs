using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NoxVault;

// The few building blocks every view is made of. Colours are always resource references, so a theme switch repaints them.
internal static class Elements
{
    internal static TextBlock Text(string text, TextRole role = TextRole.Body, string tone = Theme.Fg, bool bold = false)
    {
        var block = new TextBlock
        {
            Text = text, FontSize = Theme.Size(role), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            FontFamily = role >= TextRole.Head ? Theme.DisplayFont : Theme.Font,
            TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center,
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, tone);
        return block;
    }

    internal static TextBlock Line(string text, TextRole role = TextRole.Body, string tone = Theme.Fg, bool bold = false)
    {
        var block = Text(text, role, tone, bold);
        block.TextWrapping = TextWrapping.NoWrap;
        block.TextTrimming = TextTrimming.CharacterEllipsis;
        return block;
    }

    internal static Button Button(string text, Action action, string style = "SecondaryButton")
    {
        var button = new Button { Content = text };
        button.SetResourceReference(FrameworkElement.StyleProperty, style);
        button.Click += (_, _) => action();
        AutomationProperties.SetName(button, text);
        return button;
    }

    internal static Button Primary(string text, Action action) => Button(text, action, "PrimaryButton");
    internal static Button Ghost(string text, Action action) => Button(text, action, "GhostButton");

    internal static Button IconButton(string geometry, string label, Action action, bool small = false)
    {
        var button = Button(label, action, small ? "SmallIconButton" : "IconButton");
        SetIcon(button, geometry, label, small ? Theme.Size(TextRole.Small) : Theme.IconSize);
        return button;
    }

    // An icon that follows the button's foreground, so hover and disabled states recolour it. Keeps the size it had.
    internal static void SetIcon(Button button, string geometry, string label, double? size = null)
    {
        var icon = Icon(geometry, size ?? (button.Content as Path)?.Width ?? Theme.IconSize);
        icon.SetBinding(Shape.FillProperty, new System.Windows.Data.Binding(nameof(Control.Foreground)) { Source = button });
        button.Content = icon;
        button.ToolTip = label;
        AutomationProperties.SetName(button, label);
    }

    // Parsed once and frozen: the detail view rebuilds its icons on every selection.
    static readonly Dictionary<string, Geometry> Parsed = new();

    internal static Path Icon(string geometry, double size = Theme.IconSize, string? tone = null)
    {
        if (!Parsed.TryGetValue(geometry, out var data))
        {
            data = Geometry.Parse(geometry);
            data.Freeze();
            Parsed[geometry] = data;
        }
        var icon = new Path
        {
            Data = data, Width = size, Height = size, Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        if (tone != null) icon.SetResourceReference(Shape.FillProperty, tone);
        return icon;
    }

    // Icon and label side by side; the icon is never larger than the text next to it.
    internal static StackPanel IconLabel(string geometry, string text, TextRole role = TextRole.Body)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = Icon(geometry, Theme.Size(role));
        icon.SetBinding(Shape.FillProperty, new System.Windows.Data.Binding("(TextElement.Foreground)") { Source = row });
        row.Children.Add(icon);
        var label = Text(text, role);
        label.ClearValue(TextBlock.ForegroundProperty);
        label.Margin = new Thickness(Theme.S3, 0, 0, 0);
        row.Children.Add(label);
        return row;
    }

    internal static TextBox Input(string label, string value = "")
    {
        var box = new TextBox { Text = value, MaxLength = 2048 };
        AutomationProperties.SetName(box, label);
        return box;
    }

    internal static PasswordBox Secret(string label)
    {
        var box = new PasswordBox { MaxLength = 2048 };
        AutomationProperties.SetName(box, label);
        return box;
    }

    internal static void Field(Panel panel, string label, UIElement input)
    {
        var caption = Text(label, TextRole.Small, Theme.Sub);
        caption.Margin = new Thickness(0, Theme.S5, 0, Theme.S3);
        panel.Children.Add(caption);
        panel.Children.Add(input);
    }

    internal static Border Surface(UIElement child, string tone = Theme.Raise1, double padding = Theme.S5, double radius = Theme.Radius)
    {
        var surface = new Border { Child = child, Padding = new Thickness(padding), CornerRadius = new CornerRadius(radius) };
        surface.SetResourceReference(Border.BackgroundProperty, tone);
        return surface;
    }

    internal static Ellipse Dot(string hex, double size = Theme.S4) => new()
    {
        Width = size, Height = size, Fill = Theme.Color(hex), VerticalAlignment = VerticalAlignment.Center,
    };
}
