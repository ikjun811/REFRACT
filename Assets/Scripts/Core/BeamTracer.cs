using System.Collections.Generic;
using System.Linq;

namespace Refract.Core
{
    /// <summary>빔이 멈춘 이유.</summary>
    public enum BeamEnd { ExitedBoard, HitWall, HitReceiver, HitEmitter, LoopDetected }

    /// <summary>광원 하나에서 나온 빔의 경로.</summary>
    public sealed class BeamPath
    {
        public GridPos Origin { get; }
        /// <summary>빔이 지나간 칸 목록(광원 칸 제외, 멈춘 칸 포함).</summary>
        public List<GridPos> Cells { get; } = new List<GridPos>();
        public BeamEnd End { get; internal set; }
        /// <summary>마지막으로 진행하던 방향 (보드 밖으로 나간 빔을 그릴 때 사용).</summary>
        public Direction FinalDirection { get; internal set; }

        public BeamPath(GridPos origin) => Origin = origin;
    }

    public sealed class TraceResult
    {
        public List<BeamPath> Beams { get; } = new List<BeamPath>();
        public HashSet<GridPos> LitReceivers { get; } = new HashSet<GridPos>();
        public int ReceiverCount { get; internal set; }

        /// <summary>수신기가 하나 이상 있고, 모두 점등되면 클리어.</summary>
        public bool IsCleared => ReceiverCount > 0 && LitReceivers.Count == ReceiverCount;
    }

    /// <summary>보드 상태로부터 모든 빔의 경로와 수신기 점등 여부를 계산한다.</summary>
    public static class BeamTracer
    {
        /// <summary>
        /// 거울 반사 규칙.
        /// '/' : Right↔Up, Left↔Down
        /// '\' : Right↔Down, Left↔Up
        /// </summary>
        public static Direction Reflect(Direction incoming, MirrorOrientation mirror)
        {
            if (mirror == MirrorOrientation.Slash)
            {
                switch (incoming)
                {
                    case Direction.Right: return Direction.Up;
                    case Direction.Up: return Direction.Right;
                    case Direction.Left: return Direction.Down;
                    default: return Direction.Left; // Down
                }
            }

            switch (incoming)
            {
                case Direction.Right: return Direction.Down;
                case Direction.Down: return Direction.Right;
                case Direction.Left: return Direction.Up;
                default: return Direction.Left; // Up
            }
        }

        public static TraceResult Trace(Board board)
        {
            var result = new TraceResult
            {
                ReceiverCount = board.Pieces.Values.Count(p => p.Type == PieceType.Receiver)
            };

            var emitters = board.Pieces.Values
                .Where(p => p.Type == PieceType.Emitter)
                .OrderBy(p => p.Position.Y).ThenBy(p => p.Position.X); // 결과 순서를 항상 같게

            foreach (var emitter in emitters)
                result.Beams.Add(TraceOne(board, emitter, result.LitReceivers));

            return result;
        }

        private static BeamPath TraceOne(Board board, Piece emitter, HashSet<GridPos> litReceivers)
        {
            var path = new BeamPath(emitter.Position);
            var pos = emitter.Position;
            var dir = emitter.Facing;

            // 같은 칸을 같은 방향으로 다시 지나면 무한 루프 → 중단
            var visited = new HashSet<(GridPos, Direction)>();

            while (true)
            {
                var next = pos + dir.Offset();
                path.FinalDirection = dir;

                if (!board.IsInside(next))
                {
                    path.End = BeamEnd.ExitedBoard;
                    return path;
                }

                if (!visited.Add((next, dir)))
                {
                    path.End = BeamEnd.LoopDetected;
                    return path;
                }

                path.Cells.Add(next);
                pos = next;

                var piece = board.GetPiece(pos);
                if (piece == null) continue;

                switch (piece.Type)
                {
                    case PieceType.Wall:
                        path.End = BeamEnd.HitWall;
                        return path;
                    case PieceType.Emitter:
                        path.End = BeamEnd.HitEmitter;
                        return path;
                    case PieceType.Receiver:
                        litReceivers.Add(pos);
                        path.End = BeamEnd.HitReceiver;
                        return path;
                    case PieceType.Mirror:
                        dir = Reflect(dir, piece.Orientation);
                        break;
                }
            }
        }
    }
}
