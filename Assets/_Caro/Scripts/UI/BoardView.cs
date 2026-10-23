using System;
using System.Collections.Generic;
using System.Threading;
using Caro.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Caro.Game
{
    /// <summary>
    /// Draws the squared-paper board and turns taps/clicks into moves.
    /// Each cell is 1 world unit; cell (x, y) is centred at (x + 0.5, y + 0.5).
    /// X/O pieces are hand-drawn ink strokes, drawn along the pen path when placed and erased
    /// diagonally on undo; the last move is highlighted and the winning line is struck through.
    /// Animations run as UniTasks and are cancelled when another game is attached or the view is destroyed.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        [Header("Colours (squared paper)")]
        [SerializeField] private Color paperColor = Color.white;
        [SerializeField] private Color paperShadow = new Color(0.10f, 0.25f, 0.18f, 0.18f);
        [SerializeField] private Color gridColor = new Color(0.62f, 0.77f, 0.93f);
        [SerializeField] private Color xColor = new Color(0.82f, 0.20f, 0.20f);
        [SerializeField] private Color oColor = new Color(0.12f, 0.30f, 0.76f);
        [SerializeField] private Color winHighlight = new Color(1f, 0.92f, 0.55f);
        [Tooltip("Highlighter colour marking the last move")]
        [SerializeField] private Color lastMoveColor = new Color(1f, 0.90f, 0.35f, 0.45f);

        [Tooltip("Unlit sprite material (keeps the shader in player builds)")]
        [SerializeField] private Material spriteMaterialAsset;
        [Tooltip("Caro/InkReveal material: draws and erases pieces along the pen path")]
        [SerializeField] private Material inkMaterialAsset;

        [Header("Sizes")]
        [SerializeField] private float lineWidth = 0.035f;
        [SerializeField] private float pieceScale = 0.86f;
        [Tooltip("Margin on each side of the board, in cells.")]
        [SerializeField] private float sideMargin = 0.5f;
        [Tooltip("Share of the screen used by the board (the rest is for the top bar and bottom buttons).")]
        [SerializeField, Range(0.4f, 1f)] private float boardAreaHeight = 0.62f;
        [SerializeField, Range(0f, 0.5f)] private float boardAreaBottom = 0.15f;

        [Header("Effects")]
        [Tooltip("How long a piece takes to be erased on undo (seconds)")]
        [SerializeField] private float eraseDuration = 0.5f;
        [SerializeField] private float winStrokeDuration = 0.35f;
        [SerializeField] private float winStrokeWidth = 0.13f;

        [Tooltip("Moving more than this many pixels between press and release counts as a drag, not a move.")]
        [SerializeField] private float tapSlopPixels = 20f;

        [Header("Zoom / pan")]
        [SerializeField] private float maxZoom = 3f;
        [Tooltip("Zoom factor per mouse-wheel notch")]
        [SerializeField] private float wheelZoomStep = 1.12f;

        private CaroGame _game;
        private Camera _cam;
        private Sprite _pixel;
        private Sprite[] _xSprites, _oSprites;
        private Material _spriteMaterial;
        private MaterialPropertyBlock _mpb;
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private static readonly int EraseId = Shader.PropertyToID("_Erase");
        private const float RevealDone = 1.2f;
        private const float EraseNone = -1f;
        private readonly List<GameObject> _erasing = new List<GameObject>();
        private CancellationTokenSource _gameCts; // cancels the animations of the attached game
        private Transform _gridRoot, _piecesRoot;
        private readonly Dictionary<Cell, SpriteRenderer> _pieces = new Dictionary<Cell, SpriteRenderer>();
        private readonly List<GameObject> _highlights = new List<GameObject>();
        private SpriteRenderer _lastMarker;
        private Vector2 _pressPos;
        private bool _pressing;
        private bool _instant; // rebuilding the board (Attach) → no animations

        // Zoom / pan
        private float _fitSize;       // camera size when fitted to the screen
        private Vector3 _fitPos;      // camera position when fitted to the screen
        private float _zoom = 1f;     // 1 = fitted to the screen
        private bool _dragging, _pinching;
        private Vector2 _lastPointerPos;
        private float _pinchStartDist, _pinchStartZoom;

        public bool IsZoomed => _zoom > 1.001f;

        /// <summary>Raised when switching between fitted and zoomed in.</summary>
        public event Action<bool> ZoomChanged;

        public CaroGame Game => _game;

        /// <summary>Raised whenever a new game starts or an unfinished game is shown again.</summary>
        public event Action<CaroGame> GameStarted;

        /// <summary>Disabled while a popup covers the board or it is not the player's turn.</summary>
        public bool InputEnabled { get; set; } = true;

        private void Awake()
        {
            _cam = Camera.main;
            _pixel = InkSprites.Pixel();
            _xSprites = InkSprites.MakeX(4, 11);
            _oSprites = InkSprites.MakeO(4, 23);
            _spriteMaterial = spriteMaterialAsset;
            _mpb = new MaterialPropertyBlock();
            if (inkMaterialAsset == null)
            {
                var ink = Shader.Find("Caro/InkReveal");
                if (ink != null) inkMaterialAsset = new Material(ink);
            }
            if (_spriteMaterial == null)
            {
                var unlit = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (unlit != null) _spriteMaterial = new Material(unlit);
            }
            NewGame(Piece.X);
        }

        public void NewGame(Piece firstPlayer) => Attach(new CaroGame(firstPlayer));

        /// <summary>
        /// Shows a game (new or unfinished): redraws the grid and every piece from the move history.
        /// </summary>
        public void Attach(CaroGame game)
        {
            if (_game != null)
            {
                _game.PiecePlaced -= OnPiecePlaced;
                _game.GameEnded -= OnGameEnded;
                _game.BoardResized -= OnBoardResized;
                _game.MoveUndone -= OnMoveUndone;
            }
            _gameCts?.Cancel();
            _gameCts?.Dispose();
            _gameCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            ClearHighlights();
            foreach (var r in _pieces.Values) if (r != null) Destroy(r.gameObject);
            _pieces.Clear();
            foreach (var g in _erasing) if (g != null) Destroy(g);
            _erasing.Clear();

            _game = game;
            _game.PiecePlaced += OnPiecePlaced;
            _game.GameEnded += OnGameEnded;
            _game.BoardResized += OnBoardResized;
            _game.MoveUndone += OnMoveUndone;
            SetZoom(1f, null);
            FitCamera();
            DrawGrid();

            _instant = true;
            foreach (var cell in _game.History) OnPiecePlaced(cell, _game.Board.Get(cell));
            if (_game.IsOver && _game.Winner != Piece.None) OnGameEnded(_game.Winner);
            _instant = false;
            UpdateLastMarker();

            GameStarted?.Invoke(_game);
        }

        // ---------- Input ----------

        private void Update()
        {
            if (_game == null) return;
            if (HandlePinch()) return;
            HandleWheel();

            var pointer = Pointer.current;
            if (pointer == null) return;
            var pos = pointer.position.ReadValue();

            // Press and release can land in the same frame (a very quick tap), so check them separately.
            if (pointer.press.wasPressedThisFrame)
            {
                // Ignore touches that land on UI buttons/bars
                _pressing = !IsOverUI(pointer);
                _dragging = false;
                _pressPos = _lastPointerPos = pos;
            }
            else if (_pressing && pointer.press.isPressed)
            {
                // Dragged past the threshold → pan the board (only while zoomed in)
                if (!_dragging && (pos - _pressPos).magnitude > tapSlopPixels) _dragging = true;
                if (_dragging && IsZoomed) PanBy(_lastPointerPos, pos);
                _lastPointerPos = pos;
            }
            if (pointer.press.wasReleasedThisFrame && _pressing)
            {
                _pressing = false;
                if (!_dragging && (pos - _pressPos).magnitude <= tapSlopPixels) HandleTap(pos);
                _dragging = false;
            }
        }

        /// <summary>Two-finger pinch to zoom. Returns true while a pinch is being handled (no move is placed).</summary>
        private bool HandlePinch()
        {
            var ts = Touchscreen.current;
            if (ts == null) return false;
            int active = 0; Vector2 p0 = default, p1 = default;
            foreach (var t in ts.touches)
            {
                if (!t.press.isPressed) continue;
                if (active == 0) p0 = t.position.ReadValue(); else if (active == 1) p1 = t.position.ReadValue();
                active++;
            }
            if (active >= 2)
            {
                float dist = Mathf.Max(1f, (p1 - p0).magnitude);
                var mid = (p0 + p1) / 2f;
                if (!_pinching)
                {
                    _pinching = true;
                    _pressing = false; // cancel the pending single tap
                    _pinchStartDist = dist;
                    _pinchStartZoom = _zoom;
                    _lastPointerPos = mid;
                }
                else
                {
                    PanBy(_lastPointerPos, mid);
                    _lastPointerPos = mid;
                }
                SetZoom(_pinchStartZoom * dist / _pinchStartDist, mid);
                return true;
            }
            if (_pinching)
            {
                // Wait until all fingers lift so the remaining finger does not place a piece by mistake
                if (active == 0) _pinching = false;
                _pressing = false;
                return true;
            }
            return false;
        }

        private void HandleWheel()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            float y = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(y) < 0.01f) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            float notches = y / 120f;
            if (Mathf.Abs(notches) < 0.5f) notches = Mathf.Sign(y); // some devices report 1 instead of 120 per notch
            SetZoom(_zoom * Mathf.Pow(wheelZoomStep, notches), mouse.position.ReadValue());
        }

        /// <summary>Pans the board with the finger: the point under the finger follows it.</summary>
        private void PanBy(Vector2 fromScreen, Vector2 toScreen)
        {
            var a = ScreenToWorld(fromScreen);
            var b = ScreenToWorld(toScreen);
            _cam.transform.position += (Vector3)(a - b);
            ClampCamera();
        }

        /// <summary>Changes the zoom, keeping the point under the finger/cursor (anchor) fixed if given.</summary>
        private void SetZoom(float zoom, Vector2? anchorScreen)
        {
            bool wasZoomed = IsZoomed;
            zoom = Mathf.Clamp(zoom, 1f, maxZoom);
            if (_cam == null || _fitSize <= 0f) { _zoom = zoom; return; }

            Vector2 before = anchorScreen.HasValue ? ScreenToWorld(anchorScreen.Value) : default;
            _zoom = zoom;
            _cam.orthographicSize = _fitSize / _zoom;
            if (!IsZoomed) _cam.transform.position = _fitPos;
            else if (anchorScreen.HasValue)
            {
                Vector2 after = ScreenToWorld(anchorScreen.Value);
                _cam.transform.position += (Vector3)(before - after);
            }
            ClampCamera();
            if (wasZoomed != IsZoomed) ZoomChanged?.Invoke(IsZoomed);
        }

        /// <summary>
        /// Sets the screen area reserved for the board (0..1 from the bottom), matching the real gap
        /// between the top bar and the bottom buttons on each device. Call again when the screen size changes.
        /// </summary>
        public void SetBoardArea(float bottom, float height)
        {
            bottom = Mathf.Clamp01(bottom);
            height = Mathf.Clamp(height, 0.2f, 1f - bottom);
            if (Mathf.Approximately(bottom, boardAreaBottom) && Mathf.Approximately(height, boardAreaHeight)) return;
            boardAreaBottom = bottom;
            boardAreaHeight = height;
            if (_game == null || _cam == null) return;
            FitCamera();
            DrawGrid();
        }

        /// <summary>Fits the board to the screen again.</summary>
        public void ResetZoom() => SetZoom(1f, null);

        /// <summary>Keeps the paper on screen: its edges stop at the edges of the board area.</summary>
        private void ClampCamera()
        {
            if (_game == null || !IsZoomed) return;
            const float pad = 0.3f;
            var b = _game.Board;
            float size = _cam.orthographicSize;
            float halfW = size * _cam.aspect;
            float halfH = size * boardAreaHeight;
            float areaCenter = boardAreaBottom + boardAreaHeight / 2f;
            float yOff = (0.5f - areaCenter) * 2f * size; // camera offset from the centre of the board area

            var p = _cam.transform.position;
            p.x = ClampRange(p.x, b.MinX - pad + halfW, b.MaxX + 1 + pad - halfW);
            float areaY = ClampRange(p.y - yOff, b.MinY - pad + halfH, b.MaxY + 1 + pad - halfH);
            p.y = areaY + yOff;
            _cam.transform.position = p;
        }

        private static float ClampRange(float v, float min, float max) =>
            min > max ? (min + max) / 2f : Mathf.Clamp(v, min, max);

        private Vector2 ScreenToWorld(Vector2 screen) =>
            _cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_cam.transform.position.z));

        private static bool IsOverUI(Pointer pointer)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (pointer is Touchscreen ts) return es.IsPointerOverGameObject(ts.primaryTouch.touchId.ReadValue());
            return es.IsPointerOverGameObject();
        }

        /// <summary>Converts a screen position to a board cell.</summary>
        public Cell ScreenToCell(Vector2 screenPos)
        {
            var world = ScreenToWorld(screenPos);
            return new Cell(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
        }

        private void HandleTap(Vector2 screenPos)
        {
            if (InputEnabled) _game.TryPlace(ScreenToCell(screenPos));
        }

        // ---------- Pieces ----------

        private void OnPiecePlaced(Cell cell, Piece piece)
        {
            var go = new GameObject($"{piece} {cell}");
            go.transform.SetParent(_piecesRoot, false);
            go.transform.localPosition = new Vector3(cell.X + 0.5f, cell.Y + 0.5f, -0.1f);
            // Slight random rotation so it looks hand-drawn
            int h = (cell.X * 73856093) ^ (cell.Y * 19349663);
            go.transform.localRotation = Quaternion.Euler(0, 0, ((h & 0xFF) / 255f - 0.5f) * 10f);
            go.transform.localScale = Vector3.one * pieceScale;

            var sr = go.AddComponent<SpriteRenderer>();
            var set = piece == Piece.X ? _xSprites : _oSprites;
            sr.sprite = set[(h >> 8 & 0x7FFFFFFF) % set.Length];
            sr.color = piece == Piece.X ? xColor : oColor;
            sr.sortingOrder = 2;
            if (inkMaterialAsset != null) sr.sharedMaterial = inkMaterialAsset;
            _pieces[cell] = sr;

            if (_instant) SetInk(sr, RevealDone, EraseNone);
            else
            {
                SetInk(sr, 0f, EraseNone);
                DrawAsync(sr, piece, _gameCts.Token).Forget();
                UpdateLastMarker();
            }
        }

        /// <summary>Sets how much of the stroke is drawn (_Reveal) and how much is erased (_Erase).</summary>
        private void SetInk(SpriteRenderer sr, float reveal, float erase)
        {
            sr.GetPropertyBlock(_mpb);
            _mpb.SetFloat(RevealId, reveal);
            _mpb.SetFloat(EraseId, erase);
            sr.SetPropertyBlock(_mpb);
        }

        /// <summary>Waits one frame; returns false if the animation was cancelled.</summary>
        private static async UniTask<bool> NextFrame(CancellationToken ct) =>
            !await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow();

        /// <summary>
        /// Draws a piece like a pen would: X as two strokes one after the other, O as one loop.
        /// Timing comes from PenTiming so it matches the pen sound (Feedback.Place).
        /// </summary>
        private async UniTaskVoid DrawAsync(SpriteRenderer sr, Piece piece, CancellationToken ct)
        {
            float total = PenTiming.Duration(piece);
            for (float e = 0; e < total; e += Time.deltaTime)
            {
                if (sr == null) return;
                SetInk(sr, RevealAt(piece, e), EraseNone);
                if (!await NextFrame(ct)) break;
            }
            if (sr != null) SetInk(sr, RevealDone, EraseNone);
        }

        /// <summary>How far along the stroke order (0..1) the pen is after e seconds. Faster mid-stroke, slower at the ends.</summary>
        private static float RevealAt(Piece piece, float e)
        {
            if (piece == Piece.X)
            {
                if (e < PenTiming.XStroke) return 0.5f * Ease(e / PenTiming.XStroke);
                if (e < PenTiming.XSecondStart) return 0.5f;
                return 0.5f + 0.5f * Ease((e - PenTiming.XSecondStart) / PenTiming.XStroke);
            }
            return Ease(e / PenTiming.OStroke) * 1.03f;
        }

        private static float Ease(float k)
        {
            k = Mathf.Clamp01(k);
            return Mathf.Lerp(k, k * k * (3f - 2f * k), 0.6f);
        }

        /// <summary>Highlights the cell of the last move like a highlighter pen.</summary>
        private void UpdateLastMarker()
        {
            if (_lastMarker == null)
            {
                var go = new GameObject("LastMove");
                go.transform.SetParent(transform, false);
                _lastMarker = go.AddComponent<SpriteRenderer>();
                _lastMarker.sprite = _pixel;
                _lastMarker.color = lastMoveColor;
                _lastMarker.sortingOrder = 0;
                if (_spriteMaterial != null) _lastMarker.sharedMaterial = _spriteMaterial;
            }
            bool show = _game != null && _game.History.Count > 0 && !_game.IsOver;
            _lastMarker.gameObject.SetActive(show);
            if (!show) return;
            var c = _game.History[_game.History.Count - 1];
            _lastMarker.transform.localPosition = new Vector3(c.X + 0.5f, c.Y + 0.5f, 0.04f);
            _lastMarker.transform.localScale = new Vector3(0.94f, 0.94f, 1f);
        }

        private void OnBoardResized()
        {
            FitCamera();
            DrawGrid();
        }

        private void OnMoveUndone(Cell cell)
        {
            if (_pieces.TryGetValue(cell, out var sr))
            {
                _pieces.Remove(cell);
                if (sr != null)
                {
                    _erasing.Add(sr.gameObject);
                    EraseAsync(sr, _gameCts.Token).Forget();
                }
            }
            ClearHighlights();
            UpdateLastMarker();
        }

        /// <summary>
        /// Erases a piece with an eraser sweeping diagonally from its top-left corner to the bottom-right
        /// (the shader's _Erase edge), while the piece jitters slightly as if being rubbed.
        /// </summary>
        private async UniTaskVoid EraseAsync(SpriteRenderer sr, CancellationToken ct)
        {
            var t = sr.transform;
            var basePos = t.localPosition;
            // The ink only covers roughly 0.1..0.9 of the sprite's diagonal, so sweep just that range.
            const float from = 0.08f, to = 0.95f;
            for (float e = 0; e < eraseDuration; e += Time.deltaTime)
            {
                if (sr == null) return;
                float k = e / eraseDuration;
                float rub = Mathf.Sin(e * 60f) * 0.035f * (1f - k);
                t.localPosition = basePos + new Vector3(rub, -rub * 0.4f, 0f);
                SetInk(sr, RevealDone, Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, k)));
                if (!await NextFrame(ct)) break;
            }
            if (sr == null) return;
            _erasing.Remove(sr.gameObject);
            Destroy(sr.gameObject);
        }

        // ---------- Win ----------

        private void OnGameEnded(Piece winner)
        {
            UpdateLastMarker();
            var line = _game.WinningLine;
            if (line.Count == 0) return;

            foreach (var cell in line)
            {
                var go = new GameObject($"Win {cell}");
                go.transform.SetParent(_piecesRoot, false);
                go.transform.localPosition = new Vector3(cell.X + 0.5f, cell.Y + 0.5f, 0.05f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _pixel;
                sr.color = winHighlight;
                sr.sortingOrder = 0;
                if (_spriteMaterial != null) sr.sharedMaterial = _spriteMaterial;
                _highlights.Add(go);
            }

            // Pen stroke through the winning line
            var a = new Vector2(line[0].X + 0.5f, line[0].Y + 0.5f);
            var b = new Vector2(line[line.Count - 1].X + 0.5f, line[line.Count - 1].Y + 0.5f);
            var dir = (b - a).normalized;
            a -= dir * 0.35f; b += dir * 0.35f;
            var stroke = new GameObject("WinStroke");
            stroke.transform.SetParent(_piecesRoot, false);
            stroke.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            var ssr = stroke.AddComponent<SpriteRenderer>();
            ssr.sprite = _pixel;
            var col = winner == Piece.X ? xColor : oColor;
            ssr.color = new Color(col.r, col.g, col.b, 0.85f);
            ssr.sortingOrder = 3;
            if (_spriteMaterial != null) ssr.sharedMaterial = _spriteMaterial;
            _highlights.Add(stroke);

            if (_instant) SetStroke(stroke.transform, a, b, 1f);
            else
            {
                SetStroke(stroke.transform, a, b, 0f);
                var last = _game.History[_game.History.Count - 1];
                WinAsync(stroke.transform, a, b, line, PenTiming.Duration(_game.Board.Get(last)), _gameCts.Token).Forget();
            }
        }

        private void SetStroke(Transform t, Vector2 a, Vector2 b, float k)
        {
            var end = Vector2.Lerp(a, b, k);
            t.localPosition = new Vector3((a.x + end.x) / 2f, (a.y + end.y) / 2f, -0.2f);
            t.localScale = new Vector3(Mathf.Max(0.001f, (end - a).magnitude), winStrokeWidth, 1f);
        }

        private async UniTaskVoid WinAsync(Transform stroke, Vector2 a, Vector2 b, IReadOnlyList<Cell> line, float delay, CancellationToken ct)
        {
            // Wait until the last piece is fully drawn before striking through the line
            if (delay > 0f && await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: ct).SuppressCancellationThrow()) return;
            for (float e = 0; e < winStrokeDuration; e += Time.deltaTime)
            {
                if (stroke == null) return;
                SetStroke(stroke, a, b, Mathf.SmoothStep(0, 1, e / winStrokeDuration));
                if (!await NextFrame(ct)) return;
            }
            if (stroke == null) return;
            SetStroke(stroke, a, b, 1f);

            // The winning pieces pulse three times
            var cells = new List<Cell>(line);
            for (float e = 0; e < 1.2f; e += Time.deltaTime)
            {
                float s = 1f + 0.14f * Mathf.Abs(Mathf.Sin(e / 1.2f * Mathf.PI * 3f));
                foreach (var c in cells)
                    if (_pieces.TryGetValue(c, out var sr) && sr != null) sr.transform.localScale = Vector3.one * pieceScale * s;
                if (!await NextFrame(ct)) break;
            }
            foreach (var c in cells)
                if (_pieces.TryGetValue(c, out var sr) && sr != null) sr.transform.localScale = Vector3.one * pieceScale;
        }

        private void ClearHighlights()
        {
            foreach (var h in _highlights) if (h != null) Destroy(h);
            _highlights.Clear();
        }

        // ---------- Grid ----------

        private void DrawGrid()
        {
            if (_gridRoot != null) Destroy(_gridRoot.gameObject);
            _gridRoot = new GameObject("Grid").transform;
            _gridRoot.SetParent(transform, false);
            if (_piecesRoot == null)
            {
                _piecesRoot = new GameObject("Pieces").transform;
                _piecesRoot.SetParent(transform, false);
            }

            var b = _game.Board;
            // Keep grid lines at least ~2 pixels thick so they never vanish when the board grows and cells shrink.
            float worldPerPixel = 2f * _fitSize / Mathf.Max(1, Screen.height);
            float lw = Mathf.Max(lineWidth, 2f * worldPerPixel);
            float left = b.MinX, right = b.MaxX + 1, bottom = b.MinY, top = b.MaxY + 1;

            // White paper (with a soft drop shadow) under the grid; the rest of the screen is the camera background.
            const float pad = 0.25f;
            var paperCenter = new Vector2((left + right) / 2f, (bottom + top) / 2f);
            var paperSize = new Vector2(b.Width + pad * 2f, b.Height + pad * 2f);
            AddRect("PaperShadow", paperCenter + new Vector2(0.12f, -0.16f), paperSize, paperShadow, -2);
            AddRect("Paper", paperCenter, paperSize, paperColor, -1);

            for (int i = 0; i <= b.Width; i++)
                AddLine(new Vector2(left + i, (bottom + top) / 2f), new Vector2(lw, b.Height + lw));
            for (int j = 0; j <= b.Height; j++)
                AddLine(new Vector2((left + right) / 2f, bottom + j), new Vector2(b.Width + lw, lw));
        }

        private void AddLine(Vector2 center, Vector2 size) => AddRect("Line", center, size, gridColor, 1);

        private void AddRect(string name, Vector2 center, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_gridRoot, false);
            go.transform.localPosition = new Vector3(center.x, center.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _pixel;
            sr.color = color;
            sr.sortingOrder = order;
            if (_spriteMaterial != null) sr.sharedMaterial = _spriteMaterial;
        }

        /// <summary>
        /// Computes the "fitted" camera so the whole board sits inside the board area.
        /// If the player is zoomed in, the current view is kept.
        /// </summary>
        private void FitCamera()
        {
            var b = _game.Board;
            float needW = b.Width + sideMargin * 2f;
            float needH = b.Height + sideMargin * 2f;
            _fitSize = Mathf.Max(needW / 2f / _cam.aspect, needH / 2f / boardAreaHeight);

            float cx = b.MinX + b.Width / 2f;
            float cy = b.MinY + b.Height / 2f;
            float areaCenter = boardAreaBottom + boardAreaHeight / 2f; // measured from the bottom of the screen, 0..1
            float camY = cy + (0.5f - areaCenter) * 2f * _fitSize;
            _fitPos = new Vector3(cx, camY, _cam.transform.position.z);

            _cam.orthographicSize = _fitSize / _zoom;
            if (!IsZoomed) _cam.transform.position = _fitPos;
            else ClampCamera();
        }
    }
}
