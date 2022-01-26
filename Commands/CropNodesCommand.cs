using System.Collections.Generic;
using System.Linq;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Helpers;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class CropNodes : UndoableCommandBase<ICollection<HocrNodeViewModel>>
{
    private readonly HocrPageViewModel hocrPageViewModel;

    public CropNodes(HocrPageViewModel hocrPageViewModel) : base(hocrPageViewModel)
    {
        this.hocrPageViewModel = hocrPageViewModel;
    }

    public override bool CanExecute(ICollection<HocrNodeViewModel>? nodes) => nodes is { Count: > 0 };

    public override void Execute(ICollection<HocrNodeViewModel>? nodes)
    {
        if (nodes == null)
        {
            return;
        }

        // Order nodes from the latest occurrence (deepest) to earliest, so if a chain of parent-children is selected,
        // the deepest child is cropped, then its parent and so on, bottom-up.
        var selectedNodes = nodes.OrderBy(node => -hocrPageViewModel.Nodes.IndexOf(node));

        var commands = selectedNodes.Select(
            node => PropertyChangeCommand.FromProperty(
                node,
                n => n.BBox,
                () => NodeHelpers.CalculateUnionRect(node.Children)
            )
        );

        // ExecuteUndoableCommand(commands);
        UndoRedoManager.ExecuteCommands(commands);
    }
}
