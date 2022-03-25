using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using SkiaSharp;

namespace HocrEditor.Models
{
    public struct Rect : IEquatable<Rect>
    {
        public static Rect FromSKRect(SKRect rect) => new(
            (int)rect.Left,
            (int)rect.Top,
            (int)rect.Right,
            (int)rect.Bottom
        );

        public static Rect FromBboxAttribute(string values)
        {
            var coordinates = values.Split(' ', StringSplitOptions.TrimEntries).Select(int.Parse).ToArray();

            Debug.Assert(coordinates.Length == 4, "coordinates.Length == 4");

            return new Rect(coordinates[0], coordinates[1], coordinates[2], coordinates[3]);
        }

        public static string ToBboxAttribute(Rect rect) => $"bbox {rect.Left} {rect.Top} {rect.Right} {rect.Bottom}";

        public Rect(int left, int top, int right, int bottom) : this()
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        /// <summary>Represents a new instance of the <see cref="T:HocrEditor.Models.BoundingBox" /> class with member data left uninitialized.</summary>
        /// <remarks />
        public static readonly Rect Empty = new();

        private int left;
        private int top;
        private int right;
        private int bottom;

        /// <summary>Gets the x-coordinate of the middle of this rectangle.</summary>
        /// <value />
        /// <remarks />
        public readonly int MidX => (int)(left + Width / 2f);

        /// <summary>Gets the y-coordinate of the middle of this rectangle.</summary>
        /// <value />
        /// <remarks />
        public readonly int MidY => (int)(top + Height / 2f);

        /// <summary>Gets the width of the rectangle.</summary>
        /// <value />
        /// <remarks />
        public readonly int Width => right - left;

        /// <summary>Gets the height of the <see cref="T:HocrEditor.Models.BoundingBox" />.</summary>
        /// <value />
        /// <remarks />
        public readonly int Height => bottom - top;

        /// <summary>Gets a value indicating whether this rectangle has a zero size and location.</summary>
        /// <value />
        /// <remarks />
        public readonly bool IsEmpty => this == Empty;

        /// <summary>Gets or sets the size of the rectangle.</summary>
        /// <value />
        /// <remarks />
        public Size Size
        {
            readonly get => new(Width, Height);
            set
            {
                right = (int)(left + value.Width);
                bottom = (int)(top + value.Height);
            }
        }

        /// <summary>Gets or sets the offset of the rectangle.</summary>
        /// <value />
        /// <remarks />
        public Point Location
        {
            readonly get => new(left, top);
            set => this = Create(value, Size);
        }

        /// <param name="rect">The <see cref="T:HocrEditor.Models.BoundingBox" /> to be copied. This rectangle is not modified.</param>
        /// <param name="x">The amount to enlarge the copy of the rectangle horizontally.</param>
        /// <param name="y">The amount to enlarge the copy of the rectangle vertically.</param>
        /// <summary>Creates and returns an enlarged copy of the specified <see cref="T:HocrEditor.Models.BoundingBox" /> structure. The copy is enlarged by the specified amount and the original rectangle remains unmodified.</summary>
        /// <returns>The enlarged <see cref="T:HocrEditor.Models.BoundingBox" />.</returns>
        /// <remarks />
        public static Rect Inflate(Rect rect, int x, int y)
        {
            var boundingBox = new Rect(rect.left, rect.top, rect.right, rect.bottom);
            boundingBox.Inflate(x, y);
            return boundingBox;
        }

        /// <param name="size">The amount to inflate this <see cref="T:HocrEditor.Models.BoundingBox" />.</param>
        /// <summary>Enlarges this <see cref="T:HocrEditor.Models.BoundingBox" /> structure by the specified amount.</summary>
        /// <remarks />
        public void Inflate(Size size) => Inflate((int)size.Width, (int)size.Height);

