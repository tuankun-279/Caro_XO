using System;
using Caro.AI;
using Caro.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Caro.Game
{
    /// <summary>
    /// Full-screen settings page (mockup 5): volume, sound and vibration, who moves first against the AI,
    /// default AI difficulty and the visual theme. Opened from the menu or the game screen; the caller
    /// passes what to do when it closes.
    /// </summary>
    public class SettingsScreen : MonoBehaviour
    {
        [SerializeField] private Button backButton;

        [Header("Sound")]
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Button soundToggle;
        [SerializeField] private Button vibrationToggle;
        [Tooltip("Toggle art: on / off")]
        [SerializeField] private Sprite toggleOn;
        [SerializeField] private Sprite toggleOff;

        [Header("Game options")]
        [Tooltip("0 = Bạn (you play X and move first), 1 = Máy")]
        [SerializeField] private OptionGroup firstPlayer;
        [SerializeField] private TMP_Text firstPlayerNote;
        [Tooltip("Dễ, Vừa, Khó — the difficulty of the next game against the AI")]
        [SerializeField] private OptionGroup difficulty;
        [Tooltip("Mặc định, Giấy ô li, Tối giản")]
        [SerializeField] private OptionGroup theme;

        public bool IsShown => gameObject.activeSelf;

        private Action _onClosed;
        private Action<bool> _onFirstPlayer;
        private bool _inAiGame;

        private void Awake()
        {
            backButton.onClick.AddListener(Close);
            volumeSlider.onValueChanged.AddListener(v => Feedback.Volume = v);
            soundToggle.onClick.AddListener(() => { Feedback.SoundOn = !Feedback.SoundOn; Refresh(); });
            vibrationToggle.onClick.AddListener(() => { Feedback.VibrationOn = !Feedback.VibrationOn; Refresh(); });
            difficulty.Changed += i => AiPrefs.LastDifficulty = (Difficulty)i;
            firstPlayer.Changed += i =>
            {
                bool aiFirst = i == 1;
                AiPrefs.LastHuman = aiFirst ? Piece.O : Piece.X;
                if (!_inAiGame) return;
                var callback = _onFirstPlayer;
                Close();
                callback?.Invoke(aiFirst);
            };
        }

        /// <summary>
        /// Opens the page. inAiGame: opened during a game against the AI, so changing who moves first
        /// closes the page and starts a new game (onFirstPlayer).
        /// </summary>
        public void Open(bool inAiGame, Action onClosed, Action<bool> onFirstPlayer = null)
        {
            _inAiGame = inAiGame;
            _onClosed = onClosed;
            _onFirstPlayer = onFirstPlayer;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            volumeSlider.SetValueWithoutNotify(Feedback.Volume);
            firstPlayer.Set(AiPrefs.LastHuman == Piece.O ? 1 : 0);
            difficulty.Set((int)AiPrefs.LastDifficulty);
            theme.Set(0);
            theme.SetOptionEnabled(1, false); // "Giấy ô li" and "Tối giản" wait for their art
            theme.SetOptionEnabled(2, false);
            if (firstPlayerNote != null)
                firstPlayerNote.text = inAiGame ? "Đổi sẽ bắt đầu ván mới · tỉ số giữ nguyên" : "Áp dụng cho ván đấu máy tiếp theo";
            Refresh();
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;
            gameObject.SetActive(false);
            var callback = _onClosed;
            _onClosed = null;
            _onFirstPlayer = null;
            callback?.Invoke();
        }

        private void Refresh()
        {
            Paint(soundToggle, Feedback.SoundOn);
            Paint(vibrationToggle, Feedback.VibrationOn);
            volumeSlider.interactable = Feedback.SoundOn;
        }

        private void Paint(Button b, bool on)
        {
            if (b.targetGraphic is Image image) image.sprite = on ? toggleOn : toggleOff;
            var label = b.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = on ? "Bật" : "Tắt";
        }
    }
}
