using System;

namespace Caro.Monetization
{
    /// <summary>
    /// Ads as the game sees them. The game never talks to an ad SDK directly: the real implementation
    /// (LevelPlay) lives outside the game assemblies and registers itself in <see cref="Services"/>, so the
    /// game, its tests and builds without an ad SDK all run against <see cref="NullAdService"/>.
    /// </summary>
    public interface IAdService
    {
        /// <summary>A rewarded ad is loaded and can be shown right now.</summary>
        bool IsRewardedReady { get; }

        /// <summary>
        /// Shows a rewarded ad. <paramref name="onFinished"/> is called exactly once, on the main thread:
        /// true if the player earned the reward, false if the ad was skipped or failed to show.
        /// </summary>
        void ShowRewarded(string placement, Action<bool> onFinished);

        /// <summary>An interstitial is loaded and can be shown right now.</summary>
        bool IsInterstitialReady { get; }

        /// <summary>Shows an interstitial. <paramref name="onClosed"/> is called exactly once, whether or not it showed.</summary>
        void ShowInterstitial(string placement, Action onClosed);

        /// <summary>Asks for the banner to be shown or hidden. It only appears once loaded and while ads are not removed.</summary>
        void SetBannerVisible(bool visible);

        /// <summary>Height in screen pixels the banner covers at the bottom of the screen; 0 while it is not shown.</summary>
        float BannerHeightPixels { get; }
    }

    /// <summary>Ad placement names, used for reporting in the mediation dashboard.</summary>
    public static class Placements
    {
        public const string Undo = "undo";
        public const string GameOver = "game_over";
    }

    /// <summary>No ads at all: rewarded and interstitial are never ready, the banner never shows.</summary>
    public sealed class NullAdService : IAdService
    {
        public static readonly NullAdService Instance = new NullAdService();

        public bool IsRewardedReady => false;
        public void ShowRewarded(string placement, Action<bool> onFinished) => onFinished?.Invoke(false);
        public bool IsInterstitialReady => false;
        public void ShowInterstitial(string placement, Action onClosed) => onClosed?.Invoke();
        public void SetBannerVisible(bool visible) { }
        public float BannerHeightPixels => 0f;
    }
}
