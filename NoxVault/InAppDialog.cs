using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace NoxVault;

// Uses the existing modal call sites, but presents content in the main visual tree.
public sealed class InAppDialog : ContentControl
{
    readonly MainWindow host;
    DispatcherFrame? frame;
    IInputElement? previousFocus;
    bool? result;
    bool open;
    public event EventHandler? Closed;
    public event CancelEventHandler? Closing;
    public InAppDialog(FrameworkElement owner, string title, UIElement content, double width, bool scrollContent = true)
    {
        host = owner as MainWindow ?? (MainWindow)Window.GetWindow(owner);
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        var overlay = new Grid { Background = Ui.Brush("#AA000000") };
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition());
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var close = Ui.Button("×", Close); close.Width = 30; close.Height = 30;
        close.Content = Ui.CloseIcon();
        close.Padding = new Thickness(0); close.Margin = new Thickness(12, 0, 0, 0);
        close.Background = System.Windows.Media.Brushes.Transparent;
        if (!scrollContent) close.Style = (Style)Application.Current.FindResource("SettingsAction");
        System.Windows.Automation.AutomationProperties.SetName(close, "Dialog schließen");
        DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
        header.Children.Add(Ui.Text(title, 23, bold: true)); layout.Children.Add(header);
        // Keep content aligned with the header; place the slim scrollbar in the outer gutter.
        UIElement body = scrollContent
            ? new ScrollViewer { Content = content, Style = (Style)Application.Current.FindResource("OverlayScrollViewer"), Margin = new Thickness(0, 0, -16, 0) }
            : content;
        Grid.SetRow(body, 1); layout.Children.Add(body);
        var card = Ui.Card(layout, "#111419", 24); card.MaxWidth = width;
        card.Margin = new Thickness(24); card.VerticalAlignment = VerticalAlignment.Center;
        overlay.Children.Add(card); Content = overlay;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);
        KeyboardNavigation.SetControlTabNavigation(this, KeyboardNavigationMode.Cycle);
        FocusManager.SetIsFocusScope(this, true);
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
    }
    public bool? DialogResult { get => result; set { result = value; Close(); } }
    public bool? ShowDialog()
    {
        if (open) throw new InvalidOperationException("Dialog ist bereits geöffnet.");
        open = true; previousFocus = Keyboard.FocusedElement;
        frame = new DispatcherFrame(); host.PushDialog(this);
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (open && !IsKeyboardFocusWithin) MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }));
        Dispatcher.PushFrame(frame);
        return result;
    }
    public void Close() => Close(false);
    internal void Close(bool force)
    {
        if (!open) return;
        var args = new CancelEventArgs();
        // Security-triggered closure must never open a confirmation or be delayed.
        if (!force) Closing?.Invoke(this, args);
        if (args.Cancel && !force) return;
        open = false; host.PopDialog(this); Closed?.Invoke(this, EventArgs.Empty);
        if (frame != null) frame.Continue = false;
        if (previousFocus is UIElement element && element.IsVisible && element.IsEnabled) Keyboard.Focus(previousFocus);
    }
}
