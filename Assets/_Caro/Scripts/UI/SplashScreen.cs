using UnityEngine;
using UnityEngine.UI;

namespace Caro.Game
{
    /// <summary>
    /// Splash shown once each time the game is launched, over the menu that is already set up behind it.
    /// The Animator clip SplashIntro does everything that moves: the paper drops onto the cork board, the X and
    /// O stamp in, the logo pops, the robot waves, then the whole splash fades out. Its animation events call
    /// <see cref="Burst"/> to play the particle effects and <see cref="Finished"/> to remove the splash.
    /// A tap jumps the clip to its fade-out. Coming back from the background does not show it again, because
    /// the scene is only loaded at launch.
    /// </summary>
    public class SplashScreen : MonoBehaviour
    {
        private const string Clip = "SplashIntro";

        [SerializeField] private Animator animator;
        [Tooltip("Particle effects started by the animation events Burst(index)")]
        [SerializeField] private ParticleSystem[] bursts = new ParticleSystem[0];
        [Tooltip("Full-screen invisible button: a tap skips to the fade-out")]
        [SerializeField] private Button skipButton;
        [Tooltip("Time in the clip where the fade-out starts, and the clip length (seconds)")]
        [SerializeField] private float fadeStart = 2.1f;
        [SerializeField] private float clipLength = 2.45f;
        [Tooltip("Taps are ignored for this long so the launch tap does not skip the intro at once")]
        [SerializeField] private float skipAfterSeconds = 0.3f;

        private float _started;
        private bool _skipped;

        private void Awake()
        {
            if (animator != null) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            if (skipButton != null) skipButton.onClick.AddListener(Skip);
        }

        private void Start() => _started = Time.unscaledTime;

        /// <summary>Animation event: plays one particle effect.</summary>
        public void Burst(int index)
        {
            if (_skipped || index < 0 || index >= bursts.Length || bursts[index] == null) return;
            bursts[index].Play(true);
        }

        /// <summary>Animation event at the end of the clip: the splash is done.</summary>
        public void Finished() => Destroy(gameObject);

        private void Skip()
        {
            if (_skipped || Time.unscaledTime - _started < skipAfterSeconds) return;
            _skipped = true;
            // Particles are not faded by the canvas group, so a skipped intro clears them
            foreach (var p in bursts)
                if (p != null) p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (animator != null) animator.Play(Clip, 0, fadeStart / clipLength);
            else Finished();
        }
    }
}
