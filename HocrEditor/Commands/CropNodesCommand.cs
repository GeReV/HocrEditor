using System;
using System.Collections.Generic;
using System.Linq;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Core;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.ViewModels;
using SkiaSharp;

namespace HocrEditor.Commands;

public class CropNodesCommand : UndoableCommandBase<ICollection<HocrNodeViewModel>>
{
    private readonly HocrPageViewModel hocrPageViewModel;

    public CropNodesCommand(HocrPageViewModel hocrPageViewModel) : base(hocrPageViewModel)
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
        var selectedNodes = nodes
            .OrderBy(node => -hocrPageViewModel.Nodes.IndexOf(node))
            .ToList();

        if (Settings.AutoClean)
        {
            selectedNodes = selectedNodes
                .Concat(selectedNodes.SelectMany(n => n.Ascendants.TakeWhile(a => !a.IsRoot)))
                .Distinct() // Duplicates are likely; filter them out.
                .ToList();
        }

        var commands = new List<UndoRedoCommand>();

        var words = selectedNodes
            .Where(n => n.NodeType == HocrNodeType.Word)
            .ToList();

        if (words.Any())
        {
            ArgumentNullException.ThrowIfNull(hocrPageViewModel.ThresholdedImage);

            foreach (var word in words)
            {
                commands.Add(
                    PropertyChangeCommand.FromProperty(word, n => n.BBox, oldBounds =>
                    {
                        var bounds = oldBounds.ToSKRectI();

                        var croppedBounds = CropWord(hocrPageViewModel.ThresholdedImage, bounds);

                        if (croppedBounds.Width == 0 || croppedBounds.Height == 0 || croppedBounds == bounds)
                        {
                            return bounds;
                        }

                        return croppedBounds;
                    }
                ));
            }
        }

        commands.AddRange(
            selectedNodes
                .Where(n => n.NodeType != HocrNodeType.Word) // Words get a different treatment above.
                .Select(
                    node => PropertyChangeCommand.FromProperty(
                        node,
                        n => n.BBox,
                        () => NodeHelpers.CalculateUnionRect(node.Children)
                    )
                )
        );

        UndoRedoManager.ExecuteCommands(commands);
    }

    private static SKRectI CropWord(SKBitmap binaryImage, SKRectI bounds)
    {
        var pixels = binaryImage.GetPixelSpan();
        var width = binaryImage.RowBytes;
        var cornerColor = pixels[bounds.Top * width + bounds.Left];

        // Horizontal lines from top.
        for (var brk = false; bounds.Top <= bounds.Bottom;)
        {
            for (var x = bounds.Left; x <= bounds.Right; x++)
            {
                if (pixels[bounds.Top * width + x] != cornerColor)
                {
                    brk = true;
                    break;
                }
            }

            if (brk)
            {
                break;
            }

            bounds.Top++;
        }

        // Horizontal lines from bottom.
        for (var brk = false; bounds.Bottom >= bounds.Top;)
        {
            for (var x = bounds.Left; x <= bounds.Right; x++)
            {
                if (pixels[bounds.Bottom * width + x] != cornerColor)
                {
                    brk = true;
                    break;
                }
            }

            if (brk)
            {
                break;
            }

            bounds.Bottom--;
        }

        // Vertical lines from left.
        for (var brk = false; bounds.Left <= bounds.Right;)
        {
            for (var y = bounds.Top; y <= bounds.Bottom; y++)
            {
                if (pixels[y * width + bounds.Left] != cornerColor)
                {
                    brk = true;
                    break;
                }
            }

            if (brk)
            {
                break;
            }

            bounds.Left++;
        }

        // Vertical lines from right.
        for (var brk = false; bounds.Right >= bounds.Left;)
        {
            for (var y = bounds.Top; y <= bounds.Bottom; y++)
            {
                if (pixels[y * width + bounds.Right] != cornerColor)
                {
                    brk = true;
                    break;
                }
            }

            if (brk)
            {
                break;
            }

            bounds.Right--;
        }

        return bounds;
    }
}
