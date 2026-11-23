using NUnit.Framework;
using Refract.Core;

namespace Refract.Tests
{
    public class BoardTests
    {
        private Board _board;

        [SetUp]
        public void SetUp()
        {
            _board = new Board(5, 5, mirrorsTotal: 2);
            _board.AddFixed(Piece.Wall(new GridPos(1, 1)));
        }

        [Test]
        public void PlaceMirror_DecreasesInventory()
        {
            Assert.IsTrue(_board.TryPlaceMirror(new GridPos(0, 0), MirrorOrientation.Slash));
            Assert.AreEqual(1, _board.MirrorsLeft);
        }

        [Test]
        public void PlaceMirror_OnFixedCell_Fails()
        {
            Assert.IsFalse(_board.TryPlaceMirror(new GridPos(1, 1), MirrorOrientation.Slash));
            Assert.AreEqual(2, _board.MirrorsLeft);
        }

        [Test]
        public void PlaceMirror_OutOfBounds_Fails()
        {
            Assert.IsFalse(_board.TryPlaceMirror(new GridPos(5, 0), MirrorOrientation.Slash));
            Assert.IsFalse(_board.TryPlaceMirror(new GridPos(-1, 0), MirrorOrientation.Slash));
        }

        [Test]
        public void PlaceMirror_WithoutInventory_Fails()
        {
            _board.TryPlaceMirror(new GridPos(0, 0), MirrorOrientation.Slash);
            _board.TryPlaceMirror(new GridPos(0, 1), MirrorOrientation.Slash);

            Assert.IsFalse(_board.TryPlaceMirror(new GridPos(0, 2), MirrorOrientation.Slash));
            Assert.AreEqual(0, _board.MirrorsLeft);
        }

        [Test]
        public void Remove_RestoresInventory()
        {
            _board.TryPlaceMirror(new GridPos(0, 0), MirrorOrientation.Slash);

            Assert.IsTrue(_board.TryRemove(new GridPos(0, 0)));
            Assert.AreEqual(2, _board.MirrorsLeft);
            Assert.IsNull(_board.GetPiece(new GridPos(0, 0)));
        }

        [Test]
        public void FixedPiece_CannotBeRemovedOrRotated()
        {
            Assert.IsFalse(_board.TryRemove(new GridPos(1, 1)));
            Assert.IsFalse(_board.TryRotate(new GridPos(1, 1)));
            Assert.AreEqual(CellAction.None, _board.Interact(new GridPos(1, 1)));
        }

        [Test]
        public void Interact_CyclesPlaceRotateRemove()
        {
            var pos = new GridPos(3, 3);

            Assert.AreEqual(CellAction.PlacedSlash, _board.Interact(pos));
            Assert.AreEqual(MirrorOrientation.Slash, _board.GetPiece(pos).Orientation);

            Assert.AreEqual(CellAction.RotatedToBackslash, _board.Interact(pos));
            Assert.AreEqual(MirrorOrientation.Backslash, _board.GetPiece(pos).Orientation);

            Assert.AreEqual(CellAction.Removed, _board.Interact(pos));
            Assert.IsNull(_board.GetPiece(pos));
            Assert.AreEqual(2, _board.MirrorsLeft);
        }

        [Test]
        public void ResetPlayerPieces_KeepsFixedPieces()
        {
            _board.TryPlaceMirror(new GridPos(0, 0), MirrorOrientation.Slash);
            _board.TryPlaceMirror(new GridPos(2, 2), MirrorOrientation.Backslash);

            _board.ResetPlayerPieces();

            Assert.AreEqual(2, _board.MirrorsLeft);
            Assert.AreEqual(1, _board.Pieces.Count);
            Assert.IsNotNull(_board.GetPiece(new GridPos(1, 1)));
        }
    }
}
