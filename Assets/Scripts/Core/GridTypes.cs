using System;

namespace Refract.Core
{
    /// <summary>빔이 이동하는 4방향. 좌표계는 y가 위로 증가한다.</summary>
    public enum Direction { Up, Right, Down, Left }

    /// <summary>거울 방향. Slash = '/', Backslash = '\'</summary>
    public enum MirrorOrientation { Slash, Backslash }

    public static class DirectionExtensions
    {
        public static GridPos Offset(this Direction dir)
        {
            switch (dir)
            {
                case Direction.Up: return new GridPos(0, 1);
                case Direction.Right: return new GridPos(1, 0);
                case Direction.Down: return new GridPos(0, -1);
                case Direction.Left: return new GridPos(-1, 0);
                default: throw new ArgumentOutOfRangeException(nameof(dir), dir, null);
            }
        }
    }

    /// <summary>그리드 칸 좌표 (값 타입, Dictionary/HashSet 키로 사용).</summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Y;

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static GridPos operator +(GridPos a, GridPos b) => new GridPos(a.X + b.X, a.Y + b.Y);
        public static bool operator ==(GridPos a, GridPos b) => a.Equals(b);
        public static bool operator !=(GridPos a, GridPos b) => !a.Equals(b);

        public bool Equals(GridPos other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPos other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";
    }
}
