using TMPro;
using UnityEngine;

namespace Caro.Game
{
    /// <summary>
    /// A fixed label (title, button text…) shown in the current language. <see cref="key"/> is the Vietnamese
    /// text; the label is redrawn whenever it is shown and whenever the language changes.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [Tooltip("Vietnamese text, also the key into the English table of Loc")]
        [SerializeField, TextArea] private string key;

        private TMP_Text _text;

        public string Key
        {
            get => key;
            set { key = value; Apply(); }
        }

        private void OnEnable()
        {
            Loc.Changed += Apply;
            Apply();
        }

        private void OnDisable() => Loc.Changed -= Apply;

        private void Apply()
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            if (_text != null && !string.IsNullOrEmpty(key)) _text.text = Loc.T(key);
        }
    }
}
