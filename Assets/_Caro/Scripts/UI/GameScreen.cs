using System;
using System.Threading;
using Caro.AI;
using Caro.Core;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Caro.Game
{
    public enum MatchMode { TwoPlayer, VsAI }

    /// <summary>
    /// Game screen for both modes (two players / vs AI): drives the game, the AI's turn, undo,
    /// the score and the HUD. Each mode keeps its own game so the player can go to the menu and resume.
    /// The result and settings screens are full-screen: while one is shown the HUD and the board are hidden.
    /// The AI turn and the delayed result screen run as cancellable UniTasks.
    /// </summary>
    public class GameScreen : MonoBehaviour
    {
        // ---- PlayerPrefs keys ----
        private const string Key2pX = "caro.2p.scoreX";
        private const string Key2pO = "caro.2p.scoreO";
        private const string Key2pNextFirst = "caro.2p.nextFirst";
        private static string KeyAiHuman(Difficulty d) => $"caro.ai.{d}.human";
        private static string KeyAiMachine(Difficulty d) => $"caro.ai.{d}.ai";

        [SerializeField] private BoardView board;

        [Tooltip("Everything of the game screen except the result screen; hidden while a full-screen page is shown")]
        [SerializeField] private GameObject hud;

        [Header("Top bar")]
        [SerializeField] private TMP_Text modeLabel;
        [SerializeField] private UnityEngine.UI.Button menuButton;
        [SerializeField] private UnityEngine.UI.Button settingsButton;
        [SerializeField] private SettingsScreen settingsScreen;

        [Header("Score panel (left = you / X, right = AI / O)")]
        [SerializeField] private TMP_Text leftName;
        [SerializeField] private TMP_Text rightName;
        [SerializeField] private TMP_Text leftScore;
        [SerializeField] private TMP_Text rightScore;
        [Tooltip("Robot icon over the AI's name; hidden in two-player mode")]
        [SerializeField] private GameObject aiIcon;
        [Tooltip("Icons above the two scores (player or robot), used instead of the names")]
        [SerializeField] private UnityEngine.UI.Image leftIcon;
        [SerializeField] private UnityEngine.UI.Image rightIcon;

        [Header("Turn bar (left = AI / X, right = you / O)")]
        [SerializeField] private TurnAvatars turnAvatars;
        [SerializeField] private Sprite robotSprite;
        [Tooltip("Player icon for X (red) and O (blue)")]
        [SerializeField] private Sprite personXSprite;
        [SerializeField] private Sprite personOSprite;

        [Header("Backdrop")]
        [Tooltip("Camera background while the game screen is shown (around the board)")]
        [SerializeField] private Color backdrop = new Color(1f, 0.973f, 0.906f);

        [Header("Status")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text boardSizeText;

        [Header("Buttons")]
        [SerializeField] private UnityEngine.UI.Button undoButton;
        [SerializeField] private UnityEngine.UI.Button newGameButton;
        [Tooltip("Fit-to-screen button; only shown while zoomed in (replaces the board size line)")]
        [SerializeField] private UnityEngine.UI.Button fitButton;

        [Header("Board area")]
        [Tooltip("Top block (top bar + score + status); the board sits below its bottom edge")]
        [SerializeField] private RectTransform areaTop;
        [Tooltip("Bottom button bar; the board sits above its top edge")]
        [SerializeField] private RectTransform areaBottom;
        [SerializeField] private float areaGapPixels = 12f;

        [Header("End of game")]
        [SerializeField] private ResultScreen resultScreen;
        [Tooltip("Delay (seconds) before the result screen appears, so the player can see the winning line.")]
        [SerializeField] private float resultDelay = 1f;

        [Header("AI")]
        [Tooltip("Minimum seconds the AI waits before moving (Easy / Medium / Hard), so each level has its own pace.")]
        [SerializeField] private float[] aiMinThinkSeconds = { 0.4f, 0.7f, 1.0f };
        [Tooltip("Maximum thinking time for Hard (milliseconds).")]
        [SerializeField] private int aiTimeBudgetMs = 1400;

        [Header("Colours")]
        [SerializeField] private Color xColor = new Color(0.82f, 0.20f, 0.20f);
        [SerializeField] private Color oColor = new Color(0.12f, 0.30f, 0.76f);
        [SerializeField] private Color inkColor = new Color(0.16f, 0.17f, 0.22f);

        /// <summary>The player pressed "‹ Menu" or "Back to menu".</summary>
        public event Action MenuRequested;

        /// <summary>The player pressed "Đổi độ khó" on the result screen.</summary>
        public event Action ChangeDifficultyRequested;

        public MatchMode Mode => _mode;

        private MatchMode _mode = MatchMode.TwoPlayer;
        private Difficulty _difficulty = Difficulty.Medium;
        private Piece _human = Piece.X;

        private CaroGame _game;           // the game being shown
        private CaroGame _twoPlayerGame;  // two-player game (kept when going to the menu)
        private CaroGame _aiGame;         // vs-AI game (kept when going to the menu)
        private Difficulty _aiGameDifficulty;
        private Piece _aiGameHuman;

        private CancellationTokenSource _aiCts;      // non-null while the AI is thinking
        private CancellationTokenSource _resultCts;  // non-null while the result screen is pending
        private bool _settingsOpen;
        private Vector2Int _measuredScreen;
        private Rect _measuredSafe;

        private Piece AIPiece => _human.Opponent();
        private bool IsAITurn => _mode == MatchMode.VsAI && _game != null && !_game.IsOver && _game.Turn == AIPiece;

        // ================= Opening the screen (called from the menu) =================

        /// <summary>Enters two-player mode: resumes the unfinished game unless a new one is asked for.</summary>
        public void OpenTwoPlayer(bool forceNew = false)
        {
            _mode = MatchMode.TwoPlayer;
            SetCovered(false);
            if (!forceNew && _twoPlayerGame != null && !_twoPlayerGame.IsOver) board.Attach(_twoPlayerGame);
            else NewGame();
        }

        /// <summary>Enters vs-AI mode with the chosen difficulty and side: always starts a new game.</summary>
        public void OpenVsAI(Difficulty difficulty, Piece human)
        {
            _mode = MatchMode.VsAI;
            _difficulty = difficulty;
            _human = human;
            SetCovered(false);
            NewGame();
        }

        /// <summary>Resumes the unfinished vs-AI game (same difficulty and side).</summary>
        public void ResumeVsAI()
        {
            if (!HasGameInProgress(MatchMode.VsAI)) return;
            _mode = MatchMode.VsAI;
            _difficulty = _aiGameDifficulty;
            _human = _aiGameHuman;
            SetCovered(false);
            board.Attach(_aiGame);
        }

        public bool HasGameInProgress(MatchMode mode)
        {
            var g = mode == MatchMode.TwoPlayer ? _twoPlayerGame : _aiGame;
            return g != null && !g.IsOver && g.History.Count > 0;
        }

        public (Difficulty difficulty, Piece human) InProgressAISettings => (_aiGameDifficulty, _aiGameHuman);

        // ================= Lifecycle =================

        private void Awake()
        {
            board.GameStarted += OnGameAttached;
            if (undoButton != null) undoButton.onClick.AddListener(Undo);
            if (newGameButton != null) newGameButton.onClick.AddListener(NewGame);
            if (menuButton != null) menuButton.onClick.AddListener(GoToMenu);
            if (settingsButton != null && settingsScreen != null)
                settingsButton.onClick.AddListener(OpenSettings);
            if (fitButton != null)
            {
                fitButton.onClick.AddListener(board.ResetZoom);
                board.ZoomChanged += OnZoomChanged;
                OnZoomChanged(board.IsZoomed);
            }

            if (resultScreen != null)
            {
                resultScreen.Hide();
                // Leaving the result page turns the page (the menu and mode screens are changed inside the turn)
                resultScreen.PlayAgainClicked += () => PageTurn.Run(NewGame);
                resultScreen.ReviewClicked += () => PageTurn.Run(() => { HideResult(); Refresh(); });
                resultScreen.MenuClicked += GoToMenu;
                resultScreen.ChangeDifficultyClicked += () => PageTurn.Run(() => { HideResult(); ChangeDifficultyRequested?.Invoke(); });
            }
        }

        /// <summary>Opens the full-screen settings page; the HUD and board come back when it closes.</summary>
        private void OpenSettings()
        {
            _settingsOpen = true;
            UpdateInput(); // the settings popup opens over the game, which stays visible (blurred) behind it
            // Playing the AI: changing "who moves first" starts a new game right away
            settingsScreen.Open(_mode == MatchMode.VsAI, () =>
            {
                _settingsOpen = false;
                SetCovered(resultScreen != null && resultScreen.IsShown);
                _measuredScreen = default;
                Refresh();
            }, OnFirstPlayerChosen);
        }

        /// <summary>Hides (or shows again) the HUD and the board behind a full-screen page.</summary>
        private void SetCovered(bool covered)
        {
            if (hud != null) hud.SetActive(!covered);
            board.gameObject.SetActive(!covered);
        }

        private Vector2 _measuredCanvasSize;

        private void LateUpdate()
        {
            // Re-measure the board area when the screen is resized/rotated, the safe area changes, or the canvas resizes
            var canvasRT = CanvasRect();
            var screen = new Vector2Int(Screen.width, Screen.height);
            var canvasSize = canvasRT != null ? canvasRT.rect.size : Vector2.zero;
            if (hud != null && !hud.activeInHierarchy) return;
            if (screen == _measuredScreen && Screen.safeArea == _measuredSafe && canvasSize == _measuredCanvasSize) return;
            _measuredScreen = screen;
            _measuredSafe = Screen.safeArea;
            _measuredCanvasSize = canvasSize;
            MeasureBoardArea();
        }

        private readonly Vector3[] _corners = new Vector3[4];

        private RectTransform CanvasRect()
        {
            var c = areaTop != null ? areaTop.GetComponentInParent<Canvas>() : null;
            return c != null ? (RectTransform)c.rootCanvas.transform : null;
        }

        /// <summary>
        /// Measures the free space between the top block and the bottom bar as a fraction of the canvas
        /// height (0..1 from the bottom). Measured in canvas space, so it is independent of camera zoom/pan.
        /// </summary>
        private void MeasureBoardArea()
        {
            var canvasRT = CanvasRect();
            if (areaTop == null || areaBottom == null || canvasRT == null || Screen.height <= 0) return;
            Canvas.ForceUpdateCanvases();
            var r = canvasRT.rect;
            if (r.height <= 0f) return;
            areaTop.GetWorldCorners(_corners);
            float top = (canvasRT.InverseTransformPoint(_corners[0]).y - r.yMin) / r.height;
            areaBottom.GetWorldCorners(_corners);
            float bottom = (canvasRT.InverseTransformPoint(_corners[1]).y - r.yMin) / r.height;
            float gap = areaGapPixels / Screen.height;
            top -= gap; bottom += gap;
            if (top <= bottom) return;
            board.SetBoardArea(bottom, top - bottom);
        }

        private Color _savedBackdrop;
        private bool _backdropSet;

        private void OnEnable()
        {
            _measuredScreen = default; // re-measure when coming back to the game screen
            Loc.Changed += Refresh;
            var cam = Camera.main;
            if (cam != null) { _savedBackdrop = cam.backgroundColor; cam.backgroundColor = backdrop; _backdropSet = true; }
        }

        private void OnDisable()
        {
            if (_backdropSet && Camera.main != null) Camera.main.backgroundColor = _savedBackdrop;
            _backdropSet = false;
            Loc.Changed -= Refresh;
            // Leaving the game screen: stop the AI and drop the pending popup. Both resume on return.
            CancelAI();
            CancelResult();
            if (resultScreen != null) resultScreen.Hide();
        }

        private void OnDestroy()
        {
            if (board != null) { board.GameStarted -= OnGameAttached; board.ZoomChanged -= OnZoomChanged; }
            Unsubscribe();
            CancelAI();
            CancelResult();
        }

        private void OnZoomChanged(bool zoomed)
        {
            if (fitButton != null) fitButton.gameObject.SetActive(zoomed);
            if (boardSizeText != null) boardSizeText.gameObject.SetActive(!zoomed);
        }

        /// <summary>Changes who moves first vs the AI: starts a new game right away, keeping the score.</summary>
        private void OnFirstPlayerChosen(bool aiFirst)
        {
            if (_mode != MatchMode.VsAI) return;
            _human = aiFirst ? Piece.O : Piece.X; // whoever moves first plays X
            AiPrefs.LastHuman = _human;
            NewGame();
        }

        private void GoToMenu() => PageTurn.Run(() =>
        {
            HideResult();
            MenuRequested?.Invoke();
        });

        // ================= New game / undo =================

        public void NewGame()
        {
            HideResult();
            CancelAI();
            if (_mode == MatchMode.TwoPlayer)
            {
                // Alternate the first player every game
                var first = PlayerPrefs.GetInt(Key2pNextFirst, (int)Piece.X) == (int)Piece.O ? Piece.O : Piece.X;
                PlayerPrefs.SetInt(Key2pNextFirst, (int)first.Opponent());
                board.NewGame(first);
            }
            else
            {
                // Vs AI: X always moves first; the player chose X (first) or O (second)
                _aiGameDifficulty = _difficulty;
                _aiGameHuman = _human;
                board.NewGame(Piece.X);
            }
        }

        public void Undo()
        {
            if (_game == null) return;
            HideResult();
            CancelAI();
            int before = _game.History.Count;

            if (_mode == MatchMode.TwoPlayer)
            {
                UndoOne();
            }
            else
            {
                if (!HasHumanMove()) { Refresh(); return; }
                // Step back until one of the player's moves is removed (plus the AI reply after it)
                while (_game.History.Count > 0)
                {
                    var last = _game.Board.Get(_game.History[_game.History.Count - 1]);
                    UndoOne();
                    if (last == _human) break;
                }
            }
            if (_game.History.Count < before && Feedback.Instance != null) Feedback.Instance.Erase();
            Refresh();
            MaybeStartAI();
        }

        private void UndoOne()
        {
            if (_game.IsOver && _game.Winner != Piece.None) AddWin(_game.Winner, -1); // undoing a winning move → take the point back
            _game.Undo();
        }

        private bool HasHumanMove()
        {
            foreach (var c in _game.History) if (_game.Board.Get(c) == _human) return true;
            return false;
        }

        // ================= Game events =================

        private void OnGameAttached(CaroGame game)
        {
            Unsubscribe();
            _game = game;
            if (_mode == MatchMode.TwoPlayer) _twoPlayerGame = game; else _aiGame = game;
            _game.PiecePlaced += OnPiecePlaced;
            _game.GameEnded += OnGameEnded;
            _game.MoveUndone += OnMoveUndone;
            _game.BoardResized += Refresh;
            Refresh();
            MaybeStartAI();
        }

        private void Unsubscribe()
        {
            if (_game == null) return;
            _game.PiecePlaced -= OnPiecePlaced;
            _game.GameEnded -= OnGameEnded;
            _game.MoveUndone -= OnMoveUndone;
            _game.BoardResized -= Refresh;
        }

        private void OnPiecePlaced(Cell c, Piece p)
        {
            if (Feedback.Instance != null) Feedback.Instance.Place(p);
            Refresh();
            MaybeStartAI();
        }

        private void OnMoveUndone(Cell c) => Refresh();

        private void OnGameEnded(Piece winner)
        {
            if (winner != Piece.None) AddWin(winner, +1);
            var fb = Feedback.Instance;
            if (fb != null)
            {
                if (winner == Piece.None) fb.Draw();
                else if (_mode == MatchMode.VsAI && winner != _human) fb.Lose();
                else fb.Win();
            }
            Refresh();
            if (resultScreen != null && isActiveAndEnabled)
            {
                CancelResult();
                _resultCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
                ShowResultAsync(_resultCts).Forget();
            }
        }

        // ================= AI turn =================

        private void MaybeStartAI()
        {
            if (!IsAITurn || _aiCts != null || !isActiveAndEnabled) return;
            _aiCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            AIMoveAsync(_aiCts).Forget();
        }

        private async UniTaskVoid AIMoveAsync(CancellationTokenSource cts)
        {
            var ct = cts.Token;
            var game = _game;
            var piece = game.Turn;
            var snapshot = game.Board.Clone();
            var ai = new CaroAI(_difficulty) { TimeBudgetMs = aiTimeBudgetMs };
            float minThink = aiMinThinkSeconds != null && aiMinThinkSeconds.Length > (int)_difficulty ? aiMinThinkSeconds[(int)_difficulty] : 0.5f;
            Refresh(); // shows the "AI is thinking" status and locks the board

            Cell move;
            try
            {
                // The AI thinks on a worker thread with a copy of the board so the game never stutters,
                // while a minimum delay gives each level its own pace.
                var minDelay = UniTask.Delay(TimeSpan.FromSeconds(minThink), DelayType.Realtime, cancellationToken: ct);
                move = await UniTask.RunOnThreadPool(() => ai.ChooseMove(snapshot, piece, ct), cancellationToken: ct);
                await minDelay;
            }
            catch (OperationCanceledException)
            {
                return; // CancelAI already cleared _aiCts
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (_aiCts == cts) _aiCts = null;
                Refresh();
                return;
            }

            // Clear before placing: TryPlace raises PiecePlaced, which may start the next AI turn.
            if (_aiCts == cts) _aiCts = null;
            if (ct.IsCancellationRequested || game != _game || game.IsOver || game.Turn != piece) return;
            game.TryPlace(move);
        }

        private void CancelAI()
        {
            _aiCts?.Cancel();
            _aiCts = null;
        }

        // ================= Result screen =================

        private async UniTaskVoid ShowResultAsync(CancellationTokenSource cts)
        {
            UpdateInput();
            bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(resultDelay), cancellationToken: cts.Token)
                .SuppressCancellationThrow();
            if (cancelled) return; // CancelResult already cleared _resultCts
            if (_resultCts == cts) _resultCts = null;
            if (_game == null || !_game.IsOver) { UpdateInput(); return; }

            ResultKind kind;
            string title, subtitle;
            if (_game.IsDraw)
            {
                kind = ResultKind.Draw;
                title = Loc.T("Hoà!");
                subtitle = Loc.T("Bàn cờ đã kín");
            }
            else if (_mode == MatchMode.VsAI)
            {
                bool won = _game.Winner == _human;
                kind = won ? ResultKind.Win : ResultKind.Lose;
                title = Loc.F(won ? "<color={0}>Bạn</color> <color={1}>thắng!</color>"
                                  : "<color={0}>Máy</color> <color={1}>thắng!</color>", Hex(oColor), Hex(xColor));
                subtitle = Loc.T(won ? "Nối 5 quân liền thắng!" : "Thử lại nhé!");
            }
            else
            {
                kind = ResultKind.Win;
                var w = _game.Winner;
                title = Loc.F("<color={0}>Người chơi {2}</color> <color={1}>thắng!</color>",
                    Hex(w == Piece.X ? xColor : oColor), Hex(inkColor), w);
                subtitle = Loc.T("Nối 5 quân liền thắng!");
            }
            // The result page is turned to over the board
            PageTurn.Run(() =>
            {
                if (_game == null || !_game.IsOver) return; // undone or left while the page was being captured
                SetCovered(true);
                resultScreen.Show(kind, title, subtitle, LeftNameText(), RightNameText(),
                    ScoreOf(LeftPiece), ScoreOf(RightPiece), PieceColor(LeftPiece), PieceColor(RightPiece),
                    _mode == MatchMode.VsAI);
                UpdateInput();
            });
        }

        private void HideResult()
        {
            CancelResult();
            bool wasShown = resultScreen != null && resultScreen.IsShown;
            if (resultScreen != null) resultScreen.Hide();
            if (wasShown && !_settingsOpen) { SetCovered(false); _measuredScreen = default; }
            UpdateInput();
        }

        private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        private void CancelResult()
        {
            _resultCts?.Cancel();
            _resultCts = null;
        }

        // ================= Score =================

        public static (int x, int o) LoadTwoPlayerScore() =>
            (PlayerPrefs.GetInt(Key2pX, 0), PlayerPrefs.GetInt(Key2pO, 0));

        public static (int human, int ai) LoadAIScore(Difficulty d) =>
            (PlayerPrefs.GetInt(KeyAiHuman(d), 0), PlayerPrefs.GetInt(KeyAiMachine(d), 0));

        public static void ResetTwoPlayerScore()
        {
            PlayerPrefs.SetInt(Key2pX, 0); PlayerPrefs.SetInt(Key2pO, 0); PlayerPrefs.Save();
        }

        public static void ResetAIScore(Difficulty d)
        {
            PlayerPrefs.SetInt(KeyAiHuman(d), 0); PlayerPrefs.SetInt(KeyAiMachine(d), 0); PlayerPrefs.Save();
        }

        private void AddWin(Piece winner, int delta)
        {
            string key = _mode == MatchMode.TwoPlayer
                ? (winner == Piece.X ? Key2pX : Key2pO)
                : (winner == _human ? KeyAiHuman(_difficulty) : KeyAiMachine(_difficulty));
            PlayerPrefs.SetInt(key, Mathf.Max(0, PlayerPrefs.GetInt(key, 0) + delta));
            PlayerPrefs.Save();
        }

        private int ScoreOf(Piece p)
        {
            if (_mode == MatchMode.TwoPlayer)
            {
                var (x, o) = LoadTwoPlayerScore();
                return p == Piece.X ? x : o;
            }
            var (h, a) = LoadAIScore(_difficulty);
            return p == _human ? h : a;
        }

        // Score panel sides: vs AI the player is on the left and the AI on the right; two players X left, O right.
        private Piece LeftPiece => _mode == MatchMode.VsAI ? _human : Piece.X;
        private Piece RightPiece => LeftPiece.Opponent();

        private Color PieceColor(Piece p) => p == Piece.X ? xColor : oColor;

        // Turn bar sides: vs AI the robot is on the left and the player on the right; two players X left, O right.
        private Piece TurnLeftPiece => _mode == MatchMode.VsAI ? AIPiece : Piece.X;

        /// <summary>Robot for the AI, otherwise a player icon in the colour of the piece.</summary>
        private Sprite AvatarOf(Piece p)
        {
            if (_mode == MatchMode.VsAI && p == AIPiece) return robotSprite;
            return p == Piece.X ? personXSprite : personOSprite;
        }

        private string LeftNameText() => SideName(LeftPiece);
        private string RightNameText() => SideName(RightPiece);

        private string SideName(Piece p)
        {
            string who = Loc.T(_mode == MatchMode.TwoPlayer ? "Người chơi" : (p == _human ? "Bạn" : "Máy"));
            return $"{who} (<color={Hex(PieceColor(p))}>{p}</color>)";
        }

        // ================= HUD =================

        public static string DifficultyName(Difficulty d) =>
            Loc.T(d == Difficulty.Easy ? "Dễ" : d == Difficulty.Medium ? "Vừa" : "Khó");

        private string WinnerText(Piece winner)
        {
            if (_mode == MatchMode.VsAI) return Loc.T(winner == _human ? "Bạn thắng!" : "Máy thắng!");
            return Loc.F("Người chơi {0} thắng!", winner);
        }

        private void UpdateInput()
        {
            bool popupBlocking = _resultCts != null || (resultScreen != null && resultScreen.IsShown) || _settingsOpen;
            board.InputEnabled = !popupBlocking && !IsAITurn;
        }

        private void Refresh()
        {
            UpdateInput();
            if (_game == null) return;

            if (modeLabel != null)
                modeLabel.text = _mode == MatchMode.TwoPlayer ? Loc.T("Chơi 2 người") : Loc.F("Chơi với máy - {0}", DifficultyName(_difficulty));
            if (leftName != null) leftName.text = LeftNameText();
            if (rightName != null) rightName.text = RightNameText();
            if (leftScore != null) { leftScore.text = ScoreOf(LeftPiece).ToString(); leftScore.color = PieceColor(LeftPiece); }
            if (rightScore != null) { rightScore.text = ScoreOf(RightPiece).ToString(); rightScore.color = PieceColor(RightPiece); }
            if (aiIcon != null) aiIcon.SetActive(_mode == MatchMode.VsAI);
            if (leftIcon != null) leftIcon.sprite = AvatarOf(LeftPiece);
            if (rightIcon != null) rightIcon.sprite = AvatarOf(RightPiece);

            if (turnAvatars != null)
            {
                var tl = TurnLeftPiece;
                turnAvatars.SetSprites(AvatarOf(tl), AvatarOf(tl.Opponent()));
                var lit = _game.IsOver ? (_game.IsDraw ? Piece.None : _game.Winner) : _game.Turn;
                turnAvatars.SetActive(lit == Piece.None ? TurnAvatars.Side.None
                    : lit == tl ? TurnAvatars.Side.Left : TurnAvatars.Side.Right);
            }

            if (statusText != null)
            {
                statusText.color = inkColor;
                var turn = _game.Turn;
                string piece = $"(<color={Hex(PieceColor(turn))}>{turn}</color>)";
                if (_game.IsDraw) statusText.text = Loc.T("Hoà! Bàn cờ đã kín");
                else if (_game.IsOver) statusText.text = WinnerText(_game.Winner);
                else statusText.text = Loc.F(IsAITurn ? "Máy đang nghĩ… {0}"
                    : _mode == MatchMode.VsAI ? "Lượt của bạn {0}"
                    : "Lượt của người chơi {0}", piece);
            }

            if (boardSizeText != null)
            {
                var b = _game.Board;
                boardSizeText.text = Loc.F("Bàn {0} × {1}  ·  tối đa {2} × {2}", b.Width, b.Height, Board.MaxSize);
            }

            if (undoButton != null)
                undoButton.interactable = _mode == MatchMode.TwoPlayer ? _game.History.Count > 0 : HasHumanMove();
        }
    }
}
