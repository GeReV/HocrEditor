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

        var commands = new List<UndoRedoCommand>();

        commands.Add(new PageRemoveNodesCommand(hocrPageViewModel, selectedNodes.SelectMany(node => node.Children)));

        commands.AddRange(
            selectedNodes.Select(
                node =>
                    PropertyChangeCommand.FromProperty(
                        node,
                        n => n.NodeType,
                        HocrNodeType.Image
                    )
            )
        );

        UndoRedoManager.ExecuteCommands(commands);
    }
}
