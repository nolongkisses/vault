using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace NoxVault;

// A plain clipped layout surface: no themed ScrollViewer, content presenter,
// inner border or focus frame. Only the containing account card draws a border.
public sealed class DetailViewport : Grid
{
    readonly Surface surface;
    readonly ScrollBar bar;
    public DetailViewport(UIElement content)
    {
        ColumnDefinitions.Add(new ColumnDefinition());
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ClipToBounds = true;
        FocusVisualStyle = null;
        surface = new Surface { Child = content };
        bar = new ScrollBar
        {
            Orientation = Orientation.Vertical, Width = 7, Minimum = 0, Visibility = Visibility.Collapsed,
            Focusable = false, Margin = new Thickness(8, 0, 0, 0)
        };
        SetColumn(bar, 1);
        Children.Add(surface); Children.Add(bar);
        surface.Arranged += () =>
        {
            bar.Maximum = Math.Max(0, surface.Extent - surface.ViewportHeight);
            bar.ViewportSize = surface.ViewportHeight;
            bar.Visibility = bar.Maximum > 0.5 ? Visibility.Visible : Visibility.Collapsed;
            surface.Offset = Math.Min(surface.Offset, bar.Maximum);
        };
        bar.ValueChanged += (_, _) => { surface.Offset = bar.Value; surface.InvalidateArrange(); };
        PreviewMouseWheel += (_, e) => { bar.Value = Math.Clamp(bar.Value - e.Delta * 0.4, 0, bar.Maximum); e.Handled = true; };
        PreviewKeyDown += (_, e) =>
        {
            var delta = e.Key == Key.PageDown ? surface.ViewportHeight * 0.8 : e.Key == Key.PageUp ? -surface.ViewportHeight * 0.8 : 0;
            if (delta != 0) { bar.Value = Math.Clamp(bar.Value + delta, 0, bar.Maximum); e.Handled = true; }
        };
        surface.RequestBringIntoView += (_, e) =>
        {
            if (e.TargetObject is not FrameworkElement target || !surface.IsAncestorOf(target)) return;
            var bounds = target.TransformToAncestor(surface).TransformBounds(new Rect(target.RenderSize));
            if (bounds.Top < 0) bar.Value = Math.Max(0, bar.Value + bounds.Top);
            else if (bounds.Bottom > surface.ViewportHeight) bar.Value = Math.Min(bar.Maximum,
                bar.Value + bounds.Bottom - surface.ViewportHeight);
            e.Handled = true;
        };
    }
    sealed class Surface : Decorator
    {
        public double Offset { get; set; }
        public double Extent { get; private set; }
        public double ViewportHeight { get; private set; }
        public event Action? Arranged;
        public Surface() { ClipToBounds = true; FocusVisualStyle = null; }
        protected override Size MeasureOverride(Size available)
        {
            Child?.Measure(new Size(available.Width, double.PositiveInfinity));
            Extent = Child?.DesiredSize.Height ?? 0;
            return new Size(Child?.DesiredSize.Width
                ?? 0, double.IsInfinity(available.Height) ? Extent : Math.Min(Extent, available.Height));
        }
        protected override Size ArrangeOverride(Size size)
        {
            ViewportHeight = size.Height;
            Offset = Math.Clamp(Offset, 0, Math.Max(0, Extent - size.Height));
            Child?.Arrange(new Rect(0, -Offset, size.Width, Math.Max(size.Height, Extent)));
            Arranged?.Invoke();
            return size;
        }
    }
}
