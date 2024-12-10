using System.Linq;
using System.Windows.Input;
using HocrEditor.Core;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.ViewModels;
using Optional.Collections;
using Optional.Unsafe;
using SkiaSharp;
using SkiaSharp.Views.WPF;

namespace HocrEditor.Controls;

public sealed class WordSplittingTool : CanvasToolBase
{
    private static readonly SKColor HighlightColor = new(0xffffff99);

    private bool isDraggingWordSplitter;

    private SKPoint dragStart = SKPoint.Empty;

    private SKPointI wordSplitterPosition = SKPointI.Empty;
    private string wordSplitterValue = string.Empty;
    private int wordSplitterValueSplitStart;
    private int wordSplitterValueSplitLength;

    public override bool CanMount(HocrPageViewModel page) => page.SelectedNodes.Count == 1 &&
                                                             page.SelectedNodes.First().NodeType == HocrNodeType.Word;

    public override void Mount(DocumentCanvas canvas)
    {
        base.Mount(canvas);

        canvas.MouseDown += DocumentCanvasOnMouseDown;
        canvas.MouseUp += DocumentCanvasOnMouseUp;
        canvas.MouseMove += DocumentCanvasOnMouseMove;
        canvas.PreviewKeyDown += DocumentCanvasOnPreviewKeyDown;

        canvas.Cursor = null;

        if (canvas.IsEditing)
        {
            wordSplitterValueSplitStart = canvas.TextBox.SelectionStart;
            wordSplitterValueSplitLength = canvas.TextBox.SelectionLength;
            wordSplitterValue = canvas.EndEditing();

            canvas.OnNodeEdited(wordSplitterValue);
        }
        else
        {
            wordSplitterValue = canvas.SelectedItems
                .FlatMap(items => items.FirstOrNone())
                .Map(node => node.InnerText)
                .ValueOr(string.Empty);
        }
    }

    protected override void Unmount(DocumentCanvas canvas)
    {
        canvas.MouseDown -= DocumentCanvasOnMouseDown;
        canvas.MouseUp -= DocumentCanvasOnMouseUp;
        canvas.MouseMove -= DocumentCanvasOnMouseMove;
        canvas.PreviewKeyDown -= DocumentCanvasOnPreviewKeyDown;

        wordSplitterPosition = SKPointI.Empty;
        wordSplitterValue = string.Empty;
        wordSplitterValueSplitStart = 0;
        wordSplitterValueSplitLength = 0;
    }

    public override void Render(SKCanvas canvas)
    {
        var control = Canvas.ValueOrFailure();

        control.CanvasSelection.Render(
            canvas,
            HighlightColor
        );

        if (wordSplitterPosition.IsEmpty)
        {
            return;
        }

        var selectedElement = control.Elements[control.SelectedElements.First()].Item2;

        canvas.DrawDashedLine(
            wordSplitterPosition.X,
            selectedElement.Bounds.Top,
            wordSplitterPosition.X,
            selectedElement.Bounds.Bottom,
            HighlightColor
        );
    }

