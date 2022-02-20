using System;
using System.Collections.Generic;
using System.Linq;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class CreateNodeCommand : UndoableCommandBase<HocrNodeType>
{
    private readonly HocrPageViewModel hocrPageViewModel;

    public CreateNodeCommand(HocrPageViewModel hocrPageViewModel) : base(hocrPageViewModel)
    {
        this.hocrPageViewModel = hocrPageViewModel;
    }

    public override bool CanExecute(HocrNodeType nodeType)
    {
        return true;
    }

    public override void Execute(HocrNodeType nodeType)
    {
        var nodeTypeStack = BuildNodeTypeStack(nodeType);

        var rootNode = hocrPageViewModel.Nodes.First(n => n.IsRoot);

        var commands = new List<UndoRedoCommand>();

        var nodes = new List<HocrNodeViewModel>();

        var selectionBounds = hocrPageViewModel.SelectionBounds;

        var title = selectionBounds.ToBboxAttribute();

        var parentNode = rootNode;

        foreach (var hocrNodeType in nodeTypeStack)
        {
            var id = hocrPageViewModel.NextId();

            // TODO: Avoid this horrible bit somehow.
            IHocrNode hocrNode = hocrNodeType switch
            {
                HocrNodeType.ContentArea => new HocrContentArea(
                    id,
                    parentNode.Id,
                    title,
                    string.Empty,
                    Direction.Ltr,
                    Enumerable.Empty<HocrNode>()
                ),
                HocrNodeType.Paragraph => new HocrParagraph(
                    id,
                    parentNode.Id,
                    title,
                    string.Empty,
                    Direction.Ltr,
                    Enumerable.Empty<HocrNode>()
                ),
                HocrNodeType.Line => new HocrLine(
                    id,
                    parentNode.Id,
                    title + $"; x_size {(int)(((HocrPage)rootNode.HocrNode).Dpi.Item2 * (12.0f / 72.0f))}",
                    string.Empty,
                    Direction.Ltr,
                    Enumerable.Empty<HocrNode>()
                ),
                HocrNodeType.TextFloat => new HocrTextFloat(
                    id,
                    parentNode.Id,
                    title,
                    string.Empty,
                    Direction.Ltr,
                    Enumerable.Empty<HocrNode>()
                ),
                HocrNodeType.Caption => new HocrCaption(
                    id,
                    parentNode.Id,
                    title,
                    string.Empty,
                    Direction.Ltr,
                    Enumerable.Empty<HocrNode>()
                ),
                HocrNodeType.Header => new HocrHeader(
                    id,
                    parentNode.Id,
                    title,
                    string.Empty,
                    Direction.Ltr,
                    Enumerable.Empty<HocrNode>()
                ),
                HocrNodeType.Footer => new HocrFooter(
                    id,
                    parentNode.Id,
                    title,
                    string.Empty,
                    Direction.Ltr,
                    Enumerable.Empty<HocrNode>()
                ),
                HocrNodeType.Word => new HocrWord(
                    id,
                    parentNode.Id,
                    title,
                    string.Empty,
                    Direction.Ltr,
                    string.Empty
                ),
                HocrNodeType.Image => new HocrImage(id, parentNode.Id, title, string.Empty, Direction.Ltr),
                HocrNodeType.Page => throw new InvalidOperationException("Expected to not receive a page node type"),
                _ => throw new ArgumentOutOfRangeException()
            };

            var hocrNodeViewModel = new HocrNodeViewModel(hocrNode)
            {
                // No need to set this with a PropertyChangeCommand, as it doesn't need to be reset on undo.
                BBox = selectionBounds
            };

            nodes.Add(hocrNodeViewModel);

            // Set parent on new node.
            commands.Add(PropertyChangeCommand.FromProperty(hocrNodeViewModel, n => n.Parent, parentNode));

            // Add node to its parent children collection.
            commands.Add(parentNode.Children.ToCollectionAddCommand(hocrNodeViewModel));

            parentNode = hocrNodeViewModel;
        }

        // Add all nodes to the page's node collection.
        commands.Add(hocrPageViewModel.Nodes.ToCollectionAddCommand(nodes));

        UndoRedoManager.BeginBatch();
        UndoRedoManager.ExecuteCommands(commands);

        new ExclusiveSelectNodesCommand(hocrPageViewModel).TryExecute(new List<HocrNodeViewModel> { parentNode });

        UndoRedoManager.ExecuteBatch();
    }

    private static IEnumerable<HocrNodeType> BuildNodeTypeStack(HocrNodeType nodeType)
    {
        var nodeTypeStack = new Stack<HocrNodeType>();

        nodeTypeStack.Push(nodeType);

        var parentNodeType = HocrNodeTypeHelper.GetParentNodeType(nodeType);

        while (parentNodeType != null && parentNodeType != HocrNodeType.Page)
        {
            nodeTypeStack.Push(parentNodeType.Value);

            parentNodeType = HocrNodeTypeHelper.GetParentNodeType(parentNodeType.Value);
        }

        return nodeTypeStack;
    }
}
