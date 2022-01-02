using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using SkiaSharp;

namespace HocrEditor.Models
{
    public class BoundingBox : IEquatable<BoundingBox>
    {
        public static BoundingBox FromBboxAttribute(string values)
        {
            var coordinates = values.Split(' ', StringSplitOptions.TrimEntries).Select(int.Parse).ToArray();

            Debug.Assert(coordinates.Length == 4, "coordinates.Length == 4");

            return new BoundingBox(coordinates[0], coordinates[1], coordinates[2], coordinates[3]);
        }

        public BoundingBox(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }

        public int Width => Right - Left;

        public int Height => Bottom - Top;

        public Point Position
        {
            get => new(Left, Top);
            set
            {
                Left = (int)value.X;
                Top = (int)value.Y;
            }
        }

        public void MoveTo(Point p)
        {
            var w = Width;
            var h = Height;

            Left = (int)p.X;
            Right = (int)(p.X + w);
            Top = (int)p.Y;
            Bottom = (int)(p.Y + h);
        }

        public void Translate(Vector v)
        {
            Left += (int)v.X;
            Right += (int)v.X;
            Top += (int)v.Y;
            Bottom += (int)v.Y;
        }

        public SKRect ToSKRect() => new SKRect(Left, Top, Right, Bottom);

        public bool Equals(BoundingBox? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((BoundingBox)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Left, Top, Right, Bottom);
        }
    }
}
