using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Refract.Core
{
    // ───────────────────────── 레벨 JSON 데이터 구조 ─────────────────────────
    // 예시:
    // {
    //   "id": "L01", "name": "First Light", "width": 5, "height": 5, "mirrors": 1,
    //   "pieces":   [ { "type": "Emitter", "x": 0, "y": 2, "dir": "Right" },
    //                 { "type": "Receiver", "x": 4, "y": 4 } ],
    //   "solution": [ { "type": "Mirror", "x": 4, "y": 2, "orientation": "Slash" } ]
    // }

    public sealed class LevelData
    {
        public string id;
        public string name;
        public int width;
        public int height;
        /// <summary>플레이어가 배치할 수 있는 거울 개수.</summary>
        public int mirrors;
        /// <summary>고정 장치 목록.</summary>
        public List<PieceData> pieces = new List<PieceData>();
        /// <summary>의도한 정답(플레이어 거울 배치). CI가 이 정답으로 클리어되는지 검증한다.</summary>
        public List<PieceData> solution = new List<PieceData>();
    }

    public sealed class PieceData
    {
        public string type;
        public int x;
        public int y;
        public string dir;
        public string orientation;
    }

    public sealed class LevelFormatException : Exception
    {
        public LevelFormatException(string message) : base(message) { }
    }

    // ───────────────────────── 로드 / 검증 / 보드 생성 ─────────────────────────

    public static class LevelLoader
    {
        public static LevelData Parse(string json)
        {
            try
            {
                var data = JsonConvert.DeserializeObject<LevelData>(json);
                if (data == null) throw new LevelFormatException("빈 레벨 데이터입니다.");
                data.pieces = data.pieces ?? new List<PieceData>();
                data.solution = data.solution ?? new List<PieceData>();
                return data;
            }
            catch (JsonException e)
            {
                throw new LevelFormatException($"JSON 파싱 실패: {e.Message}");
            }
        }

        /// <summary>레벨 데이터로 보드를 만든다(고정 장치만 배치). 검증 오류가 있으면 예외.</summary>
        public static Board BuildBoard(LevelData data)
        {
            var errors = LevelValidator.Validate(data);
            if (errors.Count > 0)
                throw new LevelFormatException($"[{data.id}] " + string.Join(" / ", errors));

            var board = new Board(data.width, data.height, data.mirrors);
            foreach (var p in data.pieces)
                board.AddFixed(ToFixedPiece(p));
            return board;
        }

        /// <summary>보드에 레벨의 정답 거울을 배치한다. 배치 실패 시 false.</summary>
        public static bool ApplySolution(Board board, LevelData data)
        {
            foreach (var s in data.solution)
            {
                var orientation = ParseEnum<MirrorOrientation>(s.orientation);
                if (!board.TryPlaceMirror(new GridPos(s.x, s.y), orientation)) return false;
            }
            return true;
        }

        internal static Piece ToFixedPiece(PieceData p)
        {
            var pos = new GridPos(p.x, p.y);
            switch (ParseEnum<PieceType>(p.type))
            {
                case PieceType.Emitter: return Piece.Emitter(pos, ParseEnum<Direction>(p.dir));
                case PieceType.Wall: return Piece.Wall(pos);
                case PieceType.Receiver: return Piece.Receiver(pos);
                default: return Piece.Mirror(pos, ParseEnum<MirrorOrientation>(p.orientation), isFixed: true);
            }
        }

        internal static T ParseEnum<T>(string value) where T : struct
        {
            if (value != null && Enum.TryParse(value, ignoreCase: false, out T result) && Enum.IsDefined(typeof(T), result))
                return result;
            throw new LevelFormatException($"알 수 없는 {typeof(T).Name} 값: '{value}'");
        }

        internal static bool IsValidEnum<T>(string value) where T : struct =>
            value != null && Enum.TryParse(value, false, out T result) && Enum.IsDefined(typeof(T), result);
    }

    /// <summary>레벨 데이터의 구조적 오류를 찾는다. 빈 목록이면 정상.</summary>
    public static class LevelValidator
    {
        public const int MinSize = 3;
        public const int MaxSize = 12;

        public static List<string> Validate(LevelData data)
        {
            var errors = new List<string>();
            if (data == null)
            {
                errors.Add("레벨 데이터가 없습니다.");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(data.id)) errors.Add("id가 비어 있습니다.");
            if (data.width < MinSize || data.width > MaxSize || data.height < MinSize || data.height > MaxSize)
                errors.Add($"보드 크기는 {MinSize}~{MaxSize} 사이여야 합니다: {data.width}x{data.height}");
            if (data.mirrors < 0) errors.Add("mirrors는 0 이상이어야 합니다.");

            var occupied = new HashSet<GridPos>();
            int emitters = 0, receivers = 0;

            foreach (var p in data.pieces)
            {
                var pos = new GridPos(p.x, p.y);
                if (!LevelLoader.IsValidEnum<PieceType>(p.type))
                {
                    errors.Add($"알 수 없는 장치 타입 '{p.type}' at {pos}");
                    continue;
                }
                if (!Inside(data, pos)) errors.Add($"보드 밖 장치 '{p.type}' at {pos}");
                if (!occupied.Add(pos)) errors.Add($"장치 겹침 at {pos}");

                var type = LevelLoader.ParseEnum<PieceType>(p.type);
                if (type == PieceType.Emitter)
                {
                    emitters++;
                    if (!LevelLoader.IsValidEnum<Direction>(p.dir)) errors.Add($"광원 방향 오류 '{p.dir}' at {pos}");
                }
                if (type == PieceType.Receiver) receivers++;
                if (type == PieceType.Mirror && !LevelLoader.IsValidEnum<MirrorOrientation>(p.orientation))
                    errors.Add($"거울 방향 오류 '{p.orientation}' at {pos}");
            }

            if (emitters == 0) errors.Add("광원(Emitter)이 하나 이상 필요합니다.");
            if (receivers == 0) errors.Add("수신기(Receiver)가 하나 이상 필요합니다.");

            // 정답 데이터 검사
            if (data.solution.Count > data.mirrors)
                errors.Add($"정답 거울 수({data.solution.Count})가 사용 가능 거울 수({data.mirrors})보다 많습니다.");
            foreach (var s in data.solution)
            {
                var pos = new GridPos(s.x, s.y);
                if (s.type != nameof(PieceType.Mirror)) errors.Add($"정답에는 Mirror만 넣을 수 있습니다: '{s.type}' at {pos}");
                if (!LevelLoader.IsValidEnum<MirrorOrientation>(s.orientation)) errors.Add($"정답 거울 방향 오류 '{s.orientation}' at {pos}");
                if (!Inside(data, pos)) errors.Add($"정답 거울이 보드 밖 at {pos}");
                if (!occupied.Add(pos)) errors.Add($"정답 거울이 다른 장치와 겹침 at {pos}");
            }

            return errors;
        }

        private static bool Inside(LevelData d, GridPos p) => p.X >= 0 && p.Y >= 0 && p.X < d.width && p.Y < d.height;
    }

    /// <summary>"저장된 정답으로 실제로 클리어되는가"를 검사한다. CI 레벨 검증의 핵심.</summary>
    public static class SolutionChecker
    {
        public static bool IsSolvedBySolution(LevelData data, out string reason)
        {
            var board = LevelLoader.BuildBoard(data);
            if (!LevelLoader.ApplySolution(board, data))
            {
                reason = "정답 거울을 배치할 수 없습니다.";
                return false;
            }

            var result = BeamTracer.Trace(board);
            if (!result.IsCleared)
            {
                reason = $"정답 배치에서 수신기 {result.LitReceivers.Count}/{result.ReceiverCount}개만 점등됩니다.";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>거울을 하나도 놓지 않았는데 이미 클리어되는 레벨인지 (너무 쉬운 레벨 방지).</summary>
        public static bool IsClearedWithoutMirrors(LevelData data)
        {
            var board = LevelLoader.BuildBoard(data);
            return BeamTracer.Trace(board).IsCleared;
        }
    }
}
