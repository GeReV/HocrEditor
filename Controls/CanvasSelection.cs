using System;
using System.Collections.Generic;
using SkiaSharp;

namespace HocrEditor.Controls;

internal class CanvasSelection : IDisposable
{
    private static readonly SKPaint HandleFillPaint = new()
    {
        IsStroke = false,
        Color = SKColors.White,
        StrokeWidth = 1,
    };

    private static readonly SKPaint HandleStrokePaint = new()
    {
        IsStroke = true,
        Color = SKColors.Gray,
        StrokeWidth = 1,
    };

    private const float SELECTION_DASH_LENGTH = 5f;

    private static readonly SKPaint SelectionDashPaint = new()
    {
        IsStroke = true,
        Color = SKColors.Black,
        StrokeWidth = 1,
        PathEffect = SKPathEffect.CreateDash(new[] { SELECTION_DASH_LENGTH, SELECTION_DASH_LENGTH }, 0f)
    };

    private static readonly SKPaint SelectionBackgroundPaint = new()
    {
        IsStroke = true,
        Color = SKColors.White,
        StrokeWidth = 1,
    };

    private SKRect bounds;
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

    public SKRect InitialBounds { get; private set; } = SKRect.Empty;

    public SKRect Bounds
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

    public float Left
    {
        get => bounds.Left;
        set => bounds.Left = value;
    }

    public float Top
    {
        get => bounds.Top;
        set => bounds.Top = value;
    }

    public float Right
    {
        get => bounds.Right;
        set => bounds.Right = value;
    }

    public float Bottom
    {
        get => bounds.Bottom;
        set => bounds.Bottom = value;
    }

    public float Width => bounds.Width;

    public float Height => bounds.Height;

    public float MidX => bounds.MidX;
    public float MidY => bounds.MidY;

    public SKSize Size => bounds.Size;

    public SKPoint Center => new(bounds.MidX, bounds.MidY);

    public SKPoint ResizeRatio {
        get
        {
            if (InitialBounds.IsEmpty)
            {
                throw new InvalidOperationException("BeginResize has not been called.");
            }

            var w = bounds.Width / InitialBounds.Width;
            var h = bounds.Height / InitialBounds.Height;

            if (Math.Abs(InitialBounds.Width) < float.Epsilon)
            {
                w = 0;
            }

            if (Math.Abs(InitialBounds.Height) < float.Epsilon)
            {
                h = 0;
            }

            return new SKPoint(w, h);
        }
    }

    public void Render(SKCanvas canvas, SKMatrix transformation)
    {
        if (!ShouldShowCanvasSelection)
        {
            return;
        }

        var bbox = transformation.MapRect(Bounds);



        var path = new SKPath();

        path.MoveTo(bbox.Left, bbox.Top);
        path.LineTo(bbox.Left, bbox.Bottom);
        path.LineTo(bbox.Right, bbox.Bottom);
        path.LineTo(bbox.Right, bbox.Top);
        path.Close();

        canvas.DrawRect(bbox, SelectionBackgroundPaint);
        canvas.DrawPath(path, SelectionDashPaint);

        foreach (var handle in ResizeHandles)
        {
            RenderScalingHandle(canvas, transformation, handle);
        }
    }

    private void RenderScalingHandle(SKCanvas canvas, SKMatrix transformation, ResizeHandle handle)
    {
        var rect = handle.GetRect(transformation);

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
        InitialBounds = SKRect.Empty;

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
        SelectionBackgroundPaint.Dispose();
        SelectionDashPaint.Dispose();
    }
}
