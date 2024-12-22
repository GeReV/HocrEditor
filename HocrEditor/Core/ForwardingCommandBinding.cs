// Adapted from: https://gist.github.com/SlyZ/ca7b03931412115cc5fb1416180ad1b4

using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace HocrEditor.Core;

/// <summary>
/// A command binding encapsulating a single command-to-command mapping.
/// Unlike <see cref="System.Windows.Input.CommandBinding"/>, this binding is a DependencyObject,
/// and hence can be bound to in Xaml.
/// </summary>
public sealed class ForwardingCommandBinding : Freezable
{
    /// <summary>
    /// A dummy command used as a placeholder when source command is not set.
    /// </summary>
    private static readonly RoutedCommand DummyCommand = new();

    /// <summary>
    /// Identifies the <see cref="SourceCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SourceCommandProperty =
        DependencyProperty.Register(
            nameof(SourceCommand),
            typeof(ICommand),
            typeof(ForwardingCommandBinding),
            new FrameworkPropertyMetadata(null, SourceCommandChanged)
        );

    /// <summary>
    /// Identifies the <see cref="TargetCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TargetCommandProperty =
        DependencyProperty.Register(
            nameof(TargetCommand),
            typeof(ICommand),
            typeof(ForwardingCommandBinding),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the <see cref="TargetCommandParameter"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TargetCommandParameterProperty =
        DependencyProperty.Register(
            nameof(TargetCommandParameter),
            typeof(object),
            typeof(ForwardingCommandBinding),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Initializes a new instance of the <see cref="ForwardingCommandBinding"/> class.
    /// </summary>
    public ForwardingCommandBinding()
    {
        CommandBinding = new CommandBinding(DummyCommand, OnSourceExecuted, OnSourceCanExecute);
    }

    /// <summary>
    /// Gets a <see cref="System.Windows.Input.CommandBinding"/> instance representing command-to-command mapping
    /// that will be added to the <see cref="System.Windows.UIElement.CommandBindings"/> collection to enable
    /// listening to routed commands.
    /// </summary>
    public CommandBinding CommandBinding { get; }

    /// <summary>
    /// Gets or sets the source command for the binding.
    /// </summary>
    public ICommand? SourceCommand
    {
        get => (ICommand?)GetValue(SourceCommandProperty);
        set => SetValue(SourceCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the target command for the binding.
    /// </summary>
    public ICommand? TargetCommand
    {
        get => (ICommand?)GetValue(TargetCommandProperty);
        set => SetValue(TargetCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the target command parameter for the binding.
    /// </summary>
    public object? TargetCommandParameter
    {
        get => GetValue(TargetCommandParameterProperty);
        set => SetValue(TargetCommandParameterProperty, value);
    }

    public event ExecutedRoutedEventHandler? Executed;

    private static void SourceCommandChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
    {
        var binding = (ForwardingCommandBinding)obj;
        binding.WritePreamble();
        binding.CommandBinding.Command = (ICommand?)e.NewValue ?? DummyCommand;
        binding.WritePostscript();
    }

    private void OnSourceCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        var targetCommand = TargetCommand;
        if (targetCommand == null)
        {
            return;
        }

        e.CanExecute = targetCommand.CanExecute(TargetCommandParameter ?? e.Parameter);
        e.Handled = true;
    }

    private void OnSourceExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        var targetCommand = TargetCommand;
        if (targetCommand == null)
        {
            return;
        }

        var parameter = TargetCommandParameter ?? e.Parameter;

        if (targetCommand is IAsyncRelayCommand asyncCommand)
        {
            e.Handled = true;

            asyncCommand.ExecuteAsync(parameter)
                .ContinueWith(_ => OnExecuted(e), TaskScheduler.FromCurrentSynchronizationContext());

            return;
        }

        targetCommand.Execute(parameter);
        e.Handled = true;

        OnExecuted(e);
    }

    protected override Freezable CreateInstanceCore() => new ForwardingCommandBinding();

    private void OnExecuted(ExecutedRoutedEventArgs e)
    {
        Executed?.Invoke(this, e);
    }
}