        /// <param name="x">The amount to inflate this <see cref="T:HocrEditor.Models.BoundingBox" /> structure horizontally.</param>
        /// <param name="y">The amount to inflate this <see cref="T:HocrEditor.Models.BoundingBox" /> structure vertically.</param>
        /// <summary>Enlarges this <see cref="T:HocrEditor.Models.BoundingBox" /> structure by the specified amount.</summary>
        /// <remarks />
        public void Inflate(int x, int y)
        {
            left -= x;
            top -= y;
            right += x;
            bottom += y;
        }

        /// <param name="a">A rectangle to intersect.</param>
        /// <param name="b">A rectangle to intersect.</param>
        /// <summary>Returns a <see cref="T:HocrEditor.Models.BoundingBox" /> structure that represents the intersection of two rectangles. If there is no intersection, and empty <see cref="T:HocrEditor.Models.BoundingBox" /> is returned.</summary>
        /// <returns>A third <see cref="T:HocrEditor.Models.BoundingBox" /> structure the size of which represents the overlapped area of the two specified rectangles.</returns>
        /// <remarks />
        public static Rect Intersect(Rect a, Rect b) => !a.IntersectsWithInclusive(b)
            ? Empty
            : new Rect(
                Math.Max(a.left, b.left),
                Math.Max(a.top, b.top),
                Math.Min(a.right, b.right),
                Math.Min(a.bottom, b.bottom)
            );

        /// <param name="rect">The rectangle to intersect.</param>
        /// <summary>Replaces this <see cref="T:HocrEditor.Models.BoundingBox" /> structure with the intersection of itself and the specified <see cref="T:HocrEditor.Models.BoundingBox" /> structure.</summary>
        /// <remarks />
        public void Intersect(Rect rect) => this = Intersect(this, rect);

        /// <param name="a">A rectangle to union.</param>
        /// <param name="b">A rectangle to union.</param>
        /// <summary>Creates the smallest possible third rectangle that can contain both of two rectangles that form a union.</summary>
        /// <returns>A third <see cref="T:HocrEditor.Models.BoundingBox" /> structure that contains both of the two rectangles that form the union.</returns>
        /// <remarks />
        public static Rect Union(Rect a, Rect b) => new(
            Math.Min(a.left, b.left),
            Math.Min(a.top, b.top),
            Math.Max(a.right, b.right),
            Math.Max(a.bottom, b.bottom)
        );

        /// <param name="rect">A rectangle to union.</param>
        /// <summary>Replaces this <see cref="T:HocrEditor.Models.BoundingBox" /> structure with the union of itself and the specified <see cref="T:HocrEditor.Models.BoundingBox" /> structure.</summary>
        /// <remarks />
        public void Union(Rect rect) => this = Union(this, rect);

        /// <param name="r">The <see cref="T:HocrEditor.Models.BoundingBoxI" /> structure to convert.</param>
        /// <summary>Converts the specified <see cref="T:HocrEditor.Models.BoundingBoxI" /> structure to a <see cref="T:HocrEditor.Models.BoundingBox" /> structure.</summary>
        /// <returns>The <see cref="T:HocrEditor.Models.BoundingBox" /> structure that is converted from the specified <see cref="T:HocrEditor.Models.BoundingBoxI" /> structure.</returns>
        /// <remarks />
        public static implicit operator Rect(SKRectI r) => new(
            r.Left,
            r.Top,
            r.Right,
            r.Bottom
        );

        public static explicit operator Rect(SKRect r) => new(
            (int)r.Left,
            (int)r.Top,
            (int)r.Right,
            (int)r.Bottom
        );

        /// <param name="x">The x-coordinate.</param>
        /// <param name="y">The y-coordinate.</param>
        /// <summary>Determines whether the specified coordinates are inside this rectangle.</summary>
        /// <returns>Returns true if the coordinates are inside this rectangle, otherwise false.</returns>
        /// <remarks />
        public readonly bool Contains(double x, double y) => x >= left &&
                                                             x < right &&
                                                             y >= top &&
                                                             y < bottom;

