using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class CycleSelectionCommand : UndoableCommandBase
{
    private readonly HocrPageViewModel hocrPageViewModel;

    public CycleSelectionCommand(HocrPageViewModel hocrPageViewModel) : base(
        hocrPageViewModel
    )
    {
        this.hocrPageViewModel = hocrPageViewModel;
    }

    public override bool CanExecute(object? parameter)
        => hocrPageViewModel.SelectedNodes is { Count: 1 };

    public override void Execute(object? parameter)
    {
        // This only makes sense if we have a single node selected.
        if (hocrPageViewModel.SelectedNodes is not { Count: 1 })
        {
            return;
        }

        var selectedNode = hocrPageViewModel.SelectedNodes.First();

        HocrNodeViewModel? next = null;

        var item = selectedNode;

        var stepBack = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        var step = stepBack ? -1 : +1;


        while (item is { Parent: { } })
        {
            var siblings = item.Parent.Children;

            var index = siblings.IndexOf(item);

            // Another sibling is available.
            if ((stepBack && index > 0) || (!stepBack && index < siblings.Count - 1))
            {
                // If the next candidate is of the same type as our selected node, pick it.
                // This way, stepping words will pick the next word, stepping paragraphs will pick the next paragraph, etc.
                if (siblings[index + step].NodeType == selectedNode.NodeType)
                {
                    next = siblings[index + step];

                    break;
                }

                // Next candidate isn't the same type, assumed to be a type that would be a parent (i.e. when picking word and current is a line or paragraph).
                // Pick the next sibling and drill down the first child of each node until we find a node of the same type.
                item = siblings[index + step];

                var found = true;

                while (item.NodeType != selectedNode.NodeType)
                {
                    if (!item.Children.Any())
                    {
                        // Reached a dead-end, no other siblings to continue to, need to step up again and keep walking.
                        found = false;
                        break;
                    }

                    item = stepBack ? item.Children[^1] : item.Children[0];
                }

                if (found)
                {
                    next = item;

                    break;
                }
            }

            if (index + step > 0 && index + step < siblings.Count - 1)
            {
                // Still have another sibling to step to, move to it.
                item = siblings[index + step];
            }
            else
            {
                // No sibling is available, step up so we take the next sibling of the parent.
                item = item.Parent;
            }
        }

        if (next != null)
        {
            new ExclusiveSelectNodesCommand(hocrPageViewModel).Execute(new List<HocrNodeViewModel> { next });
        }
    }
}
