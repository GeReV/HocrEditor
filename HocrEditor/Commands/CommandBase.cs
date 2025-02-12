﻿using System;
using CommunityToolkit.Mvvm.Input;

namespace HocrEditor.Commands;

public abstract class CommandBase : IRelayCommand
{
    public abstract bool CanExecute(object? parameter);

    public abstract void Execute(object? parameter);

    public event EventHandler? CanExecuteChanged;

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public abstract class CommandBase<T> : CommandBase, IRelayCommand<T>
{
    public abstract bool CanExecute(T? nodes);

    public abstract void Execute(T? nodes);

    public override bool CanExecute(object? parameter) => CanExecute((T?)parameter);

    public override void Execute(object? parameter) => Execute((T?)parameter);
}
