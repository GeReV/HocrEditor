namespace HocrEditor.ViewModels;

public interface IUndoRedoCommandsService
{
    UndoRedoManager UndoRedoManager { get; }
}
