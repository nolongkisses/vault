using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using static NoxVault.Elements;

namespace NoxVault;

// The frame around the stage: one caption band in the frame colour, with the window buttons on the right.
internal static class WindowFrame
{
    internal const double ResizeBorder = 6;
    const double WindowRadius = 12;

    internal static ContentControl Attach(Window window, Grid layers, UIElement body)
    {
        // Use an opaque native surface with a rounded region. Layered WPF
        // surfaces can leave gray compositor pixels outside a visual clip.
        window.WindowStyle = WindowStyle.None;
        window.AllowsTransparency = false;
        window.SetResourceReference(Control.BackgroundProperty, Theme.Bg);
        layers.SetResourceReference(Panel.BackgroundProperty, Theme.Bg);
        void UpdateShape()
        {
            double radius = window.WindowState == WindowState.Maximized ? 0 : WindowRadius;
            layers.Clip = new RectangleGeometry(new Rect(0, 0, layers.ActualWidth, layers.ActualHeight), radius, radius);
            Native.Round(window, Theme.Dark);
        }
        layers.SizeChanged += (_, _) => UpdateShape();
        window.StateChanged += (_, _) => UpdateShape();
        window.ContentRendered += (_, _) => Native.Round(window, Theme.Dark);
        WindowChrome.SetWindowChrome(window, new WindowChrome
        {
            CaptionHeight = Theme.Caption, ResizeBorderThickness = new Thickness(ResizeBorder),
            GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(WindowRadius), UseAeroCaptionButtons = false,
        });
        window.SourceInitialized += (_, _) => WindowBounds.Attach(window);
        layers.RowDefinitions.Add(new RowDefinition { Height = new GridLength(Theme.Caption) });
        layers.RowDefinitions.Add(new RowDefinition());
        Grid.SetRow(body, 1);
        var caption = new ContentControl { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Stretch };
        layers.Children.Add(caption);
        layers.Children.Add(Buttons(window));
        return caption;
    }

    // Windows' own caption glyphs (Segoe Fluent Icons; MDL2 Assets on Windows 10) keep the buttons native in size and weight.
    const string MinimizeGlyph = "\uE921", MaximizeGlyph = "\uE922", RestoreGlyph = "\uE923", CloseGlyph = "\uE8BB";
    static readonly FontFamily CaptionFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    static StackPanel Buttons(Window window)
    {
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, Theme.Inset, 0),
        };
        Panel.SetZIndex(buttons, 10);
        Button Add(string glyph, string label, Action action)
        {
            var button = Button(label, action, "IconButton");
            button.Content = new TextBlock { Text = glyph, FontFamily = CaptionFont, FontSize = Theme.Size(TextRole.Micro) };
            button.ToolTip = label;
            WindowChrome.SetIsHitTestVisibleInChrome(button, true);
            buttons.Children.Add(button);
            return button;
        }
        Add(MinimizeGlyph, "Minimieren", () => SystemCommands.MinimizeWindow(window));
        var maximize = Add(MaximizeGlyph, "Maximieren", () =>
        {
            if (window.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(window);
            else SystemCommands.MaximizeWindow(window);
        });
        window.StateChanged += (_, _) =>
        {
            bool maximized = window.WindowState == WindowState.Maximized;
            ((TextBlock)maximize.Content).Text = maximized ? RestoreGlyph : MaximizeGlyph;
            maximize.ToolTip = maximized ? "Wiederherstellen" : "Maximieren";
            System.Windows.Automation.AutomationProperties.SetName(maximize, (string)maximize.ToolTip);
        };
        Add(CloseGlyph, "Schließen", window.Close);
        return buttons;
    }
}