    private void DocumentCanvasOnMouseDown(object sender, MouseButtonEventArgs e)
    {
        var canvas = (DocumentCanvas)sender;

        if (canvas.ViewModel?.Nodes is not { Count: > 0 })
        {
            return;
        }

        if (isDraggingWordSplitter)
        {
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        Mouse.Capture(canvas);

        e.Handled = true;

        var normalizedPosition = SKPointI.Truncate(
            canvas.Surface.InverseTransformation.MapPoint(e.GetPosition(canvas).ToSKPoint())
        );

        dragStart = normalizedPosition;

        Keyboard.Focus(canvas);

        Ensure.IsValid(
            nameof(canvas.SelectedItems),
            canvas.SelectedItems.Exists(items => items.Count == 1),
            "Expected to have exactly one node selected"
        );
        Ensure.IsValid(
            nameof(canvas.SelectedItems),
            canvas.SelectedItems.Exists(items => items.First().NodeType == HocrNodeType.Word),
            "Expected selected node to be a word"
        );

        var selectedElement = canvas.Elements[canvas.SelectedElements.First()].Item2;

        if (selectedElement.Bounds.Contains(normalizedPosition))
        {
            isDraggingWordSplitter = true;

            wordSplitterPosition = normalizedPosition;
        }

        canvas.Refresh();
    }

    private void DocumentCanvasOnMouseUp(object sender, MouseButtonEventArgs e)
    {
        var canvas = (DocumentCanvas)sender;

        if (!canvas.SelectedItems.HasValue)
        {
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        isDraggingWordSplitter = false;

        canvas.ReleaseMouseCapture();

        canvas.Refresh();
    }

    private void DocumentCanvasOnMouseMove(object sender, MouseEventArgs e)
    {
        var canvas = (DocumentCanvas)sender;

        if (!canvas.SelectedItems.HasValue)
        {
            return;
        }

        var normalizedPosition = SKPointI.Truncate(
            canvas.Surface.InverseTransformation.MapPoint(e.GetPosition(canvas).ToSKPoint())
        );

        var delta = normalizedPosition - dragStart;

        var hoveringOverSelection =
            canvas.CanvasSelection.Bounds.Contains(normalizedPosition);

        canvas.Cursor = hoveringOverSelection
            ? Cursors.SizeWE
            : null;

        if (isDraggingWordSplitter)
        {
            var newLocation = dragStart + delta;

            var selectedElement = canvas.Elements[canvas.SelectedElements.First()].Item2;

            newLocation.Clamp(selectedElement.Bounds);

            wordSplitterPosition = SKPointI.Truncate(newLocation);
        }

        canvas.Refresh();
    }

    private void DocumentCanvasOnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var canvas = (DocumentCanvas)sender;

        switch (e.Key)
        {
            case Key.Left or Key.Right:
            {
                e.Handled = true;

                var delta = KeyboardDeltaMultiply();

                MoveSplitterRelative(canvas, e.Key == Key.Left ? -delta : delta);
                break;
            }
            case Key.Return:
            {
                e.Handled = true;

                FinishEdit(canvas);
                break;
            }
        }
    }

    private void MoveSplitterRelative(DocumentCanvas canvas, int delta)
    {
        if (!canvas.SelectedItems.HasValue)
        {
            return;
        }

        var selectedElement = canvas.Elements[canvas.SelectedElements.First()].Item2;

        if (wordSplitterPosition.IsEmpty)
        {
            var x = delta > 0 ? selectedElement.Bounds.Left : selectedElement.Bounds.Right;

            wordSplitterPosition = new SKPointI(x, selectedElement.Bounds.Top);
        }

        wordSplitterPosition.X += delta;

        wordSplitterPosition.Clamp(selectedElement.Bounds);

        canvas.Refresh();
    }

    private void FinishEdit(DocumentCanvas canvas)
    {
        var first = wordSplitterValue;
        var second = first;

        if (wordSplitterValueSplitStart > 0 &&
            wordSplitterValueSplitStart + wordSplitterValueSplitLength < wordSplitterValue.Length)
        {
            var firstEnd = wordSplitterValueSplitStart;
            first = wordSplitterValue[..firstEnd];

            var secondStart = (wordSplitterValueSplitStart + wordSplitterValueSplitLength);
            second = wordSplitterValue[secondStart..];
        }

        var node = canvas.SelectedItems.ValueOrFailure().First();

        Ensure.IsValid(nameof(node), node.NodeType == HocrNodeType.Word, "Expected node to be a word");

        var splitPosition = wordSplitterPosition.X;

        // Reset tool, which will clear selection.
        canvas.ActiveTool = DocumentCanvasTools.SelectionTool;

        // Split the word, which selects a node, so order with previous statement matters.
        canvas.OnWordSplit(node, splitPosition, (first, second));
    }
}
