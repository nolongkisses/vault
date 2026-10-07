using System.Windows;
using System.Windows.Media;

namespace NoxVault;

// Attached states the control styles react to (Controls.xaml).
internal static class Visuals
{
    internal static readonly DependencyProperty HoverProperty =
        DependencyProperty.RegisterAttached("Hover", typeof(Brush), typeof(Visuals), new PropertyMetadata(null));
    internal static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.RegisterAttached("IsSelected", typeof(bool), typeof(Visuals), new PropertyMetadata(false));
    internal static readonly DependencyProperty IsDropTargetProperty =
        DependencyProperty.RegisterAttached("IsDropTarget", typeof(bool), typeof(Visuals), new PropertyMetadata(false));
    internal static readonly DependencyProperty IsDestructiveProperty =
        DependencyProperty.RegisterAttached("IsDestructive", typeof(bool), typeof(Visuals), new PropertyMetadata(false));

    public static Brush? GetHover(DependencyObject element) => (Brush?)element.GetValue(HoverProperty);
    public static void SetHover(DependencyObject element, Brush? value) => element.SetValue(HoverProperty, value);
    public static bool GetIsSelected(DependencyObject element) => (bool)element.GetValue(IsSelectedProperty);
    public static void SetIsSelected(DependencyObject element, bool value) => element.SetValue(IsSelectedProperty, value);
    public static bool GetIsDropTarget(DependencyObject element) => (bool)element.GetValue(IsDropTargetProperty);
    public static void SetIsDropTarget(DependencyObject element, bool value) => element.SetValue(IsDropTargetProperty, value);
    public static bool GetIsDestructive(DependencyObject element) => (bool)element.GetValue(IsDestructiveProperty);
    public static void SetIsDestructive(DependencyObject element, bool value) => element.SetValue(IsDestructiveProperty, value);
}
