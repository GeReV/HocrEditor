using System.Collections.Generic;
using System.Linq;
using HocrEditor.Helpers;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class CropNodes : CommandBase<IList<HocrNodeViewModel>>
{
    private readonly MainWindowViewModel mainWindowViewModel;

    public CropNodes(MainWindowViewModel mainWindowViewModel)
    {
        this.mainWindowViewModel = mainWindowViewModel;
    }

    public override bool CanExecute(IList<HocrNodeViewModel>? nodes) => nodes is { Count: > 0 };

    public override void Execute(IList<HocrNodeViewModel>? nodes)
    {
        if (nodes == null || mainWindowViewModel.Document == null)
        {
            return;
        }

        // Order nodes from the latest occurrence (deepest) to earliest, so if a chain of parent-children is selected,
        // the deepest child is cropped, then its parent and so on, bottom-up.
        var selectedNodes = nodes.OrderBy(node => -mainWindowViewModel.Document.Nodes.IndexOf(node));

        var commands = selectedNodes.Select(
            node => node.ToPropertyChangedCommand(
                n => n.BBox,
                NodeHelpers.CalculateUnionRect(node.Children)
            )
        );

        // ExecuteUndoableCommand(commands);
        mainWindowViewModel.UndoRedoManager.ExecuteCommands(commands);
    }
}
