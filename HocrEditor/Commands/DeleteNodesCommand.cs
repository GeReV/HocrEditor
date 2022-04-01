using System;
using System.Collections.Generic;
using System.Linq;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Core;
using HocrEditor.Helpers;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class DeleteNodesCommand : UndoableCommandBase<ICollection<HocrNodeViewModel>>
{
    private readonly HocrPageViewModel hocrPageViewModel;

    public DeleteNodesCommand(HocrPageViewModel hocrPageViewModel) : base(hocrPageViewModel)
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

        nodes = nodes.OrderByDescending(n => n.Id).ToList();

        var commands = new List<UndoRedoCommand>
        {
            hocrPageViewModel.SelectedNodes.ToCollectionClearCommand(),
            new PageRemoveNodesCommand(hocrPageViewModel, nodes)
        };

        if (Settings.AutoClean)
        {
            foreach (var node in nodes)
            {
                if (node.Parent == null || nodes.Contains(node.Parent))
                {
                    continue;
                }

                commands.AddRange(NodeCommands.CropParents(node.Parent));
                commands.Add(NodeCommands.RemoveEmptyParents(hocrPageViewModel, node.Parent));
            }
        }

        commands.AddRange(
            nodes.Select(
                selectedNode => PropertyChangeCommand.FromProperty(selectedNode, n => n.IsSelected, false)
            )
        );

        UndoRedoManager.BeginBatch();

        UndoRedoManager.ExecuteCommands(commands);

        if (nodes.Count == 1)
        {
            TrySelectNextNode(nodes.First());
        }

        UndoRedoManager.ExecuteBatch();
    }

    private void TrySelectNextNode(HocrNodeViewModel node)
    {
        Ensure.IsNotNull(node.Parent);

        var children = node.Parent.Children;

        if (children.Count <= 1)
        {
            return;
        }

        var index = children.IndexOf(node);

        var nextSelectedNode = index switch
        {
            0 => children[1],
            _ => children[index - 1]
        };

        new AppendSelectNodesCommand(hocrPageViewModel).Execute(Enumerable.Repeat(nextSelectedNode, 1));
    }
}
