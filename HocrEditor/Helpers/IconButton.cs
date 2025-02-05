using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using HocrEditor.Core;
using JetBrains.Annotations;

namespace HocrEditor.Helpers;

public class IconButton
{
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.RegisterAttached(
            "Source",
            typeof(ImageSource),
            typeof(IconButton),
            new UIPropertyMetadata(
                defaultValue: null,
                OnChangeCallback
            )
        );

    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.RegisterAttached(
            "Orientation",
            typeof(Orientation),
            typeof(IconButton),
            new UIPropertyMetadata(
                Orientation.Horizontal,
                OnChangeCallback
            )
        );

    public static readonly DependencyProperty PositionProperty =
        DependencyProperty.RegisterAttached(
            "Position",
            typeof(IconPosition),
            typeof(IconButton),
            new UIPropertyMetadata(
                IconPosition.Before,
                OnChangeCallback
            )
        );

    public static readonly DependencyProperty SpacingProperty =
        DependencyProperty.RegisterAttached(
            "Spacing",
            typeof(double),
            typeof(IconButton),
            new UIPropertyMetadata(
                2.0,
                OnChangeCallback
            )
        );

    public static ImageSource GetSource(DependencyObject obj)
    {
        return (ImageSource)obj.GetValue(SourceProperty);
    }

    [UsedImplicitly]
    public static void SetSource(DependencyObject obj, ImageSource value)
    {
        obj.SetValue(SourceProperty, value);
    }

    public static Orientation GetOrientation(DependencyObject obj)
    {
        return (Orientation)obj.GetValue(OrientationProperty);
    }

    [UsedImplicitly]
    public static void SetOrientation(DependencyObject obj, Orientation value)
    {
        obj.SetValue(OrientationProperty, value);
    }

    public static IconPosition GetPosition(DependencyObject obj)
    {
        return (IconPosition)obj.GetValue(PositionProperty);
    }

    [UsedImplicitly]
    public static void SetPosition(DependencyObject obj, IconPosition value)
    {
        obj.SetValue(PositionProperty, value);
    }

    public static double GetSpacing(DependencyObject obj)
    {
        return (double)obj.GetValue(SpacingProperty);
    }

    [UsedImplicitly]
    public static void SetSpacing(DependencyObject obj, double value)
    {
        obj.SetValue(SpacingProperty, value);
    }

    private static void OnChangeCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ButtonBase button)
        {
            throw new InvalidOperationException($"{nameof(IconButton)} properties can only be applied to buttons");
        }

        button.Loaded -= ButtonOnLoaded;
        button.Loaded += ButtonOnLoaded;

        if (button.IsLoaded)
        {
            ButtonOnLoaded(d, EventArgs.Empty);
        }
    }

    private static void ButtonOnLoaded(object? sender, EventArgs e)
    {
        ArgumentNullException.ThrowIfNull(sender);

        var button = (ButtonBase)sender;

        var border = button.FindVisualChild<Border>();

        if (border is null)
        {
            return;
        }

        var contentPresenter = border.FindVisualChild<ContentPresenter>();

        if (contentPresenter is null)
        {
            throw new InvalidOperationException("Unexpected state in button");
        }

        var spacing = GetSpacing(button);
        var orientation = GetOrientation(button);

        var parent = VisualTreeHelper.GetParent(contentPresenter);

        var margin = orientation == Orientation.Horizontal ? new Thickness(0, 0, spacing, 0) : new Thickness(0, 0, 0, spacing);

        switch (parent)
        {
            case StackPanel stackPanel:
            {
                stackPanel.Orientation = orientation;

                var icon = stackPanel.FindImmediateVisualChild<Image>();

                Ensure.IsNotNull(icon);

                stackPanel.Children.Remove(icon);

                if (GetPosition(button) == IconPosition.Before)
                {
                    stackPanel.Children.Insert(0, icon);
                }
                else
                {
                    stackPanel.Children.Add(icon);
                }

                MarginSetter.SetMargin(stackPanel, margin);
                break;
            }
            case Decorator decorator:
            {
                decorator.Child = null;
                decorator.Child = BuildStackPanel(button, contentPresenter);

                MarginSetter.SetMargin(decorator.Child, margin);
                break;
            }
            case Panel panel:
            {
                var index = panel.Children.IndexOf(contentPresenter);

                panel.Children.Remove(contentPresenter);

                var child = BuildStackPanel(button, contentPresenter);

                panel.Children.Insert(index, child);

                MarginSetter.SetMargin(child, margin);
                break;
            }
        }


    }

    private static StackPanel BuildStackPanel(ButtonBase button, ContentPresenter contentPresenter)
    {
        var icon = new Image
        {
            Source = GetSource(button),
            Width = 16,
            Height = 16,
        };

        var stackPanel = new StackPanel
        {
            Orientation = GetOrientation(button),
        };

        if (GetPosition(button) == IconPosition.Before)
        {
            stackPanel.Children.Add(icon);
            stackPanel.Children.Add(contentPresenter);
        }
        else
        {
            stackPanel.Children.Add(contentPresenter);
            stackPanel.Children.Add(icon);
        }

        return stackPanel;
    }
}
