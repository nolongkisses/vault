using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using static NoxVault.Elements;

namespace NoxVault;

// The settings layout: page list on the left, the page on the right; below 600 px the list becomes a dropdown.
internal sealed class SettingsView : Grid
{
    const double NavigationWidth = 176;
    const double CompactBelow = 600;
    const double MaxPageHeight = 540;
    readonly NavigationList navigation = new();
    readonly ComboBox compactNavigation = new() { Margin = new Thickness(0, 0, 0, Theme.S6) };
    readonly ScrollViewer scroll = new();
    readonly List<(string Title, StackPanel Page, Button Button)> pages = new();

    internal SettingsView()
    {
        MaxHeight = MaxPageHeight;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition());
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(NavigationWidth) });
        ColumnDefinitions.Add(new ColumnDefinition());
        navigation.Margin = new Thickness(0, 0, Theme.S7, 0);
        SetRow(navigation, 1);
        SetRow(scroll, 1);
        SetColumn(scroll, 1);
        SetColumnSpan(compactNavigation, 2);
        Children.Add(navigation);
        Children.Add(compactNavigation);
        Children.Add(scroll);
        compactNavigation.Visibility = Visibility.Collapsed;
        AutomationProperties.SetName(compactNavigation, "Einstellungsbereich");
        compactNavigation.SelectionChanged += (_,
            _) =>
        { if (compactNavigation.SelectedIndex >= 0) SelectPage(compactNavigation.SelectedIndex); };
        scroll.SetResourceReference(StyleProperty, "GutterScrollViewer");
    }

    internal int SelectedPage { get; private set; } = -1;
    internal int PageCount => pages.Count;
    internal bool IsCompact { get; private set; }
    internal Action? PageChanged { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        IsCompact = availableSize.Width < CompactBelow;
        navigation.Visibility = IsCompact ? Visibility.Collapsed : Visibility.Visible;
        compactNavigation.Visibility = IsCompact ? Visibility.Visible : Visibility.Collapsed;
        ColumnDefinitions[0].Width = new GridLength(IsCompact ? 0 : NavigationWidth);
        var height = Math.Min(MaxPageHeight, availableSize.Height);
        var measured = base.MeasureOverride(new Size(availableSize.Width, height));
        return new Size(measured.Width, height);
    }

    internal StackPanel AddPage(string title, string description)
    {
        var page = new StackPanel();
        page.Children.Add(Text(title, TextRole.Head, bold: true));
        var subtitle = Text(description, TextRole.Small, Theme.Sub);
        subtitle.Margin = new Thickness(0, Theme.S2, 0, Theme.S7);
        page.Children.Add(subtitle);
        int index = pages.Count;
        var button = Elements.Button(title, () => SelectPage(index), "NavRow");
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        pages.Add((title, page, button));
        navigation.Rows.Add(button);
        compactNavigation.Items.Add(title);
        if (SelectedPage < 0) SelectPage(0);
        return page;
    }

    internal void SelectPage(int index)
    {
        if (index < 0 || index >= pages.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (SelectedPage == index) return;
        SelectedPage = index;
        scroll.Content = pages[index].Page;
        scroll.ScrollToTop();
        navigation.Select(pages[index].Button);
        foreach (var item in pages) AutomationProperties.SetItemStatus(item.Button, item.Page == pages[index].Page ? "Ausgewählt" : "");
        compactNavigation.SelectedIndex = index;
        PageChanged?.Invoke();
    }

    internal static StackPanel Section(StackPanel page, string title, string description)
    {
        var section = new StackPanel { Margin = new Thickness(0, 0, 0, Theme.S7) };
        section.Children.Add(Text(title, TextRole.Body, bold: true));
        if (description.Length > 0)
        {
            var hint = Text(description, TextRole.Small, Theme.Sub);
            hint.Margin = new Thickness(0, Theme.S1, 0, 0);
            section.Children.Add(hint);
            section.Tag = hint;
        }
        page.Children.Add(section);
        return section;
    }

    // A section whose only control is one dropdown or button shows it to the right of its text.
    internal void ArrangeOptions()
    {
        foreach (var page in pages)
            foreach (var section in page.Page.Children.OfType<StackPanel>().Where(s => s.Tag is TextBlock))
            {
                var control = section.Children.OfType<FrameworkElement>()
                    .FirstOrDefault(c => c is ComboBox or System.Windows.Controls.Button or Segmented or Switch);
                if (control == null || section.Children.OfType<FrameworkElement>().Any(c => c is Panel)) continue;
                var labels = new StackPanel();
                for (int i = 0; i < 2; i++)
                {
                    var label = section.Children[0];
                    section.Children.RemoveAt(0);
                    labels.Children.Add(label);
                }
                section.Children.Remove(control);
                section.Children.Insert(0, new OptionRow(labels, control));
            }
    }

    internal static TextBlock Feedback(Panel section)
    {
        var feedback = Text("", TextRole.Small, Theme.Sub);
        feedback.Margin = new Thickness(0, Theme.S4, 0, 0);
        var style = new Style(typeof(TextBlock));
        var empty = new DataTrigger
        {
            Binding = new System.Windows.Data.Binding("Text") { RelativeSource = System.Windows.Data.RelativeSource.Self },
            Value = "",
        };
        empty.Setters.Add(new Setter(VisibilityProperty, Visibility.Collapsed));
        style.Triggers.Add(empty);
        feedback.Style = style;
        AutomationProperties.SetLiveSetting(feedback, AutomationLiveSetting.Polite);
        section.Children.Add(feedback);
        return feedback;
    }

    // Text left, control right; stacks below 440 px so nothing is cut off at large text scaling.
    sealed class OptionRow : Grid
    {
        readonly FrameworkElement labels, choice;

        internal OptionRow(FrameworkElement labels, FrameworkElement choice)
        {
            this.labels = labels;
            this.choice = choice;
            ColumnDefinitions.Add(new ColumnDefinition());
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            choice.VerticalAlignment = VerticalAlignment.Center;
            Children.Add(labels);
            Children.Add(choice);
        }

        protected override Size MeasureOverride(Size available)
        {
            bool stacked = available.Width < 440;
            SetColumnSpan(labels, stacked ? 2 : 1);
            SetRow(choice, stacked ? 1 : 0);
            SetColumn(choice, stacked ? 0 : 1);
            choice.HorizontalAlignment = stacked ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            choice.Margin = stacked ? new Thickness(0, Theme.S5, 0, 0) : new Thickness(Theme.S6, 0, 0, 0);
            return base.MeasureOverride(available);
        }
    }
}
