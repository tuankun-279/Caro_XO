using Caro.AI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Caro.Game
{
    /// <summary>
    /// Main menu (mockup 1) and navigation between the five screens:
    /// menu → mode selection → game → result, with settings reachable from the menu and the game.
    /// Going back to the menu mid-game keeps the game so it can be resumed.
    /// </summary>
    public class MenuScreen : MonoBehaviour
    {
        [SerializeField] private GameScreen gameScreen;
        [SerializeField] private BoardView board;
        [SerializeField] private ModeSelectScreen modeSelect;
        [SerializeField] private SettingsScreen settings;

        [Header("Buttons")]
        [SerializeField] private Button vsAiButton;
        [SerializeField] private Button twoPlayerButton;
        [SerializeField] private Button settingsButton;

        [Header("Labels")]
        [SerializeField] private TMP_Text vsAiLabel;
        [SerializeField] private TMP_Text twoPlayerLabel;
        [Tooltip("Recent scores: Máy – Dễ, Máy – Vừa, Máy – Khó, 2 người")]
        [SerializeField] private TMP_Text[] recentScores = new TMP_Text[4];

        private void Awake()
        {
            vsAiButton.onClick.AddListener(() => OpenModeSelect(MatchMode.VsAI));
            twoPlayerButton.onClick.AddListener(() => OpenModeSelect(MatchMode.TwoPlayer));
            settingsButton.onClick.AddListener(() =>
            {
                gameObject.SetActive(false);
                settings.Open(false, ShowMenu);
            });

            modeSelect.BackRequested += ShowMenu;
            modeSelect.StartRequested += (mode, d) =>
            {
                EnterGame();
                if (mode == MatchMode.VsAI) gameScreen.OpenVsAI(d, AiPrefs.LastHuman);
                else gameScreen.OpenTwoPlayer(forceNew: true);
            };
            modeSelect.ResumeRequested += mode =>
            {
                EnterGame();
                if (mode == MatchMode.VsAI) gameScreen.ResumeVsAI();
                else gameScreen.OpenTwoPlayer();
            };

            gameScreen.MenuRequested += ShowMenu;
            gameScreen.ChangeDifficultyRequested += () => OpenModeSelect(MatchMode.VsAI);
        }

        private void Start() => ShowMenu();

        public void ShowMenu()
        {
            gameScreen.gameObject.SetActive(false);
            board.gameObject.SetActive(false);
            modeSelect.Close();
            if (settings.IsShown) settings.Close();
            gameObject.SetActive(true);
            Refresh();
        }

        private void OpenModeSelect(MatchMode mode)
        {
            gameObject.SetActive(false);
            gameScreen.gameObject.SetActive(false);
            board.gameObject.SetActive(false);
            modeSelect.Open(mode, gameScreen);
        }

        private void EnterGame()
        {
            modeSelect.Close();
            gameObject.SetActive(false);
            board.gameObject.SetActive(true);      // enable the board first so BoardView is ready
            gameScreen.gameObject.SetActive(true);
        }

        private void Refresh()
        {
            for (int i = 0; i < 3; i++)
            {
                var (h, a) = GameScreen.LoadAIScore((Difficulty)i);
                if (recentScores[i] != null) recentScores[i].text = $"{h} - {a}";
            }
            var (x, o) = GameScreen.LoadTwoPlayerScore();
            if (recentScores[3] != null) recentScores[3].text = $"{x} - {o}";

            vsAiLabel.text = gameScreen.HasGameInProgress(MatchMode.VsAI) ? "Chơi với máy · đang dở" : "Chơi với máy";
            twoPlayerLabel.text = gameScreen.HasGameInProgress(MatchMode.TwoPlayer) ? "Chơi 2 người · đang dở" : "Chơi 2 người";
        }
    }
}
