using NUnit.Framework;
using Refract.Core;

namespace Refract.Tests
{
    public class BeamTracerTests
    {
        private static Board EmptyBoard(int size = 5) => new Board(size, size, mirrorsTotal: 4);

        [Test]
        public void EmptyBoard_BeamGoesStraight_AndExitsBoard()
        {
            var board = EmptyBoard();
            board.AddFixed(Piece.Emitter(new GridPos(0, 2), Direction.Right));

            var beam = BeamTracer.Trace(board).Beams[0];

            Assert.AreEqual(BeamEnd.ExitedBoard, beam.End);
            Assert.AreEqual(4, beam.Cells.Count);
            Assert.AreEqual(new GridPos(4, 2), beam.Cells[3]);
            Assert.AreEqual(Direction.Right, beam.FinalDirection);
        }

        // '/' 거울 반사 규칙 4가지
        [TestCase(Direction.Right, Direction.Up)]
        [TestCase(Direction.Up, Direction.Right)]
        [TestCase(Direction.Left, Direction.Down)]
        [TestCase(Direction.Down, Direction.Left)]
        public void SlashMirror_Reflects(Direction incoming, Direction expected)
        {
            Assert.AreEqual(expected, BeamTracer.Reflect(incoming, MirrorOrientation.Slash));
        }

        // '\' 거울 반사 규칙 4가지
        [TestCase(Direction.Right, Direction.Down)]
        [TestCase(Direction.Down, Direction.Right)]
        [TestCase(Direction.Left, Direction.Up)]
        [TestCase(Direction.Up, Direction.Left)]
        public void BackslashMirror_Reflects(Direction incoming, Direction expected)
        {
            Assert.AreEqual(expected, BeamTracer.Reflect(incoming, MirrorOrientation.Backslash));
        }

        [Test]
        public void MirrorOnBoard_TurnsBeam()
        {
            var board = EmptyBoard();
            board.AddFixed(Piece.Emitter(new GridPos(0, 0), Direction.Right));
            board.TryPlaceMirror(new GridPos(2, 0), MirrorOrientation.Slash);

            var beam = BeamTracer.Trace(board).Beams[0];

            // (1,0) → (2,0)[거울] → (2,1) → ... 위로 진행
            Assert.AreEqual(new GridPos(2, 1), beam.Cells[2]);
            Assert.AreEqual(Direction.Up, beam.FinalDirection);
        }

        [Test]
        public void Wall_StopsBeam()
        {
            var board = EmptyBoard();
            board.AddFixed(Piece.Emitter(new GridPos(0, 0), Direction.Right));
            board.AddFixed(Piece.Wall(new GridPos(2, 0)));

            var beam = BeamTracer.Trace(board).Beams[0];

            Assert.AreEqual(BeamEnd.HitWall, beam.End);
            Assert.AreEqual(new GridPos(2, 0), beam.Cells[beam.Cells.Count - 1]);
        }

        [Test]
        public void Receiver_IsLit_WhenHit()
        {
            var board = EmptyBoard();
            board.AddFixed(Piece.Emitter(new GridPos(0, 0), Direction.Right));
            board.AddFixed(Piece.Receiver(new GridPos(4, 0)));

            var result = BeamTracer.Trace(board);

            Assert.AreEqual(BeamEnd.HitReceiver, result.Beams[0].End);
            Assert.IsTrue(result.LitReceivers.Contains(new GridPos(4, 0)));
            Assert.IsTrue(result.IsCleared);
        }

        [Test]
        public void Receiver_NotLit_WhenMissed()
        {
            var board = EmptyBoard();
            board.AddFixed(Piece.Emitter(new GridPos(0, 0), Direction.Right));
            board.AddFixed(Piece.Receiver(new GridPos(4, 1)));

            var result = BeamTracer.Trace(board);

            Assert.AreEqual(0, result.LitReceivers.Count);
            Assert.IsFalse(result.IsCleared);
        }

        [Test]
        public void Clear_RequiresAllReceivers()
        {
            var board = EmptyBoard();
            board.AddFixed(Piece.Emitter(new GridPos(0, 0), Direction.Right));
            board.AddFixed(Piece.Receiver(new GridPos(4, 0)));
            board.AddFixed(Piece.Receiver(new GridPos(4, 4)));

            var result = BeamTracer.Trace(board);

            Assert.AreEqual(1, result.LitReceivers.Count);
            Assert.AreEqual(2, result.ReceiverCount);
            Assert.IsFalse(result.IsCleared);
        }

