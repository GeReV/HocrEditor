using System;

namespace HocrEditor.Controls;

[Flags]
internal enum CardinalDirections
{
    North = 1 << 1,
    East = 1 << 2,
    South = 1 << 3,
    West = 1 << 4,
    NorthWest = North | West,
    NorthEast = North | East,
    SouthEast = South | East,
    SouthWest = South | West,
}