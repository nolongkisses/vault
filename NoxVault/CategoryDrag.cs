using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NoxVault;

internal static class CategoryDrag
{
    const string Format = "NoxVault.CategoryOrder";
    static ScrollViewer? ScrollParent(DependencyObject item)
    {
        DependencyObject? parent = item;
        while (parent != null && parent is not ScrollViewer) parent = VisualTreeHelper.GetParent(parent);
        return parent as ScrollViewer;
    }
    internal static void Attach(Button button, string name, Action<string, string, bool> move)
    {
        Point start = default;
        bool armed = false, after = false;
        DateTime lastScroll = DateTime.MinValue;
        DragVisual? marker = null;
        AdornerLayer? markerLayer = null;
        void HideMarker()
        {
            if (marker != null) markerLayer?.Remove(marker);
            marker = null; markerLayer = null;
        }
        button.AllowDrop = true;
        button.PreviewMouseLeftButtonDown += (_, e) => { start = e.GetPosition(button); armed = true; };
        button.PreviewMouseLeftButtonUp += (_, _) => armed = false;
        button.PreviewMouseMove += (_, e) =>
        {
            if (!armed || e.LeftButton != MouseButtonState.Pressed) return;
            var point = e.GetPosition(button);
            if (Math.Abs(point.X - start.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(point.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            armed = false; button.ReleaseMouseCapture(); Keyboard.ClearFocus();
            FrameworkElement host = ScrollParent(button) ?? (FrameworkElement)button;
            var layer = AdornerLayer.GetAdornerLayer(host);
            var ghost = new DragVisual(host, name);
            void PositionGhost()
            {
                var p = Mouse.GetPosition(host);
                ghost.Visibility = p.X >= 0 && p.X <= host.ActualWidth && p.Y >= 0 && p.Y <= host.ActualHeight
                    ? Visibility.Visible : Visibility.Hidden;
                ghost.MoveTo(Math.Clamp(p.Y + 12, 0, Math.Max(0, host.ActualHeight - 34)));
                ghost.InvalidateVisual();
            }
            PositionGhost(); layer?.Add(ghost);
            EventHandler rendering = (_, _) => { PositionGhost(); ghost.Advance(); };
            CompositionTarget.Rendering += rendering;
            ghost.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 0.96, TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation ? 100 : 0)));
            button.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.45, TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation ? 100 : 0)));
            GiveFeedbackEventHandler feedback = (_, _) => PositionGhost();
            button.GiveFeedback += feedback;
            try { DragDrop.DoDragDrop(button, new DataObject(Format, name), DragDropEffects.Move); }
            finally
            {
                CompositionTarget.Rendering -= rendering;
                layer?.Remove(ghost); HideMarker(); button.GiveFeedback -= feedback;
                button.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation ? 140 : 0)));
            }
            e.Handled = true;
        };
        button.DragOver += (_, e) =>
        {
            if (e.Data.GetDataPresent(AccountDrag.Format, false)) return;
            e.Handled = true;
            if (!e.Data.GetDataPresent(Format) || e.Data.GetData(Format) is not string source || source == name)
            { e.Effects = DragDropEffects.None; HideMarker(); return; }
            e.Effects = DragDropEffects.Move;
            var scroll = ScrollParent(button);
            if (scroll != null && DateTime.UtcNow - lastScroll > TimeSpan.FromMilliseconds(60))
            {
                double y = e.GetPosition(scroll).Y;
                if (y < 28) { scroll.ScrollToVerticalOffset(scroll.VerticalOffset - 14); lastScroll = DateTime.UtcNow; }
                else if (y > scroll.ActualHeight - 28) { scroll.ScrollToVerticalOffset(scroll.VerticalOffset + 14); lastScroll = DateTime.UtcNow; }
            }
            after = e.GetPosition(button).Y >= button.ActualHeight / 2;
            if (marker == null)
            {
                markerLayer = AdornerLayer.GetAdornerLayer(button);
                marker = new DragVisual(button, null); markerLayer?.Add(marker);
                marker.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation ? 90 : 0)));
            }
            marker.Y = after ? button.ActualHeight - 3 : 1;
            marker.InvalidateVisual();
        };
        button.DragLeave += (_, _) => HideMarker();
        button.Drop += (_, e) =>
        {
            if (e.Data.GetDataPresent(AccountDrag.Format, false)) return;
            HideMarker(); e.Handled = true;
            if (e.Data.GetDataPresent(Format) && e.Data.GetData(Format) is string source && source != name)
                move(source, name, e.GetPosition(button).Y >= button.ActualHeight / 2);
        };
        button.Unloaded += (_, _) => HideMarker();
    }
    sealed class DragVisual : Adorner
    {
        readonly string? label;
        public double Y { get; set; }
        double targetY; bool positioned;
        readonly System.Diagnostics.Stopwatch motion = System.Diagnostics.Stopwatch.StartNew();
        public void MoveTo(double value)
        {
            targetY = value;
            if (!positioned || !SystemParameters.ClientAreaAnimation) { Y = value; positioned = true; }
        }
        public void Advance()
        {
            double elapsed = motion.Elapsed.TotalSeconds; motion.Restart();
            Y += (targetY - Y) * (1 - Math.Exp(-elapsed / 0.035));
            InvalidateVisual();
        }
        public DragVisual(UIElement element, string? label) : base(element)
        { this.label = label; IsHitTestVisible = false; }
        protected override void OnRender(DrawingContext dc)
        {
            var size = AdornedElement.RenderSize;
            dc.PushClip(new RectangleGeometry(new Rect(size)));
            if (label == null)
                dc.DrawRoundedRectangle(Ui.Brush("#657080"), null, new Rect(10, Y, Math.Max(0, size.Width - 20), 1), 0.5, 0.5);
            else
            {
                var width = Math.Max(0, size.Width - 16);
                dc.DrawRoundedRectangle(Ui.Brush("#171B21"), new Pen(Ui.Brush("#2A3039"), 1), new Rect(8, Y, width, 32), 5, 5);
                var text = new FormattedText(label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 12, Ui.Brush("#D7DBE0"), VisualTreeHelper.GetDpi(this).PixelsPerDip)
                { MaxTextWidth = Math.Max(1, width - 24), MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
                dc.DrawText(text, new Point(20, Y + (32 - text.Height) / 2));
            }
            dc.Pop();
        }
    }
}
