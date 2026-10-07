using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace NoxVault;

// Rows with one selection pill behind them; the pill glides to the selected row instead of jumping.
internal sealed class NavigationList : Grid
{
    readonly Border pill = new()
    {
        VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false, Visibility = Visibility.Hidden,
        CornerRadius = new CornerRadius(Theme.ControlRadius), RenderTransform = new TranslateTransform(),
    };
    readonly StackPanel rows = new();
    FrameworkElement? selected;
    double? shownTop;

    internal NavigationList(string tone = Theme.Raise2)
    {
        pill.SetResourceReference(Border.BackgroundProperty, tone);
        Children.Add(pill);
        Children.Add(rows);
    }

    internal UIElementCollection Rows => rows.Children;

    internal void Select(FrameworkElement? row)
    {
        selected = row;
        foreach (UIElement child in rows.Children) Visuals.SetIsSelected(child, ReferenceEquals(child, row));
        InvalidateArrange();
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var result = base.ArrangeOverride(arrangeSize);
        if (selected == null || !rows.Children.Contains(selected) || selected.ActualHeight <= 0)
        {
            pill.Visibility = Visibility.Hidden;
            shownTop = null;
            return result;
        }
        // The layout slot ignores a row's own render transform, so reordering animations do not drag the pill along.
        var slot = LayoutInformation.GetLayoutSlot(selected);
        double top = slot.Top + selected.Margin.Top;
        pill.Height = selected.ActualHeight;
        pill.Margin = new Thickness(selected.Margin.Left, 0, selected.Margin.Right, 0);
        pill.Visibility = Visibility.Visible;
        var transform = (TranslateTransform)pill.RenderTransform;
        if (shownTop == null) transform.Y = top;
        else if (shownTop != top) Motion.Animate(transform, TranslateTransform.YProperty, top, Motion.Glide);
        shownTop = top;
        return result;
    }
}
