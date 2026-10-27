using UnityEngine;

namespace Caro.Game
{
    /// <summary>
    /// Keeps a vertically tiled column (the notebook spiral) a whole number of tiles tall and centred in
    /// its parent, so no loop is cut in half at either end, whatever the screen height.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(RectTransform))]
    public class WholeTileColumn : MonoBehaviour
    {
        [SerializeField] private float tileHeight = 68f;
        [SerializeField] private float margin = 40f;

        private float _lastParentHeight = -1f;

        /// <summary>Sets the tile height and the free space kept at the top and bottom, then refits.</summary>
        public void Configure(float tile, float edgeMargin)
        {
            tileHeight = tile;
            margin = edgeMargin;
            _lastParentHeight = -1f;
            Fit();
        }

        private void OnEnable()
        {
            _lastParentHeight = -1f;
            Fit();
        }

        private void LateUpdate() => Fit();

        private void Fit()
        {
            var rt = (RectTransform)transform;
            if (!(rt.parent is RectTransform parent) || tileHeight <= 0f) return;
            float h = parent.rect.height;
            if (Mathf.Approximately(h, _lastParentHeight)) return;
            _lastParentHeight = h;

            int count = Mathf.Max(1, Mathf.FloorToInt((h - 2f * margin) / tileHeight));
            rt.anchorMin = new Vector2(rt.anchorMin.x, 0.5f);
            rt.anchorMax = new Vector2(rt.anchorMax.x, 0.5f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, count * tileHeight);
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, 0f);
        }
    }
}
