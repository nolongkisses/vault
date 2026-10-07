using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace NoxVault;

public static class DesktopVisuals
{
    public static readonly DependencyProperty IsDropTargetProperty = DependencyProperty.RegisterAttached("IsDropTarget", typeof(bool), typeof(DesktopVisuals), new PropertyMetadata(false));
    public static bool GetIsDropTarget(DependencyObject element) => (bool)element.GetValue(IsDropTargetProperty);
    public static void SetIsDropTarget(DependencyObject element, bool value) => element.SetValue(IsDropTargetProperty, value);
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.RegisterAttached("IsSelected", typeof(bool), typeof(DesktopVisuals), new PropertyMetadata(false));
    public static bool GetIsSelected(DependencyObject element) => (bool)element.GetValue(IsSelectedProperty);
    public static void SetIsSelected(DependencyObject element, bool value) => element.SetValue(IsSelectedProperty, value);
    public static readonly DependencyProperty IsDestructiveProperty = DependencyProperty.RegisterAttached("IsDestructive", typeof(bool), typeof(DesktopVisuals), new PropertyMetadata(false));
    public static bool GetIsDestructive(DependencyObject element) => (bool)element.GetValue(IsDestructiveProperty);
    public static void SetIsDestructive(DependencyObject element, bool value) => element.SetValue(IsDestructiveProperty, value);
}

// Read-mode container only; reveal timers and clipboard ownership stay in MainWindow.
public sealed class DetailRow : Grid
{
    public DetailRow(string label, UIElement valueAndActions)
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var hover = new Border { Background = Ui.Brush("#171B21"), CornerRadius = new CornerRadius(5), Opacity = 0 };
        SetRowSpan(hover, 2); Children.Add(hover);
        var caption = Ui.Text(label, 12, "#8D96A3"); caption.Margin = new Thickness(8, 12, 8, 4); Children.Add(caption);
        var content = new Border { Child = valueAndActions, Padding = new Thickness(8, 0, 0, 12), BorderBrush = Ui.Brush("#252A31"), BorderThickness = new Thickness(0, 0, 0, 1) };
        SetRow(content, 1); Children.Add(content);
        MouseEnter += (_, _) => hover.BeginAnimation(OpacityProperty, new DoubleAnimation(1, System.TimeSpan.FromMilliseconds(140)));
        MouseLeave += (_, _) => hover.BeginAnimation(OpacityProperty, new DoubleAnimation(0, System.TimeSpan.FromMilliseconds(140)));
    }
}