        /// <param name="pt">The point to test.</param>
        /// <summary>Determines whether the specified point is inside this rectangle.</summary>
        /// <returns>Returns true if the point is inside this rectangle, otherwise false.</returns>
        /// <remarks />
        public readonly bool Contains(Point pt) => Contains(pt.X, pt.Y);

        /// <param name="rect">The rectangle to test.</param>
        /// <summary>Determines whether the specified rectangle is inside this rectangle.</summary>
        /// <returns>Returns true if the rectangle is inside this rectangle, otherwise false.</returns>
        /// <remarks />
        public readonly bool Contains(Rect rect) => left <= rect.left &&
                                                           right >= rect.right &&
                                                           top <= rect.top &&
                                                           bottom >= rect.bottom;

        /// <param name="rect">The rectangle to test.</param>
        /// <summary>Determines if this rectangle intersects with another rectangle.</summary>
        /// <returns>This method returns true if there is any intersection.</returns>
        /// <remarks />
        public readonly bool IntersectsWith(Rect rect) => left < rect.right &&
                                                                 right > rect.left &&
                                                                 top < rect.bottom &&
                                                                 bottom > rect.top;

        /// <param name="rect">The rectangle to test.</param>
        /// <summary>Determines if this rectangle intersects with another rectangle.</summary>
        /// <returns>This method returns true if there is any intersection.</returns>
        /// <remarks />
        public readonly bool IntersectsWithInclusive(Rect rect) => left <= rect.right &&
                                                                          right >= rect.left &&
                                                                          top <= rect.bottom &&
                                                                          bottom >= rect.top;

        /// <param name="x">The amount to offset the location horizontally.</param>
        /// <param name="y">The amount to offset the location vertically.</param>
        /// <summary>Translates the this rectangle by the specified amount.</summary>
        /// <remarks />
        public void Offset(int x, int y)
        {
            left += x;
            top += y;
            right += x;
            bottom += y;
        }

        /// <param name="pos">The amount to offset the rectangle.</param>
        /// <summary>Translates the this rectangle by the specified amount.</summary>
        /// <remarks />
        public void Offset(Point pos) => Offset((int)pos.X, (int)pos.Y);

        /// <summary>Converts this <see cref="T:HocrEditor.Models.BoundingBox" /> to a human readable string.</summary>
        /// <returns>A string that represents this <see cref="T:HocrEditor.Models.BoundingBox" />.</returns>
        /// <remarks />
        public readonly override string ToString() => $"{{Left={Left},Top={Top},Width={Width},Height={Height}}}";

        public readonly string ToBboxAttribute() => ToBboxAttribute(this);

        /// <param name="location">The rectangle location.</param>
        /// <param name="size">The rectangle size.</param>
        /// <summary>Creates a new rectangle with the specified location and size.</summary>
        /// <returns>Returns the new rectangle.</returns>
        /// <remarks />
        public static Rect Create(Point location, Size size) =>
            Create((int)location.X, (int)location.Y, (int)size.Width, (int)size.Height);

        /// <param name="size">The rectangle size.</param>
        /// <summary>Creates a new rectangle with the specified size.</summary>
        /// <returns>Returns the new rectangle.</returns>
        /// <remarks />
        public static Rect Create(Size size) => Create(new Point(), size);

        /// <param name="width">The rectangle width.</param>
        /// <param name="height">The rectangle height.</param>
        /// <summary>Creates a new rectangle with the specified size.</summary>
        /// <returns>Returns the new rectangle.</returns>
        /// <remarks />
        public static Rect Create(int width, int height) =>
            new(0, 0, width, height);

        /// <param name="x">The x-coordinate.</param>
        /// <param name="y">The y-coordinate.</param>
        /// <param name="width">The rectangle width.</param>
        /// <param name="height">The rectangle height.</param>
        /// <summary>Creates a new rectangle with the specified location and size.</summary>
        /// <returns>Returns the new rectangle.</returns>
        /// <remarks />
        public static Rect Create(int x, int y, int width, int height) =>
            new(x, y, x + width, y + height);

