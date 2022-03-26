using System;
using System.Collections.Generic;
using System.Linq;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Controls;
using HocrEditor.Core;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class WordSplitCommand : UndoableCommandBase<WordSplitEventArgs>
{
    private HocrPageViewModel HocrPageViewModel { get; }

    public WordSplitCommand(HocrPageViewModel hocrPageViewModel) : base(hocrPageViewModel)
    {
        HocrPageViewModel = hocrPageViewModel;
    }

    public override bool CanExecute(WordSplitEventArgs? e) => e != null;

    public override void Execute(WordSplitEventArgs? e)
    {
        ArgumentNullException.ThrowIfNull(e);

        var node = e.Node;

        if (node.Parent == null)
        {
            throw new InvalidOperationException("Expected node to have a parent");
        }

        var commands = new List<UndoRedoCommand>();

        commands.Add(PropertyChangeCommand.FromProperty(node, n => n.BBox, node.BBox with
        {
            Right = e.SplitPosition
        }));

        var parent = node.Parent;

        var clone = (HocrNodeViewModel)node.Clone();

        int insertOffset;
        string cloneText, nodeText;
        HocrNodeViewModel selectNode;

        switch (HocrPageViewModel.Direction)
        {
            case Direction.Ltr:
                (nodeText, cloneText) = e.Words;

                selectNode = node;

                insertOffset = 1;
                break;
            case Direction.Rtl:
                (cloneText, nodeText) = e.Words;

                selectNode = clone;

                insertOffset = 0;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        clone.Id = HocrPageViewModel.NextId();
        clone.InnerText = cloneText;
        clone.BBox = clone.BBox with
        {
            Left = e.SplitPosition
        };

        commands.Add(PropertyChangeCommand.FromProperty(node, n => n.InnerText, nodeText));

        // Add to parent.
        commands.Add(parent.Children.ToCollectionInsertCommand(parent.Children.IndexOf(node) + insertOffset, clone));

        // Add to page nodes collection.
        commands.Add(HocrPageViewModel.Nodes.ToCollectionInsertCommand(HocrPageViewModel.Nodes.IndexOf(node) + insertOffset, clone));

        UndoRedoManager.BeginBatch();

        UndoRedoManager.ExecuteCommands(commands);

        if (Settings.AutoClean)
        {
            new CropNodesCommand(HocrPageViewModel).Execute(new[] { node, clone });
        }

        new ExclusiveSelectNodesCommand(HocrPageViewModel).Execute(Enumerable.Repeat(selectNode, 1));

        UndoRedoManager.ExecuteBatch();
    }
}
