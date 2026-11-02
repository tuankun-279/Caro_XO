using Caro.Monetization;
using UnityEngine;

namespace Caro.Game
{
    /// <summary>
    /// Fits the UI into the screen's safe area, avoiding notches, punch-hole cameras
    /// and the navigation bar. Attach to a full-screen RectTransform.
    /// While a banner ad is shown at the bottom, the area also stops above it, so bottom-anchored UI
    /// (the game's button bar and score) moves up instead of being covered.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        [Tooltip("Editor testing only: simulated insets (left, bottom, right, top) in pixels.")]
        [SerializeField] private Vector4 editorSimulatedInsets = Vector4.zero;

        private Rect _applied;
        private Vector2Int _screen;
        private float _bannerApplied;

        private void OnEnable() => Apply(true);
        private void Update() => Apply(false);

        private void Apply(bool force)
        {
            var area = Screen.safeArea;
#if UNITY_EDITOR
            if (editorSimulatedInsets != Vector4.zero)
                area = new Rect(editorSimulatedInsets.x, editorSimulatedInsets.y,
                    Screen.width - editorSimulatedInsets.x - editorSimulatedInsets.z,
                    Screen.height - editorSimulatedInsets.y - editorSimulatedInsets.w);
#endif
            var screen = new Vector2Int(Screen.width, Screen.height);
            float banner = Services.Ads.BannerHeightPixels;
            if (!force && area == _applied && screen == _screen && Mathf.Approximately(banner, _bannerApplied)) return;
            if (screen.x <= 0 || screen.y <= 0) return;
            _applied = area;
            _screen = screen;
            _bannerApplied = banner;
            if (banner > 0f)
            {
                area.yMin += banner;
                if (area.height <= 0f) return;
            }

            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(area.xMin / screen.x, area.yMin / screen.y);
            rt.anchorMax = new Vector2(area.xMax / screen.x, area.yMax / screen.y);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