        /// <summary>Gets or sets the x-coordinate of the left edge of this <see cref="T:HocrEditor.Models.BoundingBox" /> structure.</summary>
        /// <value />
        /// <remarks />
        public int Left
        {
            readonly get => left;
            set => left = value;
        }

        /// <summary>Gets or sets the y-coordinate of the top edge of this <see cref="T:HocrEditor.Models.BoundingBox" /> structure.</summary>
        /// <value />
        /// <remarks />
        public int Top
        {
            readonly get => top;
            set => top = value;
        }

        /// <summary>Gets or sets the x-coordinate of the right edge of this <see cref="T:HocrEditor.Models.BoundingBox" /> structure.</summary>
        /// <value />
        /// <remarks />
        public int Right
        {
            readonly get => right;
            set => right = value;
        }

        /// <summary>Gets or sets the y-coordinate of the bottom edge of this <see cref="T:HocrEditor.Models.BoundingBox" /> structure.</summary>
        /// <value />
        /// <remarks />
        public int Bottom
        {
            readonly get => bottom;
            set => bottom = value;
        }

        // ReSharper disable once InconsistentNaming
        public SKRect ToSKRect() => new(Left, Top, Right, Bottom);

        // ReSharper disable once InconsistentNaming
        public SKRectI ToSKRectI() => new(Left, Top, Right, Bottom);

        /// <param name="left">The <see cref="T:HocrEditor.Models.BoundingBox" /> structure that is to the left of the equality operator.</param>
        /// <param name="right">The <see cref="T:HocrEditor.Models.BoundingBox" /> structure that is to the right of the equality operator.</param>
        /// <summary>Tests whether two <see cref="T:HocrEditor.Models.BoundingBox" /> structures have equal coordinates.</summary>
        /// <returns>This operator returns true if the two specified <see cref="T:HocrEditor.Models.BoundingBox" /> structures have equal <see cref="P:HocrEditor.Models.BoundingBox.Left" />, <see cref="P:HocrEditor.Models.BoundingBox.Top" />, <see cref="P:HocrEditor.Models.BoundingBox.Right" />, and <see cref="P:HocrEditor.Models.BoundingBox.Bottom" /> properties.</returns>
        /// <remarks />
        public static bool operator ==(Rect left, Rect right) => left.Equals(right);

        /// <param name="left">The <see cref="T:HocrEditor.Models.BoundingBox" /> structure that is to the left of the inequality operator.</param>
        /// <param name="right">The <see cref="T:HocrEditor.Models.BoundingBox" /> structure that is to the right of the inequality operator.</param>
        /// <summary>Tests whether two <see cref="T:HocrEditor.Models.BoundingBox" /> structures differ in coordinates.</summary>
        /// <returns>This operator returns true if any of the <see cref="P:HocrEditor.Models.BoundingBox.Left" />, <see cref="P:HocrEditor.Models.BoundingBox.Top" />, <see cref="P:HocrEditor.Models.BoundingBox.Right" />, or <see cref="P:HocrEditor.Models.BoundingBox.Bottom" /> properties of the two <see cref="T:HocrEditor.Models.BoundingBox" /> structures are unequal; otherwise false.</returns>
        /// <remarks />
        public static bool operator !=(Rect left, Rect right) => !left.Equals(right);

        /// <summary>Calculates the hashcode for this rectangle.</summary>
        /// <returns>Returns the hashcode for this rectangle.</returns>
        /// <remarks />
        public readonly override int GetHashCode()
        {
            return HashCode.Combine(left, top, right, bottom);
        }

        public bool Equals(Rect other)
        {
            return left == other.left && top == other.top && right == other.right && bottom == other.bottom;
        }

        public override bool Equals(object? obj)
        {
            return obj is Rect other && Equals(other);
        }
    }
}
