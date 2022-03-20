using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class PasteCommand : UndoableCommandBase
{
    private const int PASTE_OFFSET = 20;

    private readonly HocrPageViewModel hocrPageViewModel;

    public PasteCommand(HocrPageViewModel hocrPageViewModel) : base(hocrPageViewModel)
    {
        this.hocrPageViewModel = hocrPageViewModel;
    }

    public override bool CanExecute(object? parameter) =>
        hocrPageViewModel.Clipboard.HasData;

    public override void Execute(object? parameter)
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
            _ => throw new ArgumentOutOfRangeException()
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

        // Those inserted nodes are added to their original parent nodes.
        commands.AddRange(topmostNodes.Select(n => n.Parent!.Children.ToCollectionAddCommand(n)));

        // Add all nodes to the page nodes collection.
        commands.Add(hocrPageViewModel.Nodes.ToCollectionAddCommand(allNodes));

        UndoRedoManager.BeginBatch();

        UndoRedoManager.ExecuteCommands(commands);

        new ExclusiveSelectNodesCommand(hocrPageViewModel).Execute(topmostNodes);

        UndoRedoManager.ExecuteBatch();
    }
}
