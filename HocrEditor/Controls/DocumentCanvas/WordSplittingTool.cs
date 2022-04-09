using System.Linq;
using System.Windows.Input;
using HocrEditor.Core;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.ViewModels;
using Optional;
using Optional.Collections;
using Optional.Unsafe;
using SkiaSharp;
using SkiaSharp.Views.WPF;

namespace HocrEditor.Controls;

public sealed class WordSplittingTool : ICanvasTool
{
    private static readonly SKColor HighlightColor = new(0xffffff99);

    private Option<DocumentCanvas> documentCanvas = Option.None<DocumentCanvas>();

    private bool isDraggingWordSplitter;

    private SKPoint dragStart = SKPoint.Empty;

    private SKPoint wordSplitterPosition = SKPoint.Empty;
    private string wordSplitterValue = string.Empty;
    private int wordSplitterValueSplitStart;
    private int wordSplitterValueSplitLength;

    public bool CanMount(HocrPageViewModel page) => page.SelectedNodes.Count == 1 &&
                                                    page.SelectedNodes.First().NodeType == HocrNodeType.Word;

    public void Mount(DocumentCanvas canvas)
    {
        documentCanvas = Option.Some(canvas);

        canvas.MouseDown += DocumentCanvasOnMouseDown;
        canvas.MouseUp += DocumentCanvasOnMouseUp;
        canvas.MouseMove += DocumentCanvasOnMouseMove;
        canvas.KeyDown += DocumentCanvasOnKeyDown;

        canvas.Cursor = canvas.CurrentCursor = null;

        if (canvas.IsEditing)
        {
            canvas.OnNodeEdited(canvas.TextBox.Text);

            wordSplitterValue = canvas.TextBox.Text;
            wordSplitterValueSplitStart = canvas.TextBox.SelectionStart;
            wordSplitterValueSplitLength = canvas.TextBox.SelectionLength;

            canvas.EndEditing();
        }
        else
        {
            wordSplitterValue = canvas.SelectedItems.ValueOrFailure()
                .FirstOrNone()
                .Map(node => node.InnerText)
                .ValueOr(string.Empty);
        }
    }

    public void Unmount()
    {
        var canvas = documentCanvas.ValueOrFailure();

        canvas.MouseDown -= DocumentCanvasOnMouseDown;
        canvas.MouseUp -= DocumentCanvasOnMouseUp;
        canvas.MouseMove -= DocumentCanvasOnMouseMove;
        canvas.KeyDown -= DocumentCanvasOnKeyDown;

        wordSplitterPosition = SKPoint.Empty;
        wordSplitterValue = string.Empty;
        wordSplitterValueSplitStart = 0;
        wordSplitterValueSplitLength = 0;
    }

    public void Render(SKCanvas canvas)
    {
        var control = documentCanvas.ValueOrFailure();

        control.CanvasSelection.Render(
            canvas,
            control.Transformation,
            HighlightColor
        );

        if (wordSplitterPosition.IsEmpty)
        {
            return;
        }

        var selectedElement = control.Elements[control.SelectedElements.First()].Item2;

        var bounds = control.Transformation.MapRect(selectedElement.Bounds);
        var point = control.Transformation.MapPoint(wordSplitterPosition);

        canvas.DrawDashedLine(point.X, bounds.Top, point.X, bounds.Bottom, HighlightColor);
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

        Mouse.Capture(canvas);

        var position = e.GetPosition(canvas).ToSKPoint();

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        e.Handled = true;

        dragStart = position;

        Keyboard.Focus(canvas);

        var normalizedPosition = SKPointI.Truncate(canvas.InverseTransformation.MapPoint(position));

        Ensure.IsValid(
            nameof(canvas.SelectedItems),
            canvas.SelectedItems.Exists(items => items.Count == 1),
            "Expected to have exactly one node selected"
        );
        Ensure.IsValid(
            nameof(canvas.SelectedItems),
            canvas.SelectedItems.ValueOrFailure().First().NodeType == HocrNodeType.Word,
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

    private static void DocumentCanvasOnMouseUp(object sender, MouseButtonEventArgs e)
    {
        var canvas = (DocumentCanvas)sender;

        if (!canvas.SelectedItems.HasValue)
        {
            return;
        }

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

        var position = e.GetPosition(canvas).ToSKPoint();

        var delta = canvas.InverseScaleTransformation.MapPoint(position - dragStart);

        if (isDraggingWordSplitter)
        {
            var newLocation = canvas.InverseTransformation.MapPoint(dragStart) + delta;

            var selectedElement = canvas.Elements[canvas.SelectedElements.First()].Item2;

            newLocation.Clamp(selectedElement.Bounds);

            wordSplitterPosition = newLocation;
        }

        canvas.Refresh();
    }

    private void DocumentCanvasOnKeyDown(object sender, KeyEventArgs e)
    {
        var canvas = (DocumentCanvas)sender;

        if (!ReferenceEquals(e.OriginalSource, this))
        {
            return;
        }

        if (e.Key != Key.Return)
        {
            return;
        }

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

        var splitPosition = (int)wordSplitterPosition.X;

        // Reset tool, which will clear selection.
        canvas.ActiveTool = DocumentCanvasTools.SelectionTool;

        // Split the word, which selects a node, so order with previous statement matters.
        canvas.OnWordSplit(node, splitPosition, (first, second));
    }
}
