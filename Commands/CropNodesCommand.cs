using System.Collections.Generic;
using System.Linq;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Helpers;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class CropNodes : CommandBase<ICollection<HocrNodeViewModel>>
{
    private readonly MainWindowViewModel mainWindowViewModel;

    public CropNodes(MainWindowViewModel mainWindowViewModel)
    {
        this.mainWindowViewModel = mainWindowViewModel;
    }

    public override bool CanExecute(ICollection<HocrNodeViewModel>? nodes) => nodes is { Count: > 0 };

    public override void Execute(ICollection<HocrNodeViewModel>? nodes)
    {
        if (nodes == null || mainWindowViewModel.Document.CurrentPage == null)
        {
            return;
        }

        // Order nodes from the latest occurrence (deepest) to earliest, so if a chain of parent-children is selected,
        // the deepest child is cropped, then its parent and so on, bottom-up.
        var selectedNodes = nodes.OrderBy(node => -mainWindowViewModel.Document.CurrentPage.Nodes.IndexOf(node));

        var commands = selectedNodes.Select(
            node => PropertyChangeCommand.FromProperty(
                node,
                n => n.BBox,
                () => NodeHelpers.CalculateUnionRect(node.Children)
            )
        );

        // ExecuteUndoableCommand(commands);
        mainWindowViewModel.UndoRedoManager.ExecuteCommands(commands);
    }
}
