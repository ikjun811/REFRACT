using System.Collections.Generic;
using System.Linq;

namespace Refract.Core
{
    /// <summary>빔이 멈춘 이유.</summary>
    public enum BeamEnd { ExitedBoard, HitWall, HitReceiver, HitEmitter, LoopDetected }

    /// <summary>빔 한 가닥의 경로. 광원에서 시작하거나, 분산기에서 갈라져 나온 가지.</summary>
    public sealed class BeamPath
    {
        /// <summary>시작 칸 (광원 또는 분산기 위치).</summary>
        public GridPos Origin { get; }
        /// <summary>빔이 지나간 칸 목록(시작 칸 제외, 멈춘 칸 포함).</summary>
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

        /// <summary>분산기로 갈라지는 빔 가지 수 상한 (안전장치).</summary>
        public const int MaxBranchesPerEmitter = 64;

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
                TraceEmitter(board, emitter, result);

            return result;
        }

        /// <summary>
        /// 광원 하나에서 나온 빔을 추적한다.
        /// 분산기를 만나면 반사 빔을 새 가지로 큐에 넣고, 현재 빔은 직진을 계속한다.
        /// </summary>
        private static void TraceEmitter(Board board, Piece emitter, TraceResult result)
        {
            // 같은 칸을 같은 방향으로 다시 지나면 무한 루프 → 그 가지는 중단
            var visited = new HashSet<(GridPos, Direction)>();
            var branches = new Queue<(GridPos origin, Direction dir)>();
            branches.Enqueue((emitter.Position, emitter.Facing));
            var branchCount = 0;

            while (branches.Count > 0 && branchCount < MaxBranchesPerEmitter)
            {
                var (origin, startDir) = branches.Dequeue();
                branchCount++;
                result.Beams.Add(TraceBranch(board, origin, startDir, visited, branches, result.LitReceivers));
            }
        }

        private static BeamPath TraceBranch(Board board, GridPos origin, Direction dir,
            HashSet<(GridPos, Direction)> visited, Queue<(GridPos, Direction)> branches, HashSet<GridPos> litReceivers)
        {
            var path = new BeamPath(origin);
            var pos = origin;

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
                    case PieceType.Splitter:
                        // 반사 빔은 새 가지로, 현재 빔은 그대로 직진
                        branches.Enqueue((pos, Reflect(dir, piece.Orientation)));
                        break;
                }
            }
        }
    }
}
