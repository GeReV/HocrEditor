using System;
using System.Collections.Generic;
using System.Linq;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Models;
using HocrEditor.ViewModels;
using Microsoft.Toolkit.Mvvm.Input;

namespace HocrEditor.Commands;

public class ConvertToImageCommand : IRelayCommand<IEnumerable<HocrNodeViewModel>>
{
    private readonly MainWindowViewModel mainWindowViewModel;

    public ConvertToImageCommand(MainWindowViewModel mainWindowViewModel)
    {
        this.mainWindowViewModel = mainWindowViewModel;
    }

    public bool CanExecute(IEnumerable<HocrNodeViewModel>? parameter)
    {
        if (parameter == null)
        {
            return false;
        }

        var list = parameter.ToList();

        return list.Any() &&
               list.All(node => node.NodeType == HocrNodeType.ContentArea);
    }

    public void Execute(IEnumerable<HocrNodeViewModel>? parameter)
    {
        if (parameter == null)
        {
            return;
        }

        var selectedNodes = parameter.ToList();

        if (!CanExecute(selectedNodes))
        {
            return;
        }

        var document = mainWindowViewModel.Document ?? throw new InvalidOperationException();

        var commands = new List<UndoRedoCommand>();

        commands.Add(new DocumentRemoveNodesCommand(document, selectedNodes.SelectMany(node => node.Children)));

        commands.AddRange(
            selectedNodes.Select(
                node => new PropertyChangedCommand(
                    node,
                    nameof(node.HocrNode),
                    node.HocrNode,
                    new HocrImage(node.HocrNode.Id, node.ParentId, node.HocrNode.Title) { BBox = node.BBox }
                )
            )
        );

        mainWindowViewModel.UndoRedoManager.ExecuteCommands(commands);
    }

    public bool CanExecute(object? parameter) => CanExecute((IEnumerable<HocrNodeViewModel>?)parameter);

    public void Execute(object? parameter) => Execute((IEnumerable<HocrNodeViewModel>?)parameter);

    public event EventHandler? CanExecuteChanged;

    public void NotifyCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
