using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NoxVault;

// An on/off toggle whose knob settles with the spring instead of a fixed-duration tween.
internal sealed class Switch : CheckBox
{
    const double Travel = 18;
    TranslateTransform? knob;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        // Template-defined transforms are frozen; the knob gets its own so the spring can move it.
        if (GetTemplateChild("PART_Knob") is not FrameworkElement part) return;
        knob = new TranslateTransform(IsChecked == true ? Travel : 0, 0);
        part.RenderTransform = knob;
    }

    protected override void OnChecked(RoutedEventArgs e)
    {
        base.OnChecked(e);
        Move(Travel);
    }

    protected override void OnUnchecked(RoutedEventArgs e)
    {
        base.OnUnchecked(e);
        Move(0);
    }

    void Move(double x)
    {
        if (knob != null) Motion.Animate(knob, TranslateTransform.XProperty, x, Motion.Settle);
    }
}
