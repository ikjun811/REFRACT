using System;
using System.Collections.Generic;
using System.Linq;

namespace Refract.Core
{
    /// <summary>클릭 한 번이 칸에 일으킨 결과.</summary>
    public enum CellAction { None, PlacedSlash, RotatedToBackslash, Removed }

    /// <summary>
    /// 퍼즐 보드. 고정 장치와 플레이어 거울, 남은 거울 개수를 관리한다.
    /// 플레이어 조작은 칸 클릭 한 가지: 빈 칸 → '/' 배치 → '\' 회전 → 회수 순서로 순환.
    /// </summary>
    public sealed class Board
    {
        private readonly Dictionary<GridPos, Piece> _pieces = new Dictionary<GridPos, Piece>();

        public int Width { get; }
        public int Height { get; }
        public int MirrorsTotal { get; }
        public int MirrorsLeft { get; private set; }

        public IReadOnlyDictionary<GridPos, Piece> Pieces => _pieces;

        public Board(int width, int height, int mirrorsTotal)
        {
            if (width < 1 || height < 1) throw new ArgumentException("보드 크기는 1 이상이어야 합니다.");
            if (mirrorsTotal < 0) throw new ArgumentException("거울 개수는 0 이상이어야 합니다.");
            Width = width;
            Height = height;
            MirrorsTotal = mirrorsTotal;
            MirrorsLeft = mirrorsTotal;
        }

        public bool IsInside(GridPos pos) => pos.X >= 0 && pos.Y >= 0 && pos.X < Width && pos.Y < Height;

        public Piece GetPiece(GridPos pos) => _pieces.TryGetValue(pos, out var piece) ? piece : null;

        /// <summary>레벨 데이터의 고정 장치를 추가한다. 범위 밖이거나 겹치면 예외.</summary>
        public void AddFixed(Piece piece)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            if (!piece.IsFixed) throw new ArgumentException("AddFixed에는 고정 장치만 넣을 수 있습니다.");
            if (!IsInside(piece.Position)) throw new ArgumentException($"보드 밖 좌표: {piece.Position}");
            if (_pieces.ContainsKey(piece.Position)) throw new ArgumentException($"이미 장치가 있는 칸: {piece.Position}");
            _pieces.Add(piece.Position, piece);
        }

        public bool TryPlaceMirror(GridPos pos, MirrorOrientation orientation)
        {
            if (!IsInside(pos) || _pieces.ContainsKey(pos) || MirrorsLeft <= 0) return false;
            _pieces.Add(pos, Piece.Mirror(pos, orientation, isFixed: false));
            MirrorsLeft--;
            return true;
        }

        public bool TryRotate(GridPos pos)
        {
            var piece = GetPiece(pos);
            if (piece == null || piece.IsFixed || piece.Type != PieceType.Mirror) return false;
            piece.Orientation = piece.Orientation == MirrorOrientation.Slash
                ? MirrorOrientation.Backslash
                : MirrorOrientation.Slash;
            return true;
        }

        public bool TryRemove(GridPos pos)
        {
            var piece = GetPiece(pos);
            if (piece == null || piece.IsFixed) return false;
            _pieces.Remove(pos);
            MirrorsLeft++;
            return true;
        }

        /// <summary>칸 클릭: 빈 칸 → '/' 배치, '/' → '\' 회전, '\' → 회수.</summary>
        public CellAction Interact(GridPos pos)
        {
            var piece = GetPiece(pos);
            if (piece == null)
                return TryPlaceMirror(pos, MirrorOrientation.Slash) ? CellAction.PlacedSlash : CellAction.None;
            if (piece.IsFixed)
                return CellAction.None;
            if (piece.Orientation == MirrorOrientation.Slash)
            {
                TryRotate(pos);
                return CellAction.RotatedToBackslash;
            }
            TryRemove(pos);
            return CellAction.Removed;
        }

        /// <summary>플레이어가 배치한 거울을 모두 회수한다.</summary>
        public void ResetPlayerPieces()
        {
            var playerPositions = _pieces.Values.Where(p => !p.IsFixed).Select(p => p.Position).ToList();
            foreach (var pos in playerPositions) _pieces.Remove(pos);
            MirrorsLeft = MirrorsTotal;
        }
    }
}
