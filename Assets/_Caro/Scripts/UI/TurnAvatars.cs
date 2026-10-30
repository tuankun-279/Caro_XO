using UnityEngine;
using UnityEngine.UI;

namespace Caro.Game
{
    /// <summary>
    /// The two avatars on either side of the turn label. GameScreen sets the sprites for the current mode
    /// (robot / player, or two players) and whose turn it is. The look is the Animator's: the TurnLeft,
    /// TurnRight and TurnNone clips make the active side larger, in full colour and gently bobbing and fade the
    /// other, and the controller cross-fades between them when the "Side" parameter changes.
    /// </summary>
    public class TurnAvatars : MonoBehaviour
    {
        public enum Side { None = 0, Left = 1, Right = 2 }

        private static readonly int SideParam = Animator.StringToHash("Side");

        [SerializeField] private Image left;
        [SerializeField] private Image right;
        [SerializeField] private Animator animator;

        public void SetSprites(Sprite leftSprite, Sprite rightSprite)
        {
            if (left != null && leftSprite != null) left.sprite = leftSprite;
            if (right != null && rightSprite != null) right.sprite = rightSprite;
        }

        public void SetActive(Side side)
        {
            if (animator != null && animator.isActiveAndEnabled) animator.SetInteger(SideParam, (int)side);
        }
    }
}
