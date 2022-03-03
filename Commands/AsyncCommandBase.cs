using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Toolkit.Mvvm.Input;
using PropertyChanged;

namespace HocrEditor.Commands;

internal static class AsyncCommandBaseEventArgs
{
    /// <summary>
    /// The cached <see cref="PropertyChangedEventArgs"/> for <see cref="AsyncCommandBase.CanBeCanceled"/>.
    /// </summary>
    internal static readonly PropertyChangedEventArgs CanBeCanceledChangedEventArgs =
        new(nameof(AsyncCommandBase.CanBeCanceled));

    /// <summary>
    /// The cached <see cref="PropertyChangedEventArgs"/> for <see cref="AsyncCommandBase.IsCancellationRequested"/>.
    /// </summary>
    internal static readonly PropertyChangedEventArgs IsCancellationRequestedChangedEventArgs =
        new(nameof(AsyncCommandBase.IsCancellationRequested));

    /// <summary>
    /// The cached <see cref="PropertyChangedEventArgs"/> for <see cref="AsyncCommandBase.IsRunning"/>.
    /// </summary>
    internal static readonly PropertyChangedEventArgs IsRunningChangedEventArgs =
        new(nameof(AsyncCommandBase.IsRunning));

    /// <summary>
    /// The cached <see cref="PropertyChangedEventArgs"/> for <see cref="AsyncCommandBase.IsRunning"/>.
    /// </summary>
    internal static readonly PropertyChangedEventArgs ExecutionTaskChangedEventArgs =
        new(nameof(AsyncCommandBase.ExecutionTask));
}

public abstract class AsyncCommandBase : AsyncCommandBase<object>
{
}

[DoNotNotify]
public abstract class AsyncCommandBase<T> : IAsyncRelayCommand<T>
{
    /// <summary>
    /// The <see cref="CancellationTokenSource"/> instance to use to cancel the task.
    /// </summary>
    /// <remarks>This is only used when <see cref="IsCancelable"/> is not <see langword="false"/>.</remarks>
    private CancellationTokenSource? cancellationTokenSource;

    public bool CanExecute(object? parameter) => CanExecute((T?)parameter);

    public void Execute(object? parameter) => Execute((T?)parameter);

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    private Task? executionTask;

    /// <inheritdoc/>
    public Task? ExecutionTask
    {
        get => executionTask;
        protected set
        {
            void Callback()
            {
                // When the task completes
                OnPropertyChanged(AsyncCommandBaseEventArgs.IsRunningChangedEventArgs);
                OnPropertyChanged(AsyncCommandBaseEventArgs.CanBeCanceledChangedEventArgs);
            }

            if (ReferenceEquals(executionTask, value))
            {
                return;
            }

            // Check the status of the new task before assigning it to the
            // target field. This is so that in case the task is either
            // null or already completed, we can avoid the overhead of
            // scheduling the method to monitor its completion.
            var isAlreadyCompletedOrNull = value?.IsCompleted ?? true;

            executionTask = value;

            OnPropertyChanged(AsyncCommandBaseEventArgs.ExecutionTaskChangedEventArgs);

            // If the input task is either null or already completed, we don't need to
            // execute the additional logic to monitor its completion, so we can just bypass
            // the rest of the method and return that the field changed here. The return value
            // does not indicate that the task itself has completed, but just that the property
            // value itself has changed (ie. the referenced task instance has changed).
            // This mirrors the return value of all the other synchronous Set methods as well.
            if (isAlreadyCompletedOrNull)
            {
                Callback();

                return;
            }

            // We use a local async function here so that the main method can
            // remain synchronous and return a value that can be immediately
            // used by the caller. This mirrors Set<T>(ref T, T, string).
            // We use an async void function instead of a Task-returning function
            // so that if a binding update caused by the property change notification
            // causes a crash, it is immediately reported in the application instead of
            // the exception being ignored (as the returned task wouldn't be awaited),
            // which would result in a confusing behavior for users.
            async void MonitorTask()
            {
                try
                {
                    // Await the task and ignore any exceptions
                    await value!;
                }
                catch
                {
                    // ignored
                }

                // Only notify if the property hasn't changed
                if (ReferenceEquals(executionTask, value))
                {
                    OnPropertyChanged(AsyncCommandBaseEventArgs.ExecutionTaskChangedEventArgs);
                }

                Callback();
            }

            MonitorTask();

            Callback();
        }
    }

    protected abstract bool IsCancelable { get; }

    /// <inheritdoc/>
    public bool CanBeCanceled => IsCancelable && IsRunning;

    /// <inheritdoc/>
    public bool IsCancellationRequested => cancellationTokenSource?.IsCancellationRequested == true;

    /// <inheritdoc/>
    public bool IsRunning => ExecutionTask?.IsCompleted == false;

    /// <inheritdoc/>
    public void NotifyCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    protected abstract bool CanExecuteImpl(T? parameter);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual bool CanExecute(T? parameter) => !IsRunning && CanExecuteImpl(parameter);

    protected abstract Task ExecuteAsyncImpl(T? parameter, CancellationToken? cancellationToken);

    /// <inheritdoc/>
    public virtual void Execute(T? parameter)
    {
        _ = ExecuteAsync(parameter);
    }

    public Task ExecuteAsync(object? parameter) => ExecuteAsync((T?)parameter);

    /// <inheritdoc/>
    public Task ExecuteAsync(T? parameter)
    {
        if (!CanExecute(parameter))
        {
            return Task.CompletedTask;
        }

        // Non cancelable command delegate
        if (IsCancelable)
        {
            return ExecutionTask = ExecuteAsyncImpl(parameter, null);
        }

        // Cancel the previous operation, if one is pending
        cancellationTokenSource?.Cancel();

        var tokenSource = cancellationTokenSource = new CancellationTokenSource();

        OnPropertyChanged(AsyncCommandBaseEventArgs.IsCancellationRequestedChangedEventArgs);

        // Invoke the cancelable command delegate with a new linked token
        return ExecutionTask = ExecuteAsyncImpl(parameter, tokenSource.Token);
    }

    /// <inheritdoc/>
    public void Cancel()
    {
        cancellationTokenSource?.Cancel();

        OnPropertyChanged(AsyncCommandBaseEventArgs.IsCancellationRequestedChangedEventArgs);
        OnPropertyChanged(AsyncCommandBaseEventArgs.CanBeCanceledChangedEventArgs);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    [NotifyPropertyChangedInvocator]
    private void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, e);
    }
}
