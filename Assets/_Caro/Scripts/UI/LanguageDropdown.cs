using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Caro.Game
{
    /// <summary>
    /// Language picker of the settings popup: a paper field showing the current language with a chevron;
    /// tapping it drops down a list with one row per language (a tick marks the current one). Tapping a row
    /// switches the language at once; tapping anywhere else closes the list. All visuals are art assets.
    /// </summary>
    public class LanguageDropdown : MonoBehaviour
    {
        [SerializeField] private Button field;
        [SerializeField] private TMP_Text fieldLabel;
        [Tooltip("Chevron on the field: points down when closed, up when open")]
        [SerializeField] private Image chevron;
        [SerializeField] private Sprite chevronDown;
        [SerializeField] private Sprite chevronUp;

        [Header("List")]
        [Tooltip("The dropped-down list; hidden while closed")]
        [SerializeField] private GameObject list;
        [Tooltip("Invisible full-screen button under the list that closes it")]
        [SerializeField] private Button blocker;
        [Tooltip("One row per Language value, in enum order")]
        [SerializeField] private Button[] rows = new Button[2];
        [SerializeField] private TMP_Text[] rowLabels = new TMP_Text[2];
        [SerializeField] private GameObject[] rowChecks = new GameObject[2];

        private Quaternion _chevronRest;

        private void Awake()
        {
            if (chevron != null) _chevronRest = chevron.rectTransform.localRotation;
            field.onClick.AddListener(() => SetOpen(!list.activeSelf));
            if (blocker != null) blocker.onClick.AddListener(() => SetOpen(false));
            for (int i = 0; i < rows.Length; i++)
            {
                var lang = (Language)i;
                rows[i].onClick.AddListener(() =>
                {
                    Loc.Current = lang;
                    SetOpen(false);
                });
            }
        }

        private void OnEnable()
        {
            Loc.Changed += Refresh;
            SetOpen(false);
        }

        private void OnDisable() => Loc.Changed -= Refresh;

        public void SetOpen(bool open)
        {
            list.SetActive(open);
            if (blocker != null) blocker.gameObject.SetActive(open);
            if (open) transform.SetAsLastSibling(); // the list draws over the rows below it
            Refresh();
            if (chevron != null)
            {
                bool upArt = chevronUp != null && chevronUp != chevronDown;
                chevron.sprite = open && upArt ? chevronUp : chevronDown;
                // Without separate up-chevron art the down chevron is turned half a turn
                chevron.rectTransform.localRotation = open && !upArt ? _chevronRest * Quaternion.Euler(0, 0, 180) : _chevronRest;
            }
        }

        private void Refresh()
        {
            var current = Loc.Current;
            if (fieldLabel != null) fieldLabel.text = Loc.NativeName(current);
            for (int i = 0; i < rows.Length; i++)
            {
                if (i < rowLabels.Length && rowLabels[i] != null) rowLabels[i].text = Loc.NativeName((Language)i);
                if (i < rowChecks.Length && rowChecks[i] != null) rowChecks[i].SetActive((Language)i == current);
            }
        }
    }
}
