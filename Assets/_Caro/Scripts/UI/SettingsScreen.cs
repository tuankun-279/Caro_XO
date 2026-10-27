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
        [Tooltip("The paper card holding the settings; it scales in when the popup opens")]
        [SerializeField] private RectTransform popup;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private float openSeconds = 0.18f;
        [Tooltip("Final size of the card, so some of the blurred screen shows around it")]
        [SerializeField] private float popupScale = 0.92f;
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
            if (firstPlayerNote != null)
                firstPlayerNote.text = inAiGame ? "Đổi sẽ bắt đầu ván mới · tỉ số giữ nguyên" : "Áp dụng cho ván đấu máy tiếp theo";
            Refresh();

            if (_opening != null) StopCoroutine(_opening);
            _opening = StartCoroutine(OpenRoutine());
        }

        /// <summary>Hidden for one frame while the screen behind is captured, then blurred in and scaled up.</summary>
        private IEnumerator OpenRoutine()
        {
            if (group != null) { group.alpha = 0f; group.blocksRaycasts = true; }
            if (blurBackdrop != null) blurBackdrop.enabled = false;
            yield return new WaitForEndOfFrame();

            if (blurBackdrop != null)
            {
                _blur = ScreenBlur.Capture(_blur, blurHalvings, out bool flipped);
                blurBackdrop.texture = _blur;
                blurBackdrop.uvRect = flipped ? new Rect(0, 1, 1, -1) : new Rect(0, 0, 1, 1);
                blurBackdrop.enabled = true;
            }

            for (float t = 0f; t < openSeconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / openSeconds);
                if (group != null) group.alpha = k;
                if (popup != null) popup.localScale = Vector3.one * (popupScale * Mathf.Lerp(0.92f, 1f, k));
                yield return null;
            }
            if (group != null) group.alpha = 1f;
            if (popup != null) popup.localScale = Vector3.one * popupScale;
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
