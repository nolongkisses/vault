using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace NoxVault;

public static class Ui
{
    public const string CopyGeometry = "M8,5 L8,2 L21,2 L21,15 L18,15 M3,8 L16,8 L16,21 L3,21 Z";
    public const string EyeGeometry = "M2,12 C6,4 18,4 22,12 C18,20 6,20 2,12 Z M15,12 A3,3 0 1 1 9,12 A3,3 0 1 1 15,12";
    public const string EyeOffGeometry = "M3,3 L21,21 M9,6.5 C14,5 19,7 22,12 C21,14 20,15 18,16 M15,17.5 C10,19 5,17 2,12 C3,10 4,9 6,8 M10,10 A3,3 0 0 0 14,14";
    public static void SetButtonIcon(Button button, string geometry, string label)
    {
        var icon = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(geometry), Width = 12, Height = 12, Stretch = Stretch.Uniform,
            StrokeThickness = 1.2, StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        icon.SetBinding(System.Windows.Shapes.Shape.StrokeProperty,
            new System.Windows.Data.Binding(nameof(Control.Foreground)) { Source = button });
        button.Content = icon;
        button.ToolTip = label;
        AutomationProperties.SetName(button, label);
    }
    public static Button IconButton(string geometry, string label, Action action)
    {
        var button = Button(label, action);
        button.Width = 32; button.Height = 32; button.Padding = new Thickness(8);
        button.Background = Brushes.Transparent;
        button.SetResourceReference(FrameworkElement.StyleProperty, "CredentialIconButton");
        SetButtonIcon(button, geometry, label);
        return button;
    }
    public static FrameworkElement Wordmark(double height = 32) => Text("vault", height * 0.85, bold: true);
    public static System.Windows.Shapes.Path CloseIcon() => new()
    {
        Data = Geometry.Parse("M0,0 L10,10 M10,0 L0,10"),
        Stroke = Brush("#BDBDBD"), StrokeThickness = 1,
        Width = 10, Height = 10, Stretch = Stretch.Uniform,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
    };
    public static System.Windows.Controls.Image Logo(double size)
    {
        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(Brush("#141414"), new Pen(Brush("#343434"), 1), new RectangleGeometry(new Rect(2, 2, 60, 60), 14, 14)));
        drawing.Children.Add(new GeometryDrawing(Brush("#F2F2EE"), null,
            Geometry.Parse("M16,19 L24.5,19 L32,39 L39.5,19 L48,19 L36,47 L28,47 Z")));
        drawing.Freeze();
        return new System.Windows.Controls.Image { Source = new DrawingImage(drawing), Width = size, Height = size, Stretch = Stretch.Uniform };
    }
    public static SolidColorBrush Brush(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    public static TextBlock Text(string text, double size = 14, string color = "#F1F3F5", bool bold = false) => new()
    {
        Text = text, FontSize = size, Foreground = Brush(color), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        FontFamily = new FontFamily(size >= 20 ? "Bahnschrift, Segoe UI" : "Segoe UI"),
        TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center
    };
    public static Button Button(string text, Action action, bool primary = false)
    {
        var b = new Button { Content = text };
        if (primary) b.SetResourceReference(FrameworkElement.StyleProperty, "PrimaryButton");
        b.Click += (_, _) => action();
        AutomationProperties.SetName(b, text);
        return b;
    }
    public static Border Card(UIElement child, string bg = "#111419", double padding = 20) => new()
    { Child = child, Background = Brush(bg), CornerRadius = new CornerRadius(8), Padding = new Thickness(padding), BorderBrush = Brush("#252A31"), BorderThickness = new Thickness(1) };
    public static TextBox Input(string label, string value = "")
    {
        var box = new TextBox { Text = value, MaxLength = 2048, Height = 40, MinHeight = 38, VerticalContentAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(box, label);
        return box;
    }
    public static PasswordBox Secret(string label)
    {
        var box = new PasswordBox { MaxLength = 2048, Height = 40, VerticalContentAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(box, label); return box;
    }
    public static void Field(Panel panel, string label, UIElement input)
    {
        var t = Text(label, 12, "#A5ABB5"); t.Margin = new Thickness(0, 16, 0, 7);
        panel.Children.Add(t); panel.Children.Add(input);
    }
    public static InAppDialog Dialog(FrameworkElement owner, string title, UIElement content, double width = 500) => new(owner, title, content, width);
    public static string? Ask(FrameworkElement owner, string title, string label, string value = "")
    {
        var panel = new StackPanel(); var input = Input(label, value); input.MaxLength = 80; Field(panel, label, input);
        var w = Dialog(owner, title, panel);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 24, 0, 0) };
        buttons.Children.Add(Button("Speichern", () => { if (!string.IsNullOrWhiteSpace(input.Text)) w.DialogResult = true; }, true));
        buttons.Children.Add(Button("Abbrechen", () => w.Close())); panel.Children.Add(buttons);
        w.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        return w.ShowDialog() == true ? input.Text.Trim() : null;
    }
    public static bool Confirm(FrameworkElement owner, string title, string message)
    {
        var panel = new StackPanel(); var t = Text(message, 14, "#B5BAC4"); t.Margin = new Thickness(0, 18, 0, 24); panel.Children.Add(t);
        var w = Dialog(owner, title, panel); var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(Button("Bestätigen", () => w.DialogResult = true, true));
        buttons.Children.Add(Button("Abbrechen", () => w.Close())); panel.Children.Add(buttons);
        return w.ShowDialog() == true;
    }
}
