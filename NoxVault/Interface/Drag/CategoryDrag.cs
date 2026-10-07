using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NoxVault;

// Reorders categories by drag and drop: a ghost follows the pointer, a line marks where the category will land.
internal static class CategoryDrag
{
    const string Format = "NoxVault.CategoryOrder";

    static ScrollViewer? ScrollParent(DependencyObject item)
    {
        DependencyObject? parent = item;
        while (parent != null && parent is not ScrollViewer) parent = VisualTreeHelper.GetParent(parent);
        return parent as ScrollViewer;
    }

    static TimeSpan Fade(int milliseconds) => TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation ? milliseconds : 0);

    internal static void Attach(Button button, string name, Action<string, string, bool> move) => new Row(button, name, move);

    sealed class Row
    {
        readonly Button button;
        readonly string name;
        readonly Action<string, string, bool> move;
        Point start;
        bool armed;
        DateTime lastScroll = DateTime.MinValue;
        DragVisual? marker;
        AdornerLayer? markerLayer;

        internal Row(Button button, string name, Action<string, string, bool> move)
        {
            (this.button, this.name, this.move) = (button, name, move);
            button.AllowDrop = true;
            button.PreviewMouseLeftButtonDown += (_, e) => { start = e.GetPosition(button); armed = true; };
            button.PreviewMouseLeftButtonUp += (_, _) => armed = false;
            button.PreviewMouseMove += OnMove;
            button.DragOver += OnDragOver;
            button.DragLeave += (_, _) => HideMarker();
            button.Drop += OnDrop;
            button.Unloaded += (_, _) => HideMarker();
        }

        void HideMarker()
        {
            if (marker != null) markerLayer?.Remove(marker);
            marker = null;
            markerLayer = null;
        }

        void OnMove(object sender, MouseEventArgs e)
        {
            if (!armed || e.LeftButton != MouseButtonState.Pressed) return;
            var point = e.GetPosition(button);
            if (Math.Abs(point.X - start.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(point.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            armed = false;
            button.ReleaseMouseCapture();
            Keyboard.ClearFocus();
            Drag();
            e.Handled = true;
        }

        void Drag()
        {
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
            PositionGhost();
            layer?.Add(ghost);
            EventHandler rendering = (_, _) => { PositionGhost(); ghost.Advance(); };
            CompositionTarget.Rendering += rendering;
            ghost.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 0.96, Fade(100)));
            button.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.45, Fade(100)));
            GiveFeedbackEventHandler feedback = (_, _) => PositionGhost();
            button.GiveFeedback += feedback;
            try { DragDrop.DoDragDrop(button, new DataObject(Format, name), DragDropEffects.Move); }
            finally
            {
                CompositionTarget.Rendering -= rendering;
                layer?.Remove(ghost);
                HideMarker();
                button.GiveFeedback -= feedback;
                button.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, Fade(140)));
            }
        }

        void OnDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(AccountDrag.Format, false)) return;
            e.Handled = true;
            if (!e.Data.GetDataPresent(Format) || e.Data.GetData(Format) is not string source || source == name)
            {
                e.Effects = DragDropEffects.None;
                HideMarker();
                return;
            }
            e.Effects = DragDropEffects.Move;
            AutoScroll(e);
            bool after = e.GetPosition(button).Y >= button.ActualHeight / 2;
            if (marker == null)
            {
                markerLayer = AdornerLayer.GetAdornerLayer(button);
                marker = new DragVisual(button, null);
                markerLayer?.Add(marker);
                marker.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, Fade(90)));
            }
            marker.Y = after ? button.ActualHeight - 3 : 1;
            marker.InvalidateVisual();
        }

        // Near the top or bottom edge the list scrolls, at most every 60 ms.
        void AutoScroll(DragEventArgs e)
        {
            var scroll = ScrollParent(button);
            if (scroll == null || DateTime.UtcNow - lastScroll <= TimeSpan.FromMilliseconds(60)) return;
            double y = e.GetPosition(scroll).Y;
            double step = y < 28 ? -14 : y > scroll.ActualHeight - 28 ? 14 : 0;
            if (step == 0) return;
            scroll.ScrollToVerticalOffset(scroll.VerticalOffset + step);
            lastScroll = DateTime.UtcNow;
        }

        void OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(AccountDrag.Format, false)) return;
            HideMarker();
            e.Handled = true;
            if (e.Data.GetDataPresent(Format) && e.Data.GetData(Format) is string source && source != name)
                move(source, name, e.GetPosition(button).Y >= button.ActualHeight / 2);
        }
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
                dc.DrawRoundedRectangle(Theme.Brush(Theme.Sub), null, new Rect(10, Y, Math.Max(0, size.Width - 20), 2), 1, 1);
            else
            {
                var width = Math.Max(0, size.Width - 16);
                dc.DrawRoundedRectangle(Theme.Brush(Theme.Raise2), null, new Rect(8, Y, width, Theme.ControlHeight),
                    Theme.ControlRadius, Theme.ControlRadius);
                var text = new FormattedText(label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    new Typeface(Theme.Font.Source), Theme.Size(TextRole.Small), Theme.Brush(Theme.Fg),
                        VisualTreeHelper.GetDpi(this).PixelsPerDip)
                { MaxTextWidth = Math.Max(1, width - 24), MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
                dc.DrawText(text, new Point(20, Y + (Theme.ControlHeight - text.Height) / 2));
            }
            dc.Pop();
        }
    }
}
