using HocrEditor.Core;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public abstract class UndoableCommandBase(IUndoRedoCommandsService undoRedoCommandsService) : CommandBase
{
    private IUndoRedoCommandsService UndoRedoCommandsService { get; } = undoRedoCommandsService;

    protected UndoRedoManager UndoRedoManager => UndoRedoCommandsService.UndoRedoManager;
}

public abstract class UndoableCommandBase<T>(IUndoRedoCommandsService undoRedoCommandsService) : CommandBase<T>
{
    private IUndoRedoCommandsService UndoRedoCommandsService { get; } = undoRedoCommandsService;

    protected UndoRedoManager UndoRedoManager => UndoRedoCommandsService.UndoRedoManager;

    public abstract override bool CanExecute(T? nodes);

    public abstract override void Execute(T? nodes);
}
