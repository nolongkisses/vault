using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NoxVault;

internal static class AccountDrag
{
    internal const string Format = "NoxVault.AccountMove";
    static (string Token, Guid Id)? active;
    internal static Guid? Read(IDataObject data) => active is { } drag &&
        data.GetDataPresent(Format, false) && Equals(data.GetData(Format, false), drag.Token) ? drag.Id : null;

    internal static void Source(Button button, Guid id, string title, Func<bool> available)
    {
        Point start = default; bool armed = false;
        button.ToolTip = "In eine Kategorie ziehen zum Verschieben";
        button.PreviewMouseLeftButtonDown += (_, e) => { start = e.GetPosition(button); armed = true; };
        button.PreviewMouseLeftButtonUp += (_, _) => armed = false;
        button.PreviewMouseMove += (_, e) =>
        {
            if (!armed || e.LeftButton != MouseButtonState.Pressed || !available()) return;
            var delta = e.GetPosition(button) - start;
            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            armed = false; button.ReleaseMouseCapture(); Keyboard.ClearFocus();
            if (Window.GetWindow(button)?.Content is not FrameworkElement host) return;
            var layer = AdornerLayer.GetAdornerLayer(host);
            var ghost = new Preview(host, title);
            active = (Guid.NewGuid().ToString("N"), id);
            EventHandler render = (_, _) =>
            {
                if (!available()) { active = null; ghost.Visibility = Visibility.Collapsed; return; }
                ghost.Follow(Mouse.GetPosition(host));
            };
            QueryContinueDragEventHandler cancel = (_, args) =>
            { if (!available() || args.EscapePressed) { args.Action = DragAction.Cancel; args.Handled = true; } };
            try
            {
                ghost.Follow(Mouse.GetPosition(host)); layer?.Add(ghost);
                CompositionTarget.Rendering += render; button.QueryContinueDrag += cancel;
                Fade(button, 0.45); Fade(ghost, 0.96, 0);
                DragDrop.DoDragDrop(button, new DataObject(Format, active.Value.Token), DragDropEffects.Move);
            }
            finally
            {
                active = null; CompositionTarget.Rendering -= render; button.QueryContinueDrag -= cancel;
                layer?.Remove(ghost); Fade(button, 1);
            }
            e.Handled = true;
        };
    }

    internal static void Target(Button button, Func<Guid, bool> accepts, Action<Guid> move)
    {
        button.AllowDrop = true;
        void Clear() => Visuals.SetIsDropTarget(button, false);
        button.DragOver += (_, e) =>
        {
            if (!e.Data.GetDataPresent(Format, false)) return;
            e.Handled = true;
            bool valid = Read(e.Data) is Guid id && accepts(id);
            e.Effects = valid ? DragDropEffects.Move : DragDropEffects.None;
            Visuals.SetIsDropTarget(button, valid);
        };
        button.DragLeave += (_, _) => Clear();
        button.Unloaded += (_, _) => Clear();
        button.Drop += (_, e) =>
        {
            if (!e.Data.GetDataPresent(Format, false)) return;
            Clear(); e.Handled = true; e.Effects = DragDropEffects.None;
            if (Read(e.Data) is Guid id && accepts(id)) { move(id); e.Effects = DragDropEffects.Move; }
        };
    }

    static void Fade(UIElement element, double to, double? from = null) =>
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
        { From = from, To = to, Duration = TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation ? 120 : 0) });

    sealed class Preview : Adorner
    {
        readonly string title;
        readonly Stopwatch clock = Stopwatch.StartNew();
        Point position; bool positioned;
        public Preview(UIElement host, string title) : base(host) { this.title = title; IsHitTestVisible = false; }
        public void Follow(Point cursor)
        {
            var size = AdornedElement.RenderSize;
            Visibility = cursor.X >= 0 && cursor.Y >= 0 && cursor.X <= size.Width
                && cursor.Y <= size.Height ? Visibility.Visible : Visibility.Hidden;
            var target = new Point(Math.Clamp(cursor.X + 16, 0, Math.Max(0, size.Width - 204)),
                Math.Clamp(cursor.Y + 14, 0, Math.Max(0, size.Height - 50)));
            double factor = 1 - Math.Exp(-clock.Elapsed.TotalSeconds / 0.035); clock.Restart();
            position = !positioned || !SystemParameters.ClientAreaAnimation ? target : position + (target - position) * factor;
            positioned = true; InvalidateVisual();
        }
        protected override void OnRender(DrawingContext dc)
        {
            dc.PushClip(new RectangleGeometry(new Rect(AdornedElement.RenderSize)));
            dc.DrawRoundedRectangle(Theme.Brush(Theme.Raise2), null, new Rect(position, new Size(200, 46)), Theme.Radius, Theme.Radius);
            void Label(string value, double y, double size, string color)
            {
                var text = new FormattedText(value, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    new Typeface(Theme.Font.Source), size, Theme.Brush(color), VisualTreeHelper.GetDpi(this).PixelsPerDip)
                { MaxTextWidth = 176, MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
                dc.DrawText(text, new Point(position.X + 12, position.Y + y));
            }
            Label(title, 7, Theme.Size(TextRole.Small), Theme.Fg); Label("In Kategorie verschieben", 25, Theme.Size(TextRole.Micro),
                Theme.Sub);
            dc.Pop();
        }
    }
}
