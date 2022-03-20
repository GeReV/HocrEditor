// Adapted from: https://gist.github.com/SlyZ/ca7b03931412115cc5fb1416180ad1b4

using System.Windows;

namespace HocrEditor.Core;

/// <summary>
/// Command to command binding can be used to redirect one command to another.
///
/// The primary use case for it is a redirection of a RoutedCommand to a different ICommand implementation
/// (e.g. DelegateCommand, RelayCommand, etc.).
/// Unlike the standard WPF CommandBinding, ForwardCommandBinding is a DependencyObject with DependencyProperties,
/// meaning that the source and target commands can be easily bound to in Xaml, and the commands can be implemented in the View Model.
/// </summary>
/// <example>
/// <![CDATA[
/// <Window xmlns:core="clr-namespace:HocrEditor.Core" ...>
///     <core:ForwardingCommand.Bindings>
///         <st:ForwardingCommandBinding SourceCommand="Save" TargetCommand="{Binding MySaveDelegateCommand}" />
///     </st:ForwardingCommand.Bindings>
/// </Window>
/// ]]>
/// </example>
public static class ForwardingCommand
{
    /// <summary>
    /// Identifies the CommandToCommand.Bindings attached property.
    /// </summary>
    public static readonly DependencyProperty BindingsProperty =
        DependencyProperty.RegisterAttached(
            "ForwardingCommandBindingsInternal",
            typeof(ForwardingCommandBindingCollection),
            typeof(ForwardingCommand),
            new UIPropertyMetadata(null)
        );

    /// <summary>
    /// Gets the value of the CommandToCommand.Bindings attached property from a given System.Windows.UIElement.
    /// </summary>
    /// <param name="uiElement">The element from which to read the property value.</param>
    /// <returns>The value of the CommandToCommand.Bindings attached property.</returns>
    public static ForwardingCommandBindingCollection GetBindings(UIElement uiElement)
    {
        var bindings = (ForwardingCommandBindingCollection?)uiElement.GetValue(BindingsProperty);
        if (bindings == null)
        {
            bindings = new ForwardingCommandBindingCollection(uiElement);
            uiElement.SetValue(BindingsProperty, bindings);
        }

        return bindings;
    }

    /// <summary>
    /// Sets the value of the CommandToCommand.Bindings attached property to a given System.Windows.UIElement.
    /// </summary>
    /// <param name="uiElement">The element on which to set the attached property.</param>
    /// <param name="value">The property value to set.</param>
    public static void SetBindings(UIElement uiElement, ForwardingCommandBindingCollection value)
    {
        var bindings = (ForwardingCommandBindingCollection?)uiElement.GetValue(BindingsProperty);

        bindings?.Unhook();

        uiElement.SetValue(BindingsProperty, value);
    }
}
