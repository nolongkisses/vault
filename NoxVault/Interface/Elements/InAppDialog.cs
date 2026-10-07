using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using static NoxVault.Elements;

namespace NoxVault;

// A modal sheet inside the main window: keeps the ShowDialog call style, but no native window and no taskbar entry.
internal sealed class InAppDialog : ContentControl
{
    readonly MainWindow host;
    readonly Border sheet;
    DispatcherFrame? frame;
    IInputElement? previousFocus;
    bool? result;
    bool open;

    internal InAppDialog(FrameworkElement owner, string title, UIElement content, double width, bool scrollContent = true)
    {
        host = owner as MainWindow ?? Window.GetWindow(owner) as MainWindow ?? throw new InvalidOperationException("Kein Hauptfenster.");
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition());
        layout.Children.Add(Header(title));
        UIElement body = scrollContent ? new ScrollViewer
        {
            Content = content,
            Style = (Style)Application.Current.FindResource("GutterScrollViewer")
        } : content;
        Grid.SetRow(body, 1);
        layout.Children.Add(body);
        sheet = Surface(layout, Theme.Page, Theme.S7, Theme.DialogRadius);
        sheet.MaxWidth = width;
        sheet.Margin = new Thickness(Theme.S7);
        sheet.VerticalAlignment = VerticalAlignment.Center;
        sheet.RenderTransform = new TranslateTransform();
        var scrim = new Grid { Children = { sheet } };
        scrim.SetResourceReference(Panel.BackgroundProperty, Theme.Scrim);
        Content = scrim;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);
        KeyboardNavigation.SetControlTabNavigation(this, KeyboardNavigationMode.Cycle);
        FocusManager.SetIsFocusScope(this, true);
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
    }

    internal event EventHandler? Closed;
    internal event CancelEventHandler? Closing;

    internal bool? DialogResult
    {
        get => result;
        set { result = value; Close(); }
    }

    DockPanel Header(string title)
    {
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, Theme.S6) };
        var close = IconButton(Icons.Close, "Dialog schließen", Close, small: true);
        close.Margin = new Thickness(Theme.S5, 0, 0, 0);
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        header.Children.Add(Text(title, TextRole.Head, bold: true));
        return header;
    }

    internal bool? ShowDialog()
    {
        if (open) throw new InvalidOperationException("Dialog ist bereits geöffnet.");
        open = true;
        previousFocus = Keyboard.FocusedElement;
        frame = new DispatcherFrame();
        host.PushDialog(this);
        Appear();
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (open && !IsKeyboardFocusWithin) MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }));
        Dispatcher.PushFrame(frame);
        return result;
    }

    void Appear()
    {
        Opacity = 0;
        ((TranslateTransform)sheet.RenderTransform).Y = Theme.S4;
        Motion.Animate(this, OpacityProperty, 1, Motion.Settle);
        Motion.Animate((TranslateTransform)sheet.RenderTransform, TranslateTransform.YProperty, 0, Motion.Settle);
    }

    internal void Close() => Close(false);

    internal void Close(bool force)
    {
        if (!open) return;
        var args = new CancelEventArgs();
        // Security-triggered closure must never open a confirmation or be delayed.
        if (!force) Closing?.Invoke(this, args);
        if (args.Cancel && !force) return;
        open = false;
        host.PopDialog(this);
        Closed?.Invoke(this, EventArgs.Empty);
        if (frame != null) frame.Continue = false;
        if (previousFocus is UIElement element && element.IsVisible && element.IsEnabled) Keyboard.Focus(previousFocus);
    }

    internal static InAppDialog Create(FrameworkElement owner, string title, UIElement content, double width = 500) =>
        new(owner, title, content, width);

    internal static Panel Actions(params Button[] buttons)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        row.Margin = new Thickness(0, Theme.S7, 0, 0);
        foreach (var button in buttons)
        {
            button.Margin = new Thickness(Theme.S4, 0, 0, 0);
            row.Children.Add(button);
        }
        return row;
    }

    internal static string? Ask(FrameworkElement owner, string title, string label, string value = "")
    {
        var panel = new StackPanel();
        var input = Input(label, value);
        input.MaxLength = 80;
        Field(panel, label, input);
        var dialog = Create(owner, title, panel);
        panel.Children.Add(Actions(Ghost("Abbrechen", dialog.Close),
            Primary("Speichern", () => { if (!string.IsNullOrWhiteSpace(input.Text)) dialog.DialogResult = true; })));
        dialog.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        return dialog.ShowDialog() == true ? input.Text.Trim() : null;
    }

    internal static bool Confirm(FrameworkElement owner, string title, string message)
    {
        var panel = new StackPanel();
        panel.Children.Add(Text(message, TextRole.Body, Theme.Sub));
        var dialog = Create(owner, title, panel);
        panel.Children.Add(Actions(Ghost("Abbrechen", dialog.Close), Primary("Bestätigen", () => dialog.DialogResult = true)));
        return dialog.ShowDialog() == true;
    }
}
