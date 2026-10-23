using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Caro.Game
{
    /// <summary>
    /// A row of mutually exclusive buttons (difficulty, who moves first, theme). The selected option shows
    /// <see cref="selectedSprite"/>, the others <see cref="normalSprite"/>, and unavailable ones
    /// <see cref="disabledSprite"/> — all three are art assets; nothing is drawn in code.
    /// </summary>
    public class OptionGroup : MonoBehaviour
    {
        [SerializeField] private Button[] options = Array.Empty<Button>();
        [SerializeField] private Sprite selectedSprite;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite disabledSprite;
        [SerializeField] private Color selectedText = new Color(0.16f, 0.17f, 0.22f);
        [SerializeField] private Color normalText = new Color(0.30f, 0.33f, 0.38f);
        [SerializeField] private Color disabledText = new Color(0.55f, 0.57f, 0.60f);

        /// <summary>Raised when the player picks a different option.</summary>
        public event Action<int> Changed;

        public int Selected { get; private set; }

        private bool _interactable = true;
        private bool[] _enabled;
        private bool _wired;

        private void Awake() => Wire();

        private void Wire()
        {
            if (_wired) return;
            _wired = true;
            _enabled = new bool[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                _enabled[i] = true;
                int index = i;
                options[i].transition = Selectable.Transition.None;
                options[i].onClick.AddListener(() => Pick(index));
            }
        }

        /// <summary>Selects an option without raising <see cref="Changed"/>.</summary>
        public void Set(int index)
        {
            Wire();
            Selected = Mathf.Clamp(index, 0, options.Length - 1);
            Refresh();
        }

        /// <summary>Greys the whole row out (e.g. difficulty while two-player mode is chosen).</summary>
        public void SetInteractable(bool interactable)
        {
            Wire();
            _interactable = interactable;
            Refresh();
        }

        /// <summary>Makes a single option unavailable (e.g. a theme whose art is not delivered yet).</summary>
        public void SetOptionEnabled(int index, bool enabled)
        {
            Wire();
            _enabled[index] = enabled;
            Refresh();
        }

        private void Pick(int index)
        {
            if (!_interactable || !_enabled[index] || index == Selected) return;
            Selected = index;
            Refresh();
            Changed?.Invoke(index);
        }

        private void Refresh()
        {
            for (int i = 0; i < options.Length; i++)
            {
                bool on = _interactable && _enabled[i];
                options[i].interactable = on;
                var image = options[i].targetGraphic as Image;
                if (image != null) image.sprite = !on ? disabledSprite : i == Selected ? selectedSprite : normalSprite;
                var label = options[i].GetComponentInChildren<TMP_Text>();
                if (label != null) label.color = !on ? disabledText : i == Selected ? selectedText : normalText;
            }
        }
    }
}
