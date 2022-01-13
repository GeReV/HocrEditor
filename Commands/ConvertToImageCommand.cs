using System;
using System.Collections.Generic;
using System.Linq;
using HocrEditor.Models;
using HocrEditor.ViewModels;
using Microsoft.Toolkit.Mvvm.Input;

namespace HocrEditor.Commands;

public class ConvertToImageCommand : IRelayCommand
{
    private readonly MainWindowViewModel mainWindowViewModel;

    public ConvertToImageCommand(MainWindowViewModel mainWindowViewModel)
    {
        this.mainWindowViewModel = mainWindowViewModel;
    }

    public bool CanExecute(object? parameter) =>
        mainWindowViewModel.SelectedNodes != null &&
        mainWindowViewModel.SelectedNodes.Any() &&
        mainWindowViewModel.SelectedNodes.All(node => node.NodeType == HocrNodeType.ContentArea);

    public void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }


        var document = mainWindowViewModel.Document ?? throw new InvalidOperationException();
        var selectedNodes = mainWindowViewModel.SelectedNodes ?? throw new InvalidOperationException();

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

    public event EventHandler? CanExecuteChanged;

    public void NotifyCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
