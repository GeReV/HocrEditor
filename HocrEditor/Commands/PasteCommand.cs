using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.ViewModels;
using Rect = HocrEditor.Models.Rect;

namespace HocrEditor.Commands;

public class PasteCommand : UndoableCommandBase<ICollection<HocrNodeViewModel>>
{
    private const int PASTE_OFFSET = 20;

    private readonly HocrPageViewModel hocrPageViewModel;

    public PasteCommand(HocrPageViewModel hocrPageViewModel) : base(hocrPageViewModel)
    {
        this.hocrPageViewModel = hocrPageViewModel;
    }

    public override bool CanExecute(ICollection<HocrNodeViewModel>? selectedNodes) =>
        hocrPageViewModel.Clipboard.HasData;

    public override void Execute(ICollection<HocrNodeViewModel>? selectedNodes)
    {
        if (!hocrPageViewModel.Clipboard.HasData)
        {
            return;
        }

        var topmostNodes = NodeHelpers.CloneNodeCollection(hocrPageViewModel.Clipboard.GetData()).ToHashSet();

        var offset = hocrPageViewModel.Direction switch
        {
            Direction.Ltr => new Point(PASTE_OFFSET, PASTE_OFFSET),
            Direction.Rtl => new Point(-PASTE_OFFSET, PASTE_OFFSET),
            _ => throw new ArgumentOutOfRangeException(nameof(hocrPageViewModel.Direction))
        };

        // Update the nodes' individual data.
        var allNodes = topmostNodes.RecursiveSelect(n => n.Children).ToList();

        foreach (var node in allNodes)
        {
            node.Id = hocrPageViewModel.NextId();

            // TODO: Any way to keep moving it down as more copies are added?
            var bbox = node.BBox;
            bbox.Offset(offset);
            node.BBox = bbox;

            // Updating a parent's ID will not update the ParentId of its children.
            node.ParentId = node.Parent?.Id ?? -1;
        }

        var commands = new List<UndoRedoCommand>();

        var selectedNode = selectedNodes?.SingleOrDefault();

        // If we can paste into the selected node
        if (selectedNode != null && topmostNodes.All(n => HocrNodeTypeHelper.CanNodeTypeBeChildOf(n.NodeType, selectedNode.NodeType)))
        {
            // Set the topmost nodes' parent to be the selected node.
            commands.AddRange(topmostNodes.Select(node => PropertyChangeCommand.FromProperty(node, n => n.Parent, selectedNode)));

            // Add them to the selected node's children.
            commands.Add(selectedNode.Children.ToCollectionAddCommand(topmostNodes));

            MoveNodesIntoRect(topmostNodes, selectedNode.BBox);
        }
        else
        {
            // Those inserted nodes are added to their original parent nodes.
            commands.AddRange(topmostNodes.Select(n => n.Parent!.Children.ToCollectionAddCommand(n)));
        }

        // Add all nodes to the page nodes collection.
        commands.Add(hocrPageViewModel.Nodes.ToCollectionAddCommand(allNodes));

        UndoRedoManager.BeginBatch();

        UndoRedoManager.ExecuteCommands(commands);

        new ExclusiveSelectNodesCommand(hocrPageViewModel).Execute(topmostNodes);

        UndoRedoManager.ExecuteBatch();
    }

    private static void MoveNodesIntoRect(IEnumerable<HocrNodeViewModel> topmostNodes, Rect rect)
    {
        foreach (var node in topmostNodes)
        {
            var location = node.BBox.Location;

            location.Clamp(rect);

            var offset = location - node.BBox.Location;

            node.BBox = node.BBox with
            {
                Location = location
            };

            foreach (var descendant in node.Descendants)
            {
                descendant.BBox = descendant.BBox with
                {
                    Location = descendant.BBox.Location + offset
                };
            }
        }
    }
}
