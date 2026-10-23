using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Caro.Game
{
    public enum ResultKind { Win, Lose, Draw }

    /// <summary>
    /// Full-screen end-of-game page (mockup 4): title, illustration, score and the actions
    /// "Chơi lại", "Về menu", "Đổi độ khó" (vs AI only) and "Xem lại bàn cờ".
    /// </summary>
    public class ResultScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private Image art;
        [Tooltip("Illustration per result: win, lose, draw")]
        [SerializeField] private Sprite winArt;
        [SerializeField] private Sprite loseArt;
        [SerializeField] private Sprite drawArt;

        [Header("Score")]
        [SerializeField] private TMP_Text leftName;
        [SerializeField] private TMP_Text rightName;
        [SerializeField] private TMP_Text leftScore;
        [SerializeField] private TMP_Text rightScore;

        [Header("Buttons")]
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private Button changeDifficultyButton;
        [SerializeField] private Button reviewButton;

        public event Action PlayAgainClicked;
        public event Action MenuClicked;
        public event Action ChangeDifficultyClicked;
        public event Action ReviewClicked;

        public bool IsShown => gameObject.activeSelf;

        private Vector2 _reviewPos;
        private bool _ready;

        private void Awake() => Init();

        // The screen starts inactive, so Show() may run before Awake(): initialise on first use either way.
        private void Init()
        {
            if (_ready) return;
            _ready = true;
            _reviewPos = ((RectTransform)reviewButton.transform).anchoredPosition;
            playAgainButton.onClick.AddListener(() => PlayAgainClicked?.Invoke());
            menuButton.onClick.AddListener(() => MenuClicked?.Invoke());
            changeDifficultyButton.onClick.AddListener(() => ChangeDifficultyClicked?.Invoke());
            reviewButton.onClick.AddListener(() => ReviewClicked?.Invoke());
        }

        public void Show(ResultKind kind, string title, string subtitle,
            string left, string right, int leftValue, int rightValue, Color leftColor, Color rightColor, bool vsAi)
        {
            Init();
            titleText.text = title;
            subtitleText.text = subtitle;
            var sprite = kind == ResultKind.Win ? winArt : kind == ResultKind.Lose ? loseArt : drawArt;
            art.sprite = sprite;
            art.enabled = sprite != null;
            leftName.text = left;
            rightName.text = right;
            leftScore.text = leftValue.ToString();
            leftScore.color = leftColor;
            rightScore.text = rightValue.ToString();
            rightScore.color = rightColor;
            changeDifficultyButton.gameObject.SetActive(vsAi);
            // Without "Đổi độ khó" the review link moves up into its place so no gap is left
            ((RectTransform)reviewButton.transform).anchoredPosition =
                vsAi ? _reviewPos : ((RectTransform)changeDifficultyButton.transform).anchoredPosition;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
