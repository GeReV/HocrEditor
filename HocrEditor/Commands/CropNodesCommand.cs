﻿using System.Collections.Generic;
using System.Linq;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Core;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.ViewModels;
using SkiaSharp;

namespace HocrEditor.Commands;

public class CropNodesCommand(HocrPageViewModel hocrPageViewModel)
    : UndoableCommandBase<ICollection<HocrNodeViewModel>>(hocrPageViewModel)
{
    private const int WORD_CROP_PADDING = 1;

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

        if (words.Count != 0)
        {
            hocrPageViewModel.ThresholdedBitmap
                .ContinueWith(
                    async task =>
                    {
                        var thresholdedImage = await task.ConfigureAwait(false);

                        commands.AddRange(
                            words.Select(
                                word => PropertyChangeCommand.FromProperty(
                                    word,
                                    n => n.BBox,
                                    oldBounds =>
                                    {
                                        var bounds = oldBounds.ToSKRectI();

                                        var croppedBounds = CropWord(thresholdedImage, bounds);

                                        if (croppedBounds.Width == 0 || croppedBounds.Height == 0 ||
                                            croppedBounds == bounds)
                                        {
                                            return bounds;
                                        }

                                        return croppedBounds;
                                    }
                                )
                            )
                        );
                    }
                )
                .Wait();
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
        var nextBounds = bounds;
        var pixels = binaryImage.GetPixelSpan();
        var bytesPerPixel = binaryImage.BytesPerPixel;
        var width = binaryImage.RowBytes;
        var cornerColor = pixels[bounds.Top * width + bounds.Left * bytesPerPixel];

        // Horizontal lines from top.
        for (var brk = false; nextBounds.Top <= nextBounds.Bottom;)
        {
            for (var x = nextBounds.Left; x <= nextBounds.Right; x++)
            {
                if (pixels[nextBounds.Top * width + x * bytesPerPixel] != cornerColor)
                {
                    brk = true;
                    break;
                }
            }

            if (brk)
            {
                break;
            }

            nextBounds.Top++;
        }

        // Horizontal lines from bottom.
        for (var brk = false; nextBounds.Bottom >= nextBounds.Top;)
        {
            for (var x = nextBounds.Left; x <= nextBounds.Right; x++)
            {
                if (pixels[nextBounds.Bottom * width + x * bytesPerPixel] != cornerColor)
                {
                    brk = true;
                    break;
                }
            }

            if (brk)
            {
                break;
            }

            nextBounds.Bottom--;
        }

        // Vertical lines from left.
        for (var brk = false; nextBounds.Left <= nextBounds.Right;)
        {
            for (var y = nextBounds.Top; y <= nextBounds.Bottom; y++)
            {
                if (pixels[y * width + nextBounds.Left * bytesPerPixel] != cornerColor)
                {
                    brk = true;
                    break;
                }
            }

            if (brk)
            {
                break;
            }

            nextBounds.Left++;
        }

        // Vertical lines from right.
        for (var brk = false; nextBounds.Right >= nextBounds.Left;)
        {
            for (var y = nextBounds.Top; y <= nextBounds.Bottom; y++)
            {
                if (pixels[y * width + nextBounds.Right * bytesPerPixel] != cornerColor)
                {
                    brk = true;
                    break;
                }
            }

            if (brk)
            {
                break;
            }

            nextBounds.Right--;
        }

        // If anything change, add some padding.
        if (nextBounds != bounds)
        {
            nextBounds.Inflate(WORD_CROP_PADDING, WORD_CROP_PADDING);
        }

        return nextBounds;
    }
}
