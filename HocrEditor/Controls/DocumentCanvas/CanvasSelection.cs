using System;
using System.Collections.Generic;
using HocrEditor.Helpers;
using SkiaSharp;

namespace HocrEditor.Controls;

internal sealed class CanvasSelection : IDisposable
{
    private static readonly SKPaint HandleFillPaint = new()
    {
        Style = SKPaintStyle.Fill,
        Color = SKColors.White,
    };

    private static readonly SKPaint HandleStrokePaint = new()
    {
        Style = SKPaintStyle.Stroke,
        Color = SKColors.Gray,
        StrokeWidth = 0,
    };

    private SKRectI bounds;
    private readonly ResizeHandle[] resizeHandles;

    public CanvasSelection()
    {
        resizeHandles = new[]
        {
            // Clockwise from top-left.
            new ResizeHandle(Bounds.Location, CardinalDirections.NorthWest),
            new ResizeHandle(new SKPoint(Bounds.MidX, Bounds.Top), CardinalDirections.North),
            new ResizeHandle(new SKPoint(Bounds.Right, Bounds.Top), CardinalDirections.NorthEast),
            new ResizeHandle(new SKPoint(Bounds.Right, Bounds.MidY), CardinalDirections.East),
            new ResizeHandle(new SKPoint(Bounds.Right, Bounds.Bottom), CardinalDirections.SouthEast),
            new ResizeHandle(new SKPoint(Bounds.MidX, Bounds.Bottom), CardinalDirections.South),
            new ResizeHandle(new SKPoint(Bounds.Left, Bounds.Bottom), CardinalDirections.SouthWest),
            new ResizeHandle(new SKPoint(Bounds.Left, Bounds.MidY), CardinalDirections.West),
        };
    }

    public SKRectI InitialBounds { get; private set; } = SKRectI.Empty;

    public SKRectI Bounds
    {
        get => bounds;
        set => bounds = value;
    }

    public IEnumerable<ResizeHandle> ResizeHandles
    {
        get
        {
            CalculateRectResizeHandles(bounds);

            return resizeHandles;
        }
    }

    public bool ShouldShowCanvasSelection => Math.Abs(Width) > 0 || Math.Abs(Height) > 0;

    public bool IsEmpty => Bounds.IsEmpty;

    public int Left
    {
        get => bounds.Left;
        set => bounds.Left = value;
    }

    public int Top
    {
        get => bounds.Top;
        set => bounds.Top = value;
    }

    public int Right
    {
        get => bounds.Right;
        set => bounds.Right = value;
    }

    public int Bottom
    {
        get => bounds.Bottom;
        set => bounds.Bottom = value;
    }

    public int Width => bounds.Width;

    public int Height => bounds.Height;

    public float MidX => bounds.MidX;
    public float MidY => bounds.MidY;

    public SKSize Size => bounds.Size;

    public SKPoint Center => new(bounds.MidX, bounds.MidY);

    public SKPoint ResizeRatio
    {
        get
        {
            if (InitialBounds.IsEmpty)
            {
                throw new InvalidOperationException("BeginResize has not been called.");
            }

            var w = bounds.Width / (float)InitialBounds.Width;
            var h = bounds.Height / (float)InitialBounds.Height;

            return new SKPoint(w, h);
        }
    }

    public void Render(SKCanvas canvas, SKColor color = default)
    {
        if (!ShouldShowCanvasSelection)
        {
            return;
        }

        var bbox = Bounds;

        var path = new SKPath();

        path.MoveTo(bbox.Left, bbox.Top);
        path.LineTo(bbox.Left, bbox.Bottom);
        path.LineTo(bbox.Right, bbox.Bottom);
        path.LineTo(bbox.Right, bbox.Top);
        path.Close();

        canvas.DrawDashedPath(path, color);

        foreach (var handle in ResizeHandles)
        {
            RenderScalingHandle(canvas, handle);
        }
    }

    private static void RenderScalingHandle(SKCanvas canvas, ResizeHandle handle)
    {
        var rect = handle.GetRect();

        canvas.DrawRect(
            rect,
            HandleFillPaint
        );
        canvas.DrawRect(
            rect,
            HandleStrokePaint
        );
    }

    public void BeginResize()
    {
        InitialBounds = bounds;
    }

    public void EndResize()
    {
        InitialBounds = SKRectI.Empty;

        // Ensure our final size has positive width and height, if it was flipped during resize.
        bounds = bounds.Standardized;
    }

    private void CalculateRectResizeHandles(SKRect r)
    {
        resizeHandles[0].Center = r.Location;
        resizeHandles[1].Center = new SKPoint(r.MidX, r.Top);
        resizeHandles[2].Center = new SKPoint(r.Right, r.Top);
        resizeHandles[3].Center = new SKPoint(r.Right, r.MidY);
        resizeHandles[4].Center = new SKPoint(r.Right, r.Bottom);
        resizeHandles[5].Center = new SKPoint(r.MidX, r.Bottom);
        resizeHandles[6].Center = new SKPoint(r.Left, r.Bottom);
        resizeHandles[7].Center = new SKPoint(r.Left, r.MidY);
    }

    public void Dispose()
    {
        HandleFillPaint.Dispose();
        HandleStrokePaint.Dispose();
    }
}
