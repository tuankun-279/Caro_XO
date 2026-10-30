using System;
using System.Collections;
using Caro.AI;
using Caro.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Caro.Game
{
    /// <summary>
    /// Settings popup: volume, sound and vibration, who moves first against the AI, default AI difficulty and
    /// the visual theme. It opens over the current screen, which is shown blurred behind it; the × button or a
    /// tap on the blurred area closes it. Opened from the menu or the game screen; the caller passes what to
    /// do when it closes.
    /// </summary>
    public class SettingsScreen : MonoBehaviour
    {
        [Tooltip("× button at the top right of the popup")]
        [SerializeField] private Button backButton;

        [Header("Popup")]
        [Tooltip("Full-screen blurred snapshot of the screen behind; tapping it closes the popup")]
        [SerializeField] private RawImage blurBackdrop;
        [SerializeField] private Button backdropButton;
        [Tooltip("The paper card holding the settings; the Animator (PopupOpen clip) fades and scales it in")]
        [SerializeField] private RectTransform popup;
        [SerializeField] private CanvasGroup group;
        [Tooltip("How many times the snapshot is halved before being shown (more = blurrier)")]
        [SerializeField] private int blurHalvings = 4;

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
        private RenderTexture _blur;
        private Coroutine _opening;

        private void Awake()
        {
            backButton.onClick.AddListener(Close);
            if (backdropButton != null) backdropButton.onClick.AddListener(Close);
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

        private void OnEnable() => Loc.Changed += Refresh;
        private void OnDisable() => Loc.Changed -= Refresh;

        private void OnDestroy()
        {
            if (_blur != null) _blur.Release();
        }

        /// <summary>
        /// Opens the popup. inAiGame: opened during a game against the AI, so changing who moves first
        /// closes it and starts a new game (onFirstPlayer).
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
            Refresh();

            if (_opening != null) StopCoroutine(_opening);
            _opening = StartCoroutine(OpenRoutine());
        }

        /// <summary>
        /// Captures the screen behind for the blurred backdrop. The card's fade and scale-in is the PopupOpen
        /// clip of the Animator, which starts each time the popup is shown and keeps the card invisible on its
        /// first frames, the ones captured here.
        /// </summary>
        private IEnumerator OpenRoutine()
        {
            if (blurBackdrop != null) blurBackdrop.enabled = false;
            yield return new WaitForEndOfFrame();

            if (blurBackdrop != null)
            {
                _blur = ScreenBlur.Capture(_blur, blurHalvings, out bool flipped);
                blurBackdrop.texture = _blur;
                blurBackdrop.uvRect = flipped ? new Rect(0, 1, 1, -1) : new Rect(0, 0, 1, 1);
                blurBackdrop.enabled = true;
            }
            _opening = null;
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;
            if (_opening != null) { StopCoroutine(_opening); _opening = null; }
            gameObject.SetActive(false);
            var callback = _onClosed;
            _onClosed = null;
            _onFirstPlayer = null;
            callback?.Invoke();
        }

        private void Refresh()
        {
            if (firstPlayerNote != null)
                firstPlayerNote.text = Loc.T(_inAiGame ? "Đổi sẽ bắt đầu ván mới · tỉ số giữ nguyên" : "Áp dụng cho ván đấu máy tiếp theo");
            Paint(soundToggle, Feedback.SoundOn);
            Paint(vibrationToggle, Feedback.VibrationOn);
            volumeSlider.interactable = Feedback.SoundOn;
        }

        private void Paint(Button b, bool on)
        {
            if (b.targetGraphic is Image image) image.sprite = on ? toggleOn : toggleOff;
            var label = b.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = Loc.T(on ? "Bật" : "Tắt");
        }
    }
}
