// Adapted from:
// https://gist.github.com/angularsen/90040fb174f71c5ab3ad
// https://web.archive.org/web/20120711175633/http://blogs.microsoft.co.il/blogs/eladkatz/archive/2011/05/29/what-is-the-easiest-way-to-set-spacing-between-items-in-stackpanel.aspx
// https://stackoverflow.com/a/6170824/242826
using System.Windows;
using System.Windows.Controls;
using JetBrains.Annotations;

namespace HocrEditor.Helpers;

public class MarginSetter
{
    private static Thickness GetLastItemMargin(DependencyObject obj)
    {
        return (Thickness) obj.GetValue(LastItemMarginProperty);
    }

    [UsedImplicitly]
    public static Thickness GetMargin(DependencyObject obj)
    {
        return (Thickness) obj.GetValue(MarginProperty);
    }

    [UsedImplicitly]
    public static void SetLastItemMargin(DependencyObject obj, Thickness value)
    {
        obj.SetValue(LastItemMarginProperty, value);
    }

    [UsedImplicitly]
    public static void SetMargin(DependencyObject obj, Thickness value)
    {
        obj.SetValue(MarginProperty, value);

        Update(obj);
    }

    private static void Update(DependencyObject obj)
    {
        if (obj is not Panel panel)
        {
            return;
        }

        UpdateMargins(panel);
    }

    // Using a DependencyProperty as the backing store for Margin.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty MarginProperty =
        DependencyProperty.RegisterAttached(
            "Margin",
            typeof(Thickness),
            typeof(MarginSetter),
            new UIPropertyMetadata(new Thickness(), MarginChangedCallback)
        );

    public static readonly DependencyProperty LastItemMarginProperty =
        DependencyProperty.RegisterAttached("LastItemMargin", typeof (Thickness), typeof (MarginSetter),
            new UIPropertyMetadata(new Thickness(), MarginChangedCallback));

    private static void MarginChangedCallback(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Make sure this is put on a panel
        if (sender is not Panel panel)
        {
            return;
        }

        // Avoid duplicate registrations
        panel.Loaded -= OnPanelLoaded;
        panel.Loaded += OnPanelLoaded;

        if (panel.IsLoaded)
        {
            OnPanelLoaded(panel, new RoutedEventArgs());
        }
    }

    private static void OnPanelLoaded(object sender, RoutedEventArgs e)
    {
        var panel = (Panel)sender;

        UpdateMargins(panel);
    }

    private static void UpdateMargins(Panel panel)
    {
        // Go over the children and set margin for them:
        for (var i = 0; i < panel.Children.Count; i++)
        {
            var child = panel.Children[i];
            if (child is not FrameworkElement fe)
            {
                continue;
            }

            var isLastItem = i == panel.Children.Count - 1;
            fe.Margin = isLastItem ? GetLastItemMargin(panel) : GetMargin(panel);
        }
    }
}
