using System;
using Caro.AI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Caro.Game
{
    /// <summary>
    /// "Chọn chế độ chơi" screen (mockup 2): pick "Chơi với máy" or "Chơi 2 người", the AI difficulty,
    /// then start a new game or resume the unfinished one of that mode.
    /// </summary>
    public class ModeSelectScreen : MonoBehaviour
    {
        [SerializeField] private Button backButton;

        [Header("Mode cards")]
        [SerializeField] private Button aiCard;
        [SerializeField] private Button twoPlayerCard;
        [Tooltip("Card art: chosen = normal sprite, not chosen = disabled sprite")]
        [SerializeField] private Sprite aiCardOn;
        [SerializeField] private Sprite aiCardOff;
        [SerializeField] private Sprite twoPlayerCardOn;
        [SerializeField] private Sprite twoPlayerCardOff;
        [Tooltip("Tick mark shown on the chosen card")]
        [SerializeField] private GameObject aiCheck;
        [SerializeField] private GameObject twoPlayerCheck;
        [Tooltip("Tint of the card that is not chosen")]
        [SerializeField] private Color offTint = new Color(1f, 1f, 1f, 0.55f);

        [Header("Difficulty (Dễ, Vừa, Khó)")]
        [SerializeField] private OptionGroup difficulty;
        [SerializeField] private TMP_Text difficultyLabel;

        [Header("Actions")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private TMP_Text resumeLabel;
        [SerializeField] private Button startButton;

        public event Action BackRequested;
        /// <summary>Start a new game in the chosen mode (difficulty only matters vs the AI).</summary>
        public event Action<MatchMode, Difficulty> StartRequested;
        /// <summary>Resume the unfinished game of the chosen mode.</summary>
        public event Action<MatchMode> ResumeRequested;

        private MatchMode _mode;
        private GameScreen _game;

        private void Awake()
        {
            backButton.onClick.AddListener(() => BackRequested?.Invoke());
            aiCard.onClick.AddListener(() => Select(MatchMode.VsAI));
            twoPlayerCard.onClick.AddListener(() => Select(MatchMode.TwoPlayer));
            difficulty.Changed += i => AiPrefs.LastDifficulty = (Difficulty)i;
            startButton.onClick.AddListener(() => StartRequested?.Invoke(_mode, (Difficulty)difficulty.Selected));
            resumeButton.onClick.AddListener(() => ResumeRequested?.Invoke(_mode));
        }

        /// <summary>Shows the screen with one mode preselected.</summary>
        public void Open(MatchMode mode, GameScreen game)
        {
            _game = game;
            gameObject.SetActive(true);
            difficulty.Set((int)AiPrefs.LastDifficulty);
            Select(mode);
        }

        public void Close() => gameObject.SetActive(false);

        private void Select(MatchMode mode)
        {
            _mode = mode;
            bool ai = mode == MatchMode.VsAI;
            ((Image)aiCard.targetGraphic).sprite = ai ? aiCardOn : aiCardOff;
            ((Image)twoPlayerCard.targetGraphic).sprite = ai ? twoPlayerCardOff : twoPlayerCardOn;
            aiCard.targetGraphic.color = ai ? Color.white : offTint;
            twoPlayerCard.targetGraphic.color = ai ? offTint : Color.white;
            if (aiCheck != null) aiCheck.SetActive(ai);
            if (twoPlayerCheck != null) twoPlayerCheck.SetActive(!ai);
            difficulty.SetInteractable(ai);
            if (difficultyLabel != null) difficultyLabel.alpha = ai ? 1f : 0.45f;

            bool resume = _game != null && _game.HasGameInProgress(mode);
            resumeButton.gameObject.SetActive(resume);
            if (resume)
            {
                if (ai)
                {
                    var (d, human) = _game.InProgressAISettings;
                    resumeLabel.text = $"Chơi tiếp ván dở  ·  {GameScreen.DifficultyName(d)}, bạn cầm {human}";
                }
                else resumeLabel.text = "Chơi tiếp ván 2 người đang dở";
            }
        }
    }
}
