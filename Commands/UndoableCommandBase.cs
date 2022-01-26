using HocrEditor.Core;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public abstract class UndoableCommandBase : CommandBase
{
    private IUndoRedoCommandsService UndoRedoCommandsService { get; }

    protected UndoRedoManager UndoRedoManager => UndoRedoCommandsService.UndoRedoManager;

    protected UndoableCommandBase(IUndoRedoCommandsService undoRedoCommandsService)
    {
        UndoRedoCommandsService = undoRedoCommandsService;
    }
}

public abstract class UndoableCommandBase<T> : CommandBase<T>
{
    private IUndoRedoCommandsService UndoRedoCommandsService { get; }

    protected UndoRedoManager UndoRedoManager => UndoRedoCommandsService.UndoRedoManager;

    protected UndoableCommandBase(IUndoRedoCommandsService undoRedoCommandsService)
    {
        UndoRedoCommandsService = undoRedoCommandsService;
    }

    public abstract override bool CanExecute(T? nodes);

    public abstract override void Execute(T? nodes);
}
