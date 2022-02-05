using System;
using System.Collections.Generic;
using System.Linq;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Models;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class ConvertToImageCommand : UndoableCommandBase<IEnumerable<HocrNodeViewModel>>
{
    private readonly HocrPageViewModel hocrPageViewModel;

    public ConvertToImageCommand(HocrPageViewModel hocrPageViewModel) : base(hocrPageViewModel)
    {
        this.hocrPageViewModel = hocrPageViewModel;
    }

    public override bool CanExecute(IEnumerable<HocrNodeViewModel>? parameter)
    {
        if (parameter == null)
        {
            return false;
        }

        var list = parameter.ToList();

        return list.Any() &&
               list.All(node => node.NodeType == HocrNodeType.ContentArea);
    }

    public override void Execute(IEnumerable<HocrNodeViewModel>? parameter)
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

        ArgumentNullException.ThrowIfNull(hocrPageViewModel.HocrPage);

        var commands = new List<UndoRedoCommand>
        {
            new PageRemoveNodesCommand(hocrPageViewModel, selectedNodes.SelectMany(node => node.Children))
        };

        foreach (var node in selectedNodes)
        {
            var hocrImage = new HocrImage(
                node.HocrNode.Id,
                node.HocrNode.ParentId,
                node.HocrNode.Title,
                node.HocrNode.Language,
                node.HocrNode.Direction
            );

            commands.Add(new CollectionReplaceCommand(hocrPageViewModel.HocrPage.ChildNodes, node.HocrNode, hocrImage));

            commands.Add(
                PropertyChangeCommand.FromProperty(
                    node,
                    n => n.HocrNode,
                    hocrImage
                )
            );
        }

        UndoRedoManager.ExecuteCommands(commands);
    }
}
