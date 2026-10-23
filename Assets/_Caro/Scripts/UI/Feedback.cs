using System;
using System.Threading;
using Caro.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Caro.Game
{
    /// <summary>
    /// Game sound and vibration. Pen and eraser sounds use real recordings (Audio/SFX);
    /// the other sounds (win, lose, draw, button click) are synthesized in code.
    /// Without assigned clips, placing a piece falls back to a synthesized "tap".
    /// Sound and vibration can be toggled separately; the choice is saved.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class Feedback : MonoBehaviour
    {
        private const string KeySound = "caro.settings.sound";
        private const string KeyVibration = "caro.settings.vibration";
        private const string KeyVolume = "caro.settings.volume";
        private const int Rate = 44100;

        public static Feedback Instance { get; private set; }

        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

        [Header("Pen sounds (recorded clips)")]
        [Tooltip("Short strokes; each X plays two different ones")]
        [SerializeField] private AudioClip[] penXClips;
        [Tooltip("Long strokes covering one full loop, for O")]
        [SerializeField] private AudioClip[] penOClips;
        [Tooltip("Eraser sound on undo")]
        [SerializeField] private AudioClip eraseClip;
        [SerializeField, Range(0f, 1f)] private float penVolume = 0.9f;

        private AudioSource _src;
        private AudioSource _penSrc;     // separate source so pen pitch changes do not affect other sounds
        private float _penBusyUntil;     // win/lose sounds wait until the pen sound is over
        private int _lastX = -1;
        private CancellationTokenSource _strokeCts; // pending second X stroke
        private AudioClip _placeX, _placeO, _win, _lose, _draw, _click;

        public static bool SoundOn
        {
            get => PlayerPrefs.GetInt(KeySound, 1) == 1;
            set { PlayerPrefs.SetInt(KeySound, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Master volume 0..1 for every sound (the settings slider).</summary>
        public static float Volume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(KeyVolume, 1f));
            set { PlayerPrefs.SetFloat(KeyVolume, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        public static bool VibrationOn
        {
            get => PlayerPrefs.GetInt(KeyVibration, 1) == 1;
            set { PlayerPrefs.SetInt(KeyVibration, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        private void Awake()
        {
            Instance = this;
            _src = GetComponent<AudioSource>();
            _src.playOnAwake = false;
            _penSrc = gameObject.AddComponent<AudioSource>();
            _penSrc.playOnAwake = false;

            _placeX = MakeTap("PlaceX", 1750f);
            _placeO = MakeTap("PlaceO", 1350f);
            _win = MakeMelody("Win", new[] { 523.25f, 659.25f, 783.99f }, new[] { 0.11f, 0.11f, 0.32f });
            _lose = MakeMelody("Lose", new[] { 392.00f, 293.66f }, new[] { 0.16f, 0.36f });
            _draw = MakeMelody("Draw", new[] { 220f }, new[] { 0.38f });
            _click = MakeBlip("Click", 1100f, 0.035f, 0.25f);

            // Attach the click sound to every button in the scene (including hidden ones)
            foreach (var b in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                b.onClick.AddListener(Click);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            CancelStroke();
        }

        // ---------- Called by the game ----------

        /// <summary>Pen sound for a placed piece: X = two short scratches, O = one stroke around the loop.</summary>
        public void Place(Piece p)
        {
            _penBusyUntil = Time.unscaledTime + PenTiming.Duration(p) + 0.05f;
            VibrateShort();
            if (p == Piece.X)
            {
                if (penXClips == null || penXClips.Length == 0) { Play(_placeX, Random.Range(0.96f, 1.04f)); return; }
                PlayPen(NextX(), Random.Range(0.94f, 1.06f));
                CancelStroke();
                _strokeCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
                SecondStrokeAsync(_strokeCts.Token).Forget();
            }
            else
            {
                if (penOClips == null || penOClips.Length == 0) { Play(_placeO, Random.Range(0.96f, 1.04f)); return; }
                PlayPen(penOClips[Random.Range(0, penOClips.Length)], Random.Range(0.95f, 1.05f));
            }
        }

        private async UniTaskVoid SecondStrokeAsync(CancellationToken ct)
        {
            bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(PenTiming.XSecondStart), DelayType.Realtime, cancellationToken: ct)
                .SuppressCancellationThrow();
            if (!cancelled) PlayPen(NextX(), Random.Range(0.94f, 1.06f));
        }

        private void CancelStroke()
        {
            _strokeCts?.Cancel();
            _strokeCts?.Dispose();
            _strokeCts = null;
        }

        /// <summary>Picks a stroke different from the previous one so the two X strokes never sound identical.</summary>
        private AudioClip NextX()
        {
            int i = Random.Range(0, penXClips.Length);
            if (penXClips.Length > 1 && i == _lastX) i = (i + 1) % penXClips.Length;
            _lastX = i;
            return penXClips[i];
        }

        /// <summary>Eraser sound on undo.</summary>
        public void Erase()
        {
            CancelStroke();
            if (eraseClip != null) PlayPen(eraseClip, Random.Range(0.95f, 1.05f));
        }

        public void Win() => AfterPenAsync(_win).Forget();
        public void Lose() => AfterPenAsync(_lose).Forget();
        public void Draw() => AfterPenAsync(_draw).Forget();

        /// <summary>Waits until the last move's pen sound is over, then plays the end-of-game sound.</summary>
        private async UniTaskVoid AfterPenAsync(AudioClip clip)
        {
            var ct = destroyCancellationToken;
            float wait = _penBusyUntil - Time.unscaledTime;
            if (wait > 0f && await UniTask.Delay(TimeSpan.FromSeconds(wait), DelayType.Realtime, cancellationToken: ct)
                    .SuppressCancellationThrow()) return;
            Play(clip);
            VibrateLong();
        }

        private void PlayPen(AudioClip clip, float pitch)
        {
            if (!SoundOn || clip == null) return;
            _penSrc.pitch = pitch;
            _penSrc.PlayOneShot(clip, penVolume * Volume);
        }

        public void Click() => Play(_click);

        private void Play(AudioClip clip, float pitch = 1f)
        {
            if (!SoundOn || clip == null) return;
            _src.pitch = pitch;
            _src.PlayOneShot(clip, volume * Volume);
        }

        // ---------- Vibration ----------

        private static void VibrateShort()
        {
            if (!VibrationOn) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidVibrate(25);
#endif
            // iOS only offers a long buzz via Handheld.Vibrate → skip the short one when placing a piece
        }

        private static void VibrateLong()
        {
            if (!VibrationOn) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidVibrate(160);
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void AndroidVibrate(long ms)
        {
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                    vibrator.Call("vibrate", ms);
            }
            catch { Handheld.Vibrate(); }
        }
#endif

        // Only here so Unity adds the VIBRATE permission to Android builds; never actually runs.
        private static void KeepVibratePermission() { if (Time.frameCount < 0) Handheld.Vibrate(); }

        // ---------- Synthesized sounds ----------

        /// <summary>A pen "tap" on paper: a short noise burst plus a quickly fading high tone.</summary>
        private static AudioClip MakeTap(string name, float tone)
        {
            int n = (int)(Rate * 0.07f);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * 90f);
                float ping = Mathf.Sin(2 * Mathf.PI * tone * t) * Mathf.Exp(-t * 55f);
                data[i] = (noise * 0.45f + ping * 0.35f) * Attack(t, 0.0015f);
            }
            return Clip(name, data);
        }

        /// <summary>A soft note sequence (sine + 2nd harmonic), each note with a gentle attack and decay.</summary>
        private static AudioClip MakeMelody(string name, float[] freqs, float[] durs)
        {
            float total = 0; foreach (var d in durs) total += d;
            int n = (int)(Rate * (total + 0.15f));
            var data = new float[n];
            float start = 0;
            for (int k = 0; k < freqs.Length; k++)
            {
                int s0 = (int)(start * Rate);
                int len = (int)((durs[k] + 0.15f) * Rate);
                for (int i = 0; i < len && s0 + i < n; i++)
                {
                    float t = i / (float)Rate;
                    float env = Attack(t, 0.01f) * Mathf.Exp(-t * (3.2f / durs[k]));
                    float w = Mathf.Sin(2 * Mathf.PI * freqs[k] * t) + 0.25f * Mathf.Sin(4 * Mathf.PI * freqs[k] * t);
                    data[s0 + i] += w * env * 0.32f;
                }
                start += durs[k];
            }
            return Clip(name, data);
        }

        private static AudioClip MakeBlip(string name, float freq, float dur, float gain)
        {
            int n = (int)(Rate * dur);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                data[i] = Mathf.Sin(2 * Mathf.PI * freq * t) * Attack(t, 0.002f) * Mathf.Exp(-t * 90f) * gain;
            }
            return Clip(name, data);
        }

        private static float Attack(float t, float time) => Mathf.Clamp01(t / time);

        private static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