        [Test]
        public void NoReceivers_IsNeverCleared()
        {
            var board = EmptyBoard();
            board.AddFixed(Piece.Emitter(new GridPos(0, 0), Direction.Right));

            Assert.IsFalse(BeamTracer.Trace(board).IsCleared);
        }

        [Test]
        public void BeamReturningToEmitter_Terminates()
        {
            // 빔이 거울 3개를 거쳐 자기 광원으로 되돌아오는 배치 → 멈춰야 한다
            var board = EmptyBoard();
            board.AddFixed(Piece.Emitter(new GridPos(0, 0), Direction.Right));
            board.TryPlaceMirror(new GridPos(2, 0), MirrorOrientation.Slash);     // Right → Up
            board.TryPlaceMirror(new GridPos(2, 2), MirrorOrientation.Backslash); // Up → Left
            board.TryPlaceMirror(new GridPos(0, 2), MirrorOrientation.Slash);     // Left → Down

            var beam = BeamTracer.Trace(board).Beams[0];

            Assert.AreEqual(BeamEnd.HitEmitter, beam.End);
            Assert.AreEqual(new GridPos(0, 0), beam.Cells[beam.Cells.Count - 1]);
        }

        [Test]
        public void MultipleEmitters_ProduceOneBeamEach()
        {
            var board = EmptyBoard();
            board.AddFixed(Piece.Emitter(new GridPos(0, 0), Direction.Right));
            board.AddFixed(Piece.Emitter(new GridPos(0, 4), Direction.Right));

            Assert.AreEqual(2, BeamTracer.Trace(board).Beams.Count);
        }

        // ───────────── 빛 분산기 (Splitter) ─────────────

        [Test]
        public void Splitter_SplitsBeamIntoStraightAndReflected()
        {
            var board = EmptyBoard();
            board.AddFixed(Piece.Emitter(new GridPos(0, 0), Direction.Right));
            board.AddFixed(Piece.Splitter(new GridPos(2, 0), MirrorOrientation.Slash));

            var result = BeamTracer.Trace(board);

            Assert.AreEqual(2, result.Beams.Count);
            // 원래 빔: 분산기를 지나 오른쪽으로 계속 진행 후 보드 밖으로
            Assert.AreEqual(Direction.Right, result.Beams[0].FinalDirection);
            Assert.AreEqual(new GridPos(4, 0), result.Beams[0].Cells[result.Beams[0].Cells.Count - 1]);
            // 갈라진 빔: 분산기 위치에서 시작해 '/' 반사 방향(위)으로 진행
            Assert.AreEqual(new GridPos(2, 0), result.Beams[1].Origin);
            Assert.AreEqual(Direction.Up, result.Beams[1].FinalDirection);
        }

        [Test]
        public void Splitter_CanLightTwoReceivers()
        {
            var board = EmptyBoard();
            board.AddFixed(Piece.Emitter(new GridPos(0, 0), Direction.Right));
            board.AddFixed(Piece.Splitter(new GridPos(2, 0), MirrorOrientation.Slash));
            board.AddFixed(Piece.Receiver(new GridPos(4, 0)));
            board.AddFixed(Piece.Receiver(new GridPos(2, 4)));

            var result = BeamTracer.Trace(board);

            Assert.AreEqual(2, result.LitReceivers.Count);
            Assert.IsTrue(result.IsCleared);
        }

        [Test]
        public void Splitter_LoopTerminates()
        {
            // 분산기에서 갈라진 빔이 거울 3개를 돌아 분산기로 되돌아오는 순환 구조 → 반드시 끝나야 한다
            var board = EmptyBoard();
            board.AddFixed(Piece.Emitter(new GridPos(0, 1), Direction.Right));
            board.AddFixed(Piece.Splitter(new GridPos(1, 1), MirrorOrientation.Slash));
            board.TryPlaceMirror(new GridPos(3, 1), MirrorOrientation.Slash);     // Right → Up
            board.TryPlaceMirror(new GridPos(3, 3), MirrorOrientation.Backslash); // Up → Left
            board.TryPlaceMirror(new GridPos(1, 3), MirrorOrientation.Slash);     // Left → Down (분산기로 복귀)

            var result = BeamTracer.Trace(board);

            Assert.LessOrEqual(result.Beams.Count, BeamTracer.MaxBranchesPerEmitter);
            Assert.IsTrue(result.Beams.Exists(b => b.End == BeamEnd.LoopDetected || b.End == BeamEnd.HitEmitter));
        }
    }
}
