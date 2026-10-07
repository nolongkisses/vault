using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;

namespace NoxVault;

internal static class WindowFrame
{
    internal const double CaptionHeight = 36;
    internal const double CaptionButtonHeight = 30;
    public static ContentControl Attach(Window window, Grid layers, UIElement body)
    {
        // Use an opaque native surface with a rounded region. Layered WPF
        // surfaces can leave gray compositor pixels outside a visual clip.
        window.WindowStyle = WindowStyle.None;
        window.AllowsTransparency = false;
        window.Background = Ui.Brush("#0D0F12");
        layers.Background = Ui.Brush("#0D0F12");
        layers.SnapsToDevicePixels = false;
        RenderOptions.SetEdgeMode(layers, EdgeMode.Unspecified);
        void UpdateShape()
        {
            double radius = window.WindowState == WindowState.Maximized ? 0 : 12;
            double width = layers.ActualWidth, height = layers.ActualHeight;
            layers.Clip = new RectangleGeometry(new Rect(0, 0, width, height), radius, radius);
            Native.Round(window);
        }
        layers.SizeChanged += (_, _) => UpdateShape();
        window.StateChanged += (_, _) => UpdateShape();
        window.ContentRendered += (_, _) => Native.Round(window);
        window.StateChanged += (_, _) => window.Dispatcher.BeginInvoke(new Action(() => Native.Round(window)));
        WindowChrome.SetWindowChrome(window, new WindowChrome
        {
            CaptionHeight = CaptionHeight, ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(12),
            UseAeroCaptionButtons = false
        });
        layers.RowDefinitions.Add(new RowDefinition { Height = new GridLength(CaptionHeight) });
        layers.RowDefinitions.Add(new RowDefinition());
        Grid.SetRow(body, 1);
        var bar = new Grid { Background = Ui.Brush("#0D0F12") };
        var titleContent = new ContentControl { HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center };
        bar.Children.Add(titleContent);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center };
        Button Caption(string name, string geometry, Action action, bool close = false)
        {
            var button = Ui.Button(name, action); Ui.SetButtonIcon(button, geometry, name); button.Width = 34; button.Height = 26;
            button.Foreground = Ui.Brush("#8D96A3");
            button.Margin = new Thickness(1, 0, 1, 0); button.Padding = new Thickness(0); button.ToolTip = name;
            button.Background = Brushes.Transparent;
            WindowChrome.SetIsHitTestVisibleInChrome(button, true);
            var border = new FrameworkElementFactory(typeof(Border)); border.Name = "Surface";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6)); border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(presenter);
            var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BackgroundProperty, Ui.Brush(close ? "#74353E" : "#29313C"), "Surface"));
            hover.Setters.Add(new Setter(Control.ForegroundProperty, Ui.Brush("#F1F3F5"))); template.Triggers.Add(hover);
            var press = new Trigger { Property = Button.IsPressedProperty, Value = true };
            press.Setters.Add(new Setter(Border.BackgroundProperty, Ui.Brush(close ? "#B44951" : "#383838"), "Surface")); template.Triggers.Add(press);
            button.Template = template; actions.Children.Add(button); return button;
        }
        Caption("Minimieren", "M0,5 L10,5", () => SystemCommands.MinimizeWindow(window));
        var maximize = Caption("Maximieren", "M0.5,0.5 L9.5,0.5 L9.5,9.5 L0.5,9.5 Z", () =>
        {
            if (window.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(window);
            else SystemCommands.MaximizeWindow(window);
        });
        window.StateChanged += (_, _) =>
        {
            bool maximized = window.WindowState == WindowState.Maximized;
            Ui.SetButtonIcon(maximize, maximized ? "M2.5,0.5 L9.5,0.5 L9.5,7.5 M0.5,2.5 L7.5,2.5 L7.5,9.5 L0.5,9.5 Z" : "M0.5,0.5 L9.5,0.5 L9.5,9.5 L0.5,9.5 Z", maximized ? "Wiederherstellen" : "Maximieren");
            maximize.ToolTip = maximized ? "Wiederherstellen" : "Maximieren";
            System.Windows.Automation.AutomationProperties.SetName(maximize, (string)maximize.ToolTip);
        };
        Caption("Schließen", "M0,0 L10,10 M10,0 L0,10", window.Close, true);
        var controls = new Border { Child = actions, Background = Ui.Brush("#171B21"), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(2), Margin = new Thickness(0, 0, 10, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        bar.Children.Add(controls); layers.Children.Add(bar);
        return titleContent;
    }
}
