using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace HocrEditor.Core;

// Adapted from: https://www.renebergelt.de/blog/2018/03/lazy-loaded-properties-in-wpf/
public sealed class LazyProperty<T> : INotifyPropertyChanged
{
    private readonly CancellationTokenSource cancelTokenSource = new();

    private bool isLoading;
    private bool errorOnLoading;

    private readonly T? defaultValue;
    private T? value;
    private readonly Func<CancellationToken, Task<T?>> retrievalFunc;

    private bool IsLoaded { get; set; }

    public bool IsLoading
    {
        get => isLoading;
        private set
        {
            if (isLoading == value)
            {
                return;
            }

            isLoading = value;
            OnPropertyChanged();
        }
    }

    public bool ErrorOnLoading
    {
        get => errorOnLoading;
        private set
        {
            if (errorOnLoading == value)
            {
                return;
            }

            errorOnLoading = value;
            OnPropertyChanged();
        }
    }

    public T? Value
    {
        get
        {
            if (IsLoaded)
            {
                return value;
            }

            if (isLoading)
            {
                return defaultValue;
            }

            IsLoading = true;

            LoadValueAsync().ConfigureAwait(true);

            return defaultValue;
        }
        set
        {
            if (isLoading)
            {
                // since we set the value now, there is no need
                // to retrieve the "old" value asynchronously
                CancelLoading();
            }

            if (EqualityComparer<T>.Default.Equals(this.value, value))
            {
                return;
            }

            this.value = value;
            IsLoaded = true;
            IsLoading = false;
            ErrorOnLoading = false;

            OnPropertyChanged();
        }
    }

    public Task<T?> ValueAsync
    {
        get
        {
            if (IsLoaded)
            {
                return Task.FromResult(value);
            }

            return LoadValueAsync();
        }
    }

    private async Task<T?> LoadValueAsync() => await retrievalFunc(cancelTokenSource.Token)
        .ContinueWith(
            t =>
            {
                if (t.IsCanceled)
                {
                    return defaultValue;
                }

                if (t.IsFaulted)
                {
                    value = defaultValue;
                    ErrorOnLoading = true;
                    IsLoaded = true;
                    IsLoading = false;
                    OnPropertyChanged(nameof(Value));
                }
                else
                {
                    Value = t.Result;
                }

                return Value;
            }
        );

    public void CancelLoading()
    {
        cancelTokenSource.Cancel();
    }

    public LazyProperty(Func<CancellationToken, Task<T?>> retrievalFunc, T? defaultValue = default)
    {
        ArgumentNullException.ThrowIfNull(retrievalFunc);

        this.retrievalFunc = retrievalFunc;
        this.defaultValue = defaultValue;

        value = default;
    }

    /// <summary>
    /// This allows you to assign the value of this lazy property directly
    /// to a variable of type T
    /// </summary>
    public static implicit operator T?(LazyProperty<T> p) => p.Value;

    public event PropertyChangedEventHandler? PropertyChanged;

    [NotifyPropertyChangedInvocator]
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
