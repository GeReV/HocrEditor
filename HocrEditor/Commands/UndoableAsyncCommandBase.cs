using HocrEditor.Core;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public abstract class UndoableAsyncCommandBase : AsyncCommandBase
{
    private IUndoRedoCommandsService UndoRedoCommandsService { get; }

    protected UndoRedoManager UndoRedoManager => UndoRedoCommandsService.UndoRedoManager;

    protected UndoableAsyncCommandBase(IUndoRedoCommandsService undoRedoCommandsService)
    {
        UndoRedoCommandsService = undoRedoCommandsService;
    }
}

public abstract class UndoableAsyncCommandBase<T> : AsyncCommandBase<T>
{
    private IUndoRedoCommandsService UndoRedoCommandsService { get; }

    protected UndoRedoManager UndoRedoManager => UndoRedoCommandsService.UndoRedoManager;

    protected UndoableAsyncCommandBase(IUndoRedoCommandsService undoRedoCommandsService)
    {
        UndoRedoCommandsService = undoRedoCommandsService;
    }
}
