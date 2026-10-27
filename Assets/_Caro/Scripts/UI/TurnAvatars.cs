using UnityEngine;
using UnityEngine.UI;

namespace Caro.Game
{
    /// <summary>
    /// The two avatars on either side of the turn label. The side whose turn it is is shown in full colour,
    /// a little larger and gently bobbing; the other side is faded and smaller. GameScreen sets the sprites
    /// for the current mode (robot / player, or two players) and which side is active.
    /// </summary>
    public class TurnAvatars : MonoBehaviour
    {
        public enum Side { None, Left, Right }

        [SerializeField] private Image left;
        [SerializeField] private Image right;
        [SerializeField] private float activeScale = 1.15f;
        [SerializeField] private float idleScale = 0.85f;
        [SerializeField] private Color idleTint = new Color(1f, 1f, 1f, 0.45f); // faded but keeps its colour (red X / blue O)
        [Tooltip("How fast the avatars ease to their new look")]
        [SerializeField] private float speed = 10f;
        [Tooltip("Up/down movement of the active avatar, in canvas units")]
        [SerializeField] private float bobHeight = 5f;
        [SerializeField] private float bobSpeed = 4f;

        private Side _active = Side.None;
        private Vector2 _leftBase, _rightBase;

        private void Awake()
        {
            if (left != null) _leftBase = left.rectTransform.anchoredPosition;
            if (right != null) _rightBase = right.rectTransform.anchoredPosition;
        }

        public void SetSprites(Sprite leftSprite, Sprite rightSprite)
        {
            if (left != null && leftSprite != null) left.sprite = leftSprite;
            if (right != null && rightSprite != null) right.sprite = rightSprite;
        }

        public void SetActive(Side side) => _active = side;

        private void Update()
        {
            float k = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
            float bob = Mathf.Sin(Time.unscaledTime * bobSpeed) * bobHeight;
            Apply(left, _leftBase, _active == Side.Left, k, bob);
            Apply(right, _rightBase, _active == Side.Right, k, bob);
        }

        private static readonly Color Full = Color.white;

        private void Apply(Image img, Vector2 basePos, bool on, float k, float bob)
        {
            if (img == null) return;
            var rt = img.rectTransform;
            float s = on ? activeScale : idleScale;
            rt.localScale = Vector3.Lerp(rt.localScale, new Vector3(s, s, 1f), k);
            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, basePos + new Vector2(0f, on ? bob : 0f), k);
            img.color = Color.Lerp(img.color, on ? Full : idleTint, k);
        }
    }
}
