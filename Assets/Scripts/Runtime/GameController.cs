using System.Collections.Generic;
using System.Linq;
using Refract.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Refract.Runtime
{
    /// <summary>
    /// 씬 편집 없이 게임을 띄우기 위한 진입점.
    /// 어떤 씬이든 로드되면 GameController가 자동으로 생성된다.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindFirstObjectByType<GameController>() != null) return;
            new GameObject("REFRACT").AddComponent<GameController>();
        }
    }

    /// <summary>
    /// 화면(View)과 입력 담당. 게임 규칙은 전부 Refract.Core에 있고,
    /// 여기서는 Core의 결과를 그리기만 한다 (Humble Object 패턴).
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        private static readonly Color BgColor = new Color(0.05f, 0.06f, 0.09f);
        private static readonly Color CellColor = new Color(0.12f, 0.14f, 0.19f);
        private static readonly Color CellHover = new Color(0.17f, 0.20f, 0.27f);
        private static readonly Color WallColor = new Color(0.35f, 0.37f, 0.42f);
        private static readonly Color EmitterColor = new Color(1f, 0.62f, 0.15f);
        private static readonly Color ReceiverOff = new Color(0.15f, 0.35f, 0.22f);
        private static readonly Color ReceiverOn = new Color(0.30f, 1f, 0.50f);
        private static readonly Color PlayerMirrorColor = new Color(0.45f, 0.85f, 1f);
        private static readonly Color FixedMirrorColor = new Color(0.85f, 0.85f, 0.90f);
        private static readonly Color BeamColor = new Color(1f, 0.25f, 0.30f);

        private const float BoardPixels = 720f;

        private readonly List<LevelData> _levels = new List<LevelData>();
        private int _levelIndex;
        private LevelData _level;
        private Board _board;

        private Font _font;
        private RectTransform _boardRoot, _cellLayer, _beamLayer, _pieceLayer;
        private Text _levelLabel, _mirrorLabel, _statusLabel;
        private float _cellSize;

        private void Start()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            LoadLevels();
            BuildUi();
            LoadLevel(0);
        }

        // ───────────────────────── 레벨 ─────────────────────────

        private void LoadLevels()
        {
            var assets = Resources.LoadAll<TextAsset>("Levels").OrderBy(a => a.name);
            foreach (var asset in assets)
            {
                try
                {
                    _levels.Add(LevelLoader.Parse(asset.text));
                }
                catch (LevelFormatException e)
                {
                    Debug.LogError($"레벨 로드 실패 ({asset.name}): {e.Message}");
                }
            }
        }

        private void LoadLevel(int index)
        {
            if (_levels.Count == 0)
            {
                _statusLabel.text = "NO LEVELS";
                return;
            }

            _levelIndex = (index % _levels.Count + _levels.Count) % _levels.Count;
            _level = _levels[_levelIndex];
            _board = LevelLoader.BuildBoard(_level);
            _levelLabel.text = $"{_level.id}  ·  {_level.name}";
            BuildCells();
            Refresh();
        }

        private void OnCellClicked(GridPos pos)
        {
            if (_board.Interact(pos) != CellAction.None) Refresh();
        }

        // ───────────────────────── 화면 갱신 ─────────────────────────

        private void Refresh()
        {
            var result = BeamTracer.Trace(_board);
            DrawPieces(result);
            DrawBeams(result);
            _mirrorLabel.text = $"MIRRORS  {_board.MirrorsLeft} / {_board.MirrorsTotal}";
            _statusLabel.text = result.IsCleared ? "CLEAR!" : "";
        }

        private void BuildCells()
        {
            Clear(_cellLayer);
            _cellSize = Mathf.Floor(BoardPixels / Mathf.Max(_board.Width, _board.Height));
            _boardRoot.sizeDelta = new Vector2(_cellSize * _board.Width, _cellSize * _board.Height);

            for (var y = 0; y < _board.Height; y++)
            for (var x = 0; x < _board.Width; x++)
            {
                var pos = new GridPos(x, y);
                // 버튼 색 틴트는 이미지 색에 곱해지므로, 이미지는 흰색으로 두고 버튼 색으로 칠한다
                var cell = CreateImage($"Cell{x}_{y}", _cellLayer, Color.white);
                Place(cell.rectTransform, CellCenter(pos), Vector2.one * (_cellSize - 4f));

                var button = cell.gameObject.AddComponent<Button>();
                var colors = button.colors;
                colors.normalColor = CellColor;
                colors.highlightedColor = CellHover;
                colors.pressedColor = CellHover;
                colors.selectedColor = CellColor;
                colors.colorMultiplier = 1f;
                button.colors = colors;
                button.onClick.AddListener(() => OnCellClicked(pos));
            }
        }

        private void DrawPieces(TraceResult result)
        {
            Clear(_pieceLayer);
            foreach (var piece in _board.Pieces.Values)
            {
                var center = CellCenter(piece.Position);
                switch (piece.Type)
                {
                    case PieceType.Wall:
                        Place(CreateImage("Wall", _pieceLayer, WallColor).rectTransform, center, Vector2.one * (_cellSize - 4f));
                        break;

                    case PieceType.Emitter:
                        Place(CreateImage("Emitter", _pieceLayer, EmitterColor).rectTransform, center, Vector2.one * (_cellSize * 0.55f));
                        var nozzleOffset = ToVector(piece.Facing.Offset()) * (_cellSize * 0.3f);
                        Place(CreateImage("Nozzle", _pieceLayer, EmitterColor).rectTransform, center + nozzleOffset, Vector2.one * (_cellSize * 0.2f));
                        break;

                    case PieceType.Receiver:
                        var lit = result.LitReceivers.Contains(piece.Position);
                        Place(CreateImage("Receiver", _pieceLayer, lit ? ReceiverOn : ReceiverOff).rectTransform, center, Vector2.one * (_cellSize * 0.6f));
                        Place(CreateImage("Core", _pieceLayer, BgColor).rectTransform, center, Vector2.one * (_cellSize * 0.3f));
                        break;

                    case PieceType.Mirror:
                        var color = piece.IsFixed ? FixedMirrorColor : PlayerMirrorColor;
                        var mirror = CreateImage("Mirror", _pieceLayer, color).rectTransform;
                        Place(mirror, center, new Vector2(_cellSize * 0.95f, _cellSize * 0.09f));
                        // UI 좌표는 y가 위로 증가 → +45° = '/', -45° = '\'
                        mirror.localRotation = Quaternion.Euler(0, 0, piece.Orientation == MirrorOrientation.Slash ? 45f : -45f);
                        break;
                }
            }
        }

        private void DrawBeams(TraceResult result)
        {
            Clear(_beamLayer);
            foreach (var beam in result.Beams)
            {
                var points = new List<Vector2> { CellCenter(beam.Origin) };
                points.AddRange(beam.Cells.Select(CellCenter));
                if (beam.End == BeamEnd.ExitedBoard)
                {
                    var last = points[points.Count - 1];
                    points.Add(last + ToVector(beam.FinalDirection.Offset()) * (_cellSize * 0.5f));
                }

                for (var i = 0; i < points.Count - 1; i++)
                    DrawSegment(points[i], points[i + 1]);
            }
        }

        private void DrawSegment(Vector2 from, Vector2 to)
        {
            var seg = CreateImage("Beam", _beamLayer, BeamColor).rectTransform;
            var delta = to - from;
            seg.pivot = new Vector2(0f, 0.5f);
            seg.anchoredPosition = from;
            seg.sizeDelta = new Vector2(delta.magnitude, Mathf.Max(4f, _cellSize * 0.07f));
            seg.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        // ───────────────────────── UI 생성 ─────────────────────────

        private void BuildUi()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;

            var bg = CreateImage("Background", root, BgColor).rectTransform;
            bg.anchorMin = Vector2.zero;
            bg.anchorMax = Vector2.one;
            bg.offsetMin = bg.offsetMax = Vector2.zero;

            CreateText("Title", root, "REFRACT", 64, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(60, -40), new Color(0.9f, 0.95f, 1f));
            _levelLabel = CreateText("Level", root, "", 36, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -50), Color.white);
            _statusLabel = CreateText("Status", root, "", 56, TextAnchor.LowerCenter, new Vector2(0.5f, 0), new Vector2(0, 50), ReceiverOn);
            _mirrorLabel = CreateText("Mirrors", root, "", 32, TextAnchor.UpperLeft, new Vector2(0, 0.5f), new Vector2(60, 120), PlayerMirrorColor);
            CreateText("Help", root, "CLICK A CELL\nplace /  ->  rotate \\  ->  remove\n\nLight every receiver.", 24,
                TextAnchor.UpperLeft, new Vector2(0, 0.5f), new Vector2(60, 60), new Color(0.6f, 0.65f, 0.75f));
            CreateText("Version", root, $"v{Application.version}", 22, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(-30, 20), new Color(0.5f, 0.55f, 0.65f));

            CreateButton("Prev", root, "PREV", new Vector2(1, 0.5f), new Vector2(-260, 80), () => LoadLevel(_levelIndex - 1));
            CreateButton("Next", root, "NEXT", new Vector2(1, 0.5f), new Vector2(-260, 0), () => LoadLevel(_levelIndex + 1));
            CreateButton("Reset", root, "RESET", new Vector2(1, 0.5f), new Vector2(-260, -80), () =>
            {
                _board.ResetPlayerPieces();
                Refresh();
            });

            _boardRoot = new GameObject("Board", typeof(RectTransform)).GetComponent<RectTransform>();
            _boardRoot.SetParent(root, false);
            _boardRoot.anchorMin = _boardRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _boardRoot.pivot = new Vector2(0.5f, 0.5f);
            _boardRoot.anchoredPosition = new Vector2(0, -10);

            _cellLayer = CreateLayer("Cells");
            _beamLayer = CreateLayer("Beams");
            _pieceLayer = CreateLayer("Pieces");
        }

        private RectTransform CreateLayer(string layerName)
        {
            var layer = new GameObject(layerName, typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(_boardRoot, false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            return layer;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        private Image CreateImage(string objName, Transform parent, Color color)
        {
            var go = new GameObject(objName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = parent == _cellLayer || parent.name == "Canvas" || parent.GetComponent<Button>() != null;
            return image;
        }

        private Text CreateText(string objName, Transform parent, string content, int size, TextAnchor align,
            Vector2 anchor, Vector2 position, Color color)
        {
            var go = new GameObject(objName, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.alignment = align;
            text.color = color;
            text.text = content;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(600, 100);
            return text;
        }

        private void CreateButton(string objName, Transform parent, string label, Vector2 anchor, Vector2 position,
            UnityEngine.Events.UnityAction onClick)
        {
            var image = CreateImage(objName, parent, CellColor);
            image.raycastTarget = true;
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(220, 64);
            var button = image.gameObject.AddComponent<Button>();
            button.onClick.AddListener(onClick);

            var text = CreateText("Label", rt, label, 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, Color.white);
            text.rectTransform.sizeDelta = rt.sizeDelta;
        }

        // ───────────────────────── 좌표 변환 ─────────────────────────

        /// <summary>그리드 좌표 → Board RectTransform 기준 픽셀 좌표 (칸 중심).</summary>
        private Vector2 CellCenter(GridPos pos)
        {
            var origin = -_boardRoot.sizeDelta * 0.5f;
            return origin + new Vector2((pos.X + 0.5f) * _cellSize, (pos.Y + 0.5f) * _cellSize);
        }

        private static Vector2 ToVector(GridPos offset) => new Vector2(offset.X, offset.Y);

        private static void Place(RectTransform rt, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        private static void Clear(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }
    }
}
