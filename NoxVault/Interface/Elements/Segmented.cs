using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace NoxVault;

// Equal-width options in a raise1 track; the thumb, one step brighter, glides to the chosen option.
internal sealed class Segmented : Border
{
    readonly Border thumb = new()
    {
        HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(Theme.SmallRadius),
        RenderTransform = new TranslateTransform(), IsHitTestVisible = false,
    };
    readonly UniformGrid options = new() { Rows = 1 };
    double? shownX;

    internal Segmented(string name, params string[] labels)
    {
        Height = Theme.ControlHeight;
        Padding = new Thickness(Theme.S1);
        CornerRadius = new CornerRadius(Theme.ControlRadius);
        SetResourceReference(BackgroundProperty, Theme.Raise1);
        thumb.SetResourceReference(Border.BackgroundProperty, Theme.Raise3);
        AutomationProperties.SetName(this, name);
        for (int i = 0; i < labels.Length; i++) options.Children.Add(Option(labels[i], i));
        var layers = new Grid();
        layers.Children.Add(thumb);
        layers.Children.Add(options);
        Child = layers;
        Select(0, notify: false);
    }

    internal int SelectedIndex { get; private set; }
    internal event Action<int>? Changed;

    Button Option(string label, int index)
    {
        var button = Elements.Ghost(label, () => Select(index, notify: true));
        button.Height = Theme.SmallControl;
        button.Padding = new Thickness(Theme.S5, 0, Theme.S5, 0);
        button.FontSize = Theme.Size(TextRole.Small);
        Visuals.SetHover(button, Brushes.Transparent);
        return button;
    }

    internal void Select(int index, bool notify)
    {
        SelectedIndex = index;
        for (int i = 0; i < options.Children.Count; i++)
        {
            var button = (Button)options.Children[i];
            button.SetResourceReference(Control.ForegroundProperty, i == index ? Theme.Fg : Theme.Sub);
            AutomationProperties.SetItemStatus(button, i == index ? "Ausgewählt" : "");
        }
        InvalidateArrange();
        if (notify) Changed?.Invoke(index);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var result = base.ArrangeOverride(arrangeSize);
        if (options.Children.Count == 0 || options.ActualWidth <= 0) return result;
        double cell = options.ActualWidth / options.Children.Count;
        thumb.Width = cell;
        var transform = (TranslateTransform)thumb.RenderTransform;
        double x = cell * SelectedIndex;
        if (shownX == null) transform.X = x;
        else if (shownX != x) Motion.Animate(transform, TranslateTransform.XProperty, x, Motion.Glide);
        shownX = x;
        return result;
    }
}
