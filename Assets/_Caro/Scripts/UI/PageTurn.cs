using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Caro.Game
{
    /// <summary>
    /// Screen change as a notebook page being turned. <see cref="Turn"/> snapshots the current screen onto the
    /// page of the transition canvas, switches to the new screen underneath, then triggers the Animator: the
    /// PageTurn clip folds the page over the spiral on the left, shades it and casts a shadow on the new page,
    /// and its last animation event, <see cref="TurnFinished"/>, hands the taps back. All motion is in the clip;
    /// this script only takes the snapshot and starts it.
    /// </summary>
    public class PageTurn : MonoBehaviour
    {
        private static readonly int TurnTrigger = Animator.StringToHash("Turn");

        [SerializeField] private Animator animator;
        [Tooltip("The turning page: shows the snapshot of the screen being left")]
        [SerializeField] private RawImage page;
        [Tooltip("Blocks taps while the page turns")]
        [SerializeField] private CanvasGroup group;

        /// <summary>The page turn of the scene, or null when there is none (the change is then instant).</summary>
        public static PageTurn Instance { get; private set; }

        private RenderTexture _snapshot;
        private bool _busy;

        private void Awake()
        {
            Instance = this;
            if (group != null) group.blocksRaycasts = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_snapshot != null) _snapshot.Release();
        }

        /// <summary>Runs <paramref name="swap"/> (the screen change) behind a page turn, or at once when no page turn is set up.</summary>
        public static void Run(Action swap)
        {
            if (Instance != null && Instance.isActiveAndEnabled) Instance.Turn(swap);
            else swap?.Invoke();
        }

        public void Turn(Action swap)
        {
            if (_busy) { swap?.Invoke(); return; } // a second change during a turn happens at once
            StartCoroutine(TurnRoutine(swap));
        }

        private IEnumerator TurnRoutine(Action swap)
        {
            _busy = true;
            if (group != null) group.blocksRaycasts = true;
            yield return new WaitForEndOfFrame();
            _snapshot = ScreenBlur.Snapshot(_snapshot, out bool flipped);
            page.texture = _snapshot;
            page.uvRect = flipped ? new Rect(0, 1, 1, -1) : new Rect(0, 0, 1, 1);
            swap?.Invoke();
            animator.SetTrigger(TurnTrigger);
        }

        /// <summary>Animation event at the end of the PageTurn clip.</summary>
        public void TurnFinished()
        {
            _busy = false;
            if (group != null) group.blocksRaycasts = false;
        }
    }
}
