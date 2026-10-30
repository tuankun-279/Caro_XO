using UnityEngine;

namespace Caro.Game
{
    /// <summary>
    /// Takes a blurred snapshot of what is on screen, for the backdrop behind a popup.
    /// Call <see cref="Capture"/> after WaitForEndOfFrame. The frame is copied, shrunk in halves (each step
    /// averages 2 × 2 pixels with bilinear filtering, which is a cheap box blur) and scaled back up a little
    /// so the result is soft without blocky edges.
    /// </summary>
    public static class ScreenBlur
    {
        /// <summary>A sharp full-size copy of the current frame (for the page turn). Call after WaitForEndOfFrame.</summary>
        public static RenderTexture Snapshot(RenderTexture target, out bool flipped)
        {
            int w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
            if (target == null || target.width != w || target.height != h)
            {
                if (target != null) target.Release();
                target = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear, name = "PageSnapshot" };
            }
            ScreenCapture.CaptureScreenshotIntoRenderTexture(target);
            flipped = SystemInfo.graphicsUVStartsAtTop;
            return target;
        }

        /// <param name="target">Texture to reuse (resized if needed); pass null the first time.</param>
        /// <param name="halvings">How many times the frame is halved; 4 = 1/16 of the screen size.</param>
        /// <param name="flipped">True when the snapshot is upside down on this graphics API.</param>
        public static RenderTexture Capture(RenderTexture target, int halvings, out bool flipped)
        {
            int w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
            var full = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            ScreenCapture.CaptureScreenshotIntoRenderTexture(full);
            flipped = SystemInfo.graphicsUVStartsAtTop;

            var current = full;
            for (int i = 0; i < halvings; i++)
            {
                w = Mathf.Max(1, w / 2);
                h = Mathf.Max(1, h / 2);
                var next = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                next.filterMode = FilterMode.Bilinear;
                Graphics.Blit(current, next);
                RenderTexture.ReleaseTemporary(current);
                current = next;
            }

            // Scale back up two steps so the final bilinear stretch on screen stays smooth
            int tw = w * 4, th = h * 4;
            if (target == null || target.width != tw || target.height != th)
            {
                if (target != null) target.Release();
                target = new RenderTexture(tw, th, 0, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Bilinear, name = "PopupBlur" };
            }
            var mid = RenderTexture.GetTemporary(w * 2, h * 2, 0, RenderTextureFormat.ARGB32);
            mid.filterMode = FilterMode.Bilinear;
            Graphics.Blit(current, mid);
            Graphics.Blit(mid, target);
            RenderTexture.ReleaseTemporary(mid);
            RenderTexture.ReleaseTemporary(current);
            return target;
        }
    }
}
