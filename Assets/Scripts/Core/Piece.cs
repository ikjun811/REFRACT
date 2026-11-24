namespace Refract.Core
{
    public enum PieceType { Emitter, Wall, Receiver, Mirror, Splitter }

    /// <summary>
    /// 보드 위의 장치 하나.
    /// IsFixed = 레벨에 고정된 장치(플레이어가 건드릴 수 없음), false = 플레이어가 배치한 거울.
    /// </summary>
    public sealed class Piece
    {
        public PieceType Type { get; }
        public GridPos Position { get; }
        public bool IsFixed { get; }

        /// <summary>광원의 발사 방향 (Emitter에서만 의미 있음).</summary>
        public Direction Facing { get; }

        /// <summary>거울·분산기 방향 (Mirror, Splitter에서 의미 있음). 플레이어 거울은 회전으로 바뀐다.</summary>
        public MirrorOrientation Orientation { get; internal set; }

        private Piece(PieceType type, GridPos position, bool isFixed, Direction facing, MirrorOrientation orientation)
        {
            Type = type;
            Position = position;
            IsFixed = isFixed;
            Facing = facing;
            Orientation = orientation;
        }

        public static Piece Emitter(GridPos pos, Direction facing) =>
            new Piece(PieceType.Emitter, pos, true, facing, MirrorOrientation.Slash);

        public static Piece Wall(GridPos pos) =>
            new Piece(PieceType.Wall, pos, true, Direction.Up, MirrorOrientation.Slash);

        public static Piece Receiver(GridPos pos) =>
            new Piece(PieceType.Receiver, pos, true, Direction.Up, MirrorOrientation.Slash);

        public static Piece Mirror(GridPos pos, MirrorOrientation orientation, bool isFixed) =>
            new Piece(PieceType.Mirror, pos, isFixed, Direction.Up, orientation);

        /// <summary>
        /// 빛 분산기(반투명 거울). 들어온 빔을 '직진 빔'과 '반사 빔' 두 갈래로 나눈다.
        /// 반사 방향은 같은 방향의 거울과 동일하다. 레벨에 고정 배치된다.
        /// </summary>
        public static Piece Splitter(GridPos pos, MirrorOrientation orientation) =>
            new Piece(PieceType.Splitter, pos, true, Direction.Up, orientation);
    }
}
