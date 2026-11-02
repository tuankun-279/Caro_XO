using System;
using System.Collections;
using Caro.Monetization;
using Unity.Services.LevelPlay;
using UnityEngine;

/// <summary>
/// <see cref="IAdService"/> on top of LevelPlay (Unity's ad mediation, SDK 9.x ad unit API).
///
/// - Initializes LevelPlay once; ad objects are created only after OnInitSuccess. A failed init is retried.
/// - Rewarded and interstitial are preloaded and reloaded after each show. A failed load retries with
///   exponential backoff (2 s, 4 s, 8 s … capped at 64 s) so no-fill or offline does not hammer the network.
/// - Every Show* callback runs exactly once: on close, on display failure, or immediately if not ready.
/// - Some networks send "rewarded" just after "closed": the close waits a moment for a late reward.
/// - The banner (320×50, bottom) is created hidden and shown only while the game asks for it.
/// - Game audio is paused while a full-screen ad is on screen.
/// - "Remove Ads" destroys the banner and stops interstitials; rewarded stays (the player opts in).
/// </summary>
public sealed class LevelPlayAdService : MonoBehaviour, IAdService
{
    private const float BannerHeightDp = 50f;          // LevelPlayAdSize.BANNER is 320×50 dp
    private const float LateRewardWaitSeconds = 1f;
    private const float InitRetrySeconds = 30f;
    private const float MaxRetrySeconds = 64f;

    private Func<bool> _adsRemoved = () => false;
    private bool _initialized;

    private LevelPlayRewardedAd _rewarded;
    private LevelPlayInterstitialAd _interstitial;
    private LevelPlayBannerAd _banner;

    private int _rewardedFailures, _interstitialFailures, _bannerFailures;

    private Action<bool> _rewardedDone;
    private bool _rewardEarned;
    private Action _interstitialDone;

    private bool _bannerWanted, _bannerLoaded, _bannerShown;

    // ================= Setup =================

    /// <param name="adsRemoved">True once the player owns "Remove Ads".</param>
    public void Init(Func<bool> adsRemoved)
    {
        if (adsRemoved != null) _adsRemoved = adsRemoved;
        // Callbacks are registered before Init, as LevelPlay requires
        LevelPlay.OnInitSuccess += OnInitSuccess;
        LevelPlay.OnInitFailed += OnInitFailed;
        StartInit();
    }

    private void StartInit()
    {
        Debug.Log("[Ads] LevelPlay init…");
        LevelPlay.Init(CaroAdIds.AppKey);
    }

    private void OnInitSuccess(LevelPlayConfiguration config)
    {
        if (_initialized) return;
        _initialized = true;
        Debug.Log("LevelPlay SDK initialized successfully");

        _rewarded = new LevelPlayRewardedAd(CaroAdIds.Rewarded);
        _rewarded.OnAdLoaded += OnRewardedLoaded;
        _rewarded.OnAdLoadFailed += OnRewardedLoadFailed;
        _rewarded.OnAdDisplayed += OnFullScreenDisplayed;
        _rewarded.OnAdDisplayFailed += OnRewardedDisplayFailed;
        _rewarded.OnAdRewarded += OnRewardedRewarded;
        _rewarded.OnAdClosed += OnRewardedClosed;
        _rewarded.LoadAd();

        _interstitial = new LevelPlayInterstitialAd(CaroAdIds.Interstitial);
        _interstitial.OnAdLoaded += OnInterstitialLoaded;
        _interstitial.OnAdLoadFailed += OnInterstitialLoadFailed;
        _interstitial.OnAdDisplayed += OnFullScreenDisplayed;
        _interstitial.OnAdDisplayFailed += OnInterstitialDisplayFailed;
        _interstitial.OnAdClosed += OnInterstitialClosed;
        if (!_adsRemoved()) _interstitial.LoadAd();

        if (!_adsRemoved()) CreateBanner();
    }

    private void OnInitFailed(LevelPlayInitError error)
    {
        Debug.LogWarning($"[Ads] LevelPlay init failed: {error.ErrorMessage}. Retrying in {InitRetrySeconds}s");
        StartCoroutine(After(InitRetrySeconds, () => { if (!_initialized) StartInit(); }));
    }

    /// <summary>Called when purchases change: "Remove Ads" takes the banner and interstitials away at once.</summary>
    public void OnEntitlementsChanged()
    {
        if (!_adsRemoved()) return;
        DestroyBanner();
    }

    private void OnDestroy()
    {
        LevelPlay.OnInitSuccess -= OnInitSuccess;
        LevelPlay.OnInitFailed -= OnInitFailed;

        if (_rewarded != null)
        {
            _rewarded.OnAdLoaded -= OnRewardedLoaded;
            _rewarded.OnAdLoadFailed -= OnRewardedLoadFailed;
            _rewarded.OnAdDisplayed -= OnFullScreenDisplayed;
            _rewarded.OnAdDisplayFailed -= OnRewardedDisplayFailed;
            _rewarded.OnAdRewarded -= OnRewardedRewarded;
            _rewarded.OnAdClosed -= OnRewardedClosed;
            _rewarded.DestroyAd();
            _rewarded = null;
        }
        if (_interstitial != null)
        {
            _interstitial.OnAdLoaded -= OnInterstitialLoaded;
            _interstitial.OnAdLoadFailed -= OnInterstitialLoadFailed;
            _interstitial.OnAdDisplayed -= OnFullScreenDisplayed;
            _interstitial.OnAdDisplayFailed -= OnInterstitialDisplayFailed;
            _interstitial.OnAdClosed -= OnInterstitialClosed;
            _interstitial.DestroyAd();
            _interstitial = null;
        }
        DestroyBanner();
    }

    // ================= Rewarded =================

    public bool IsRewardedReady => _rewarded != null && _rewarded.IsAdReady() && _rewardedDone == null;

    public void ShowRewarded(string placement, Action<bool> onFinished)
    {
        if (!IsRewardedReady)
        {
            onFinished?.Invoke(false);
            return;
        }
        _rewardedDone = onFinished ?? (_ => { });
        _rewardEarned = false;
        // Placement names are only passed once they exist in the LevelPlay dashboard (an unknown one fails to show)
        _rewarded.ShowAd();
    }

    private void OnRewardedLoaded(LevelPlayAdInfo info) => _rewardedFailures = 0;

    private void OnRewardedLoadFailed(LevelPlayAdError error)
    {
        float delay = Backoff(ref _rewardedFailures);
        Debug.LogWarning($"[Ads] Rewarded load failed: {error.ErrorMessage}. Retry in {delay}s");
        StartCoroutine(After(delay, () => _rewarded?.LoadAd()));
    }

    private void OnRewardedRewarded(LevelPlayAdInfo info, LevelPlayReward reward)
    {
        _rewardEarned = true;
        // The reward may arrive after the close; the close coroutine is waiting for it
    }

    private void OnRewardedClosed(LevelPlayAdInfo info)
    {
        ResumeAudio();
        StartCoroutine(FinishRewardedAfterClose());
        _rewarded?.LoadAd();
    }

    private IEnumerator FinishRewardedAfterClose()
    {
        float until = Time.realtimeSinceStartup + LateRewardWaitSeconds;
        while (!_rewardEarned && Time.realtimeSinceStartup < until) yield return null;
        FinishRewarded(_rewardEarned);
    }

    private void OnRewardedDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
    {
        Debug.LogWarning($"[Ads] Rewarded failed to show: {error.ErrorMessage}");
        ResumeAudio();
        FinishRewarded(false);
        _rewarded?.LoadAd();
    }

    private void FinishRewarded(bool rewarded)
    {
        var done = _rewardedDone;
        _rewardedDone = null;
        done?.Invoke(rewarded);
    }

    // ================= Interstitial =================

    public bool IsInterstitialReady =>
        _interstitial != null && !_adsRemoved() && _interstitial.IsAdReady() && _interstitialDone == null;

    public void ShowInterstitial(string placement, Action onClosed)
    {
        if (!IsInterstitialReady)
        {
            onClosed?.Invoke();
            return;
        }
        _interstitialDone = onClosed ?? (() => { });
        _interstitial.ShowAd();
    }

    private void OnInterstitialLoaded(LevelPlayAdInfo info) => _interstitialFailures = 0;

    private void OnInterstitialLoadFailed(LevelPlayAdError error)
    {
        float delay = Backoff(ref _interstitialFailures);
        Debug.LogWarning($"[Ads] Interstitial load failed: {error.ErrorMessage}. Retry in {delay}s");
        StartCoroutine(After(delay, () => { if (!_adsRemoved()) _interstitial?.LoadAd(); }));
    }

    private void OnInterstitialClosed(LevelPlayAdInfo info)
    {
        ResumeAudio();
        FinishInterstitial();
        if (!_adsRemoved()) _interstitial?.LoadAd();
    }

    private void OnInterstitialDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
    {
        Debug.LogWarning($"[Ads] Interstitial failed to show: {error.ErrorMessage}");
        ResumeAudio();
        FinishInterstitial();
        if (!_adsRemoved()) _interstitial?.LoadAd();
    }

    private void FinishInterstitial()
    {
        var done = _interstitialDone;
        _interstitialDone = null;
        done?.Invoke();
    }

    // ================= Banner =================

    public float BannerHeightPixels =>
        _bannerShown ? BannerHeightDp * (Screen.dpi > 0 ? Screen.dpi / 160f : 1f) : 0f;

    public void SetBannerVisible(bool visible)
    {
        _bannerWanted = visible;
        ApplyBanner();
    }

    private void CreateBanner()
    {
        var builder = new LevelPlayBannerAd.Config.Builder();
        builder.SetSize(LevelPlayAdSize.BANNER);
        builder.SetPosition(LevelPlayBannerPosition.BottomCenter);
        builder.SetDisplayOnLoad(false);   // shown only while the game screen asks for it
        builder.SetRespectSafeArea(true);  // Android: stays above the navigation bar
        _banner = new LevelPlayBannerAd(CaroAdIds.Banner, builder.Build());
        _banner.OnAdLoaded += OnBannerLoaded;
        _banner.OnAdLoadFailed += OnBannerLoadFailed;
        _banner.OnAdDisplayFailed += OnBannerDisplayFailed;
        _banner.LoadAd();
    }

    private void OnBannerLoaded(LevelPlayAdInfo info)
    {
        _bannerFailures = 0;
        _bannerLoaded = true;
        _bannerShown = false; // a reload hides nothing on screen, but re-apply to be sure
        ApplyBanner();
    }

    private void OnBannerLoadFailed(LevelPlayAdError error)
    {
        _bannerLoaded = false;
        ApplyBanner();
        float delay = Mathf.Max(30f, Backoff(ref _bannerFailures));
        Debug.LogWarning($"[Ads] Banner load failed: {error.ErrorMessage}. Retry in {delay}s");
        StartCoroutine(After(delay, () => _banner?.LoadAd()));
    }

    private void OnBannerDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
    {
        Debug.LogWarning($"[Ads] Banner failed to show: {error.ErrorMessage}");
        _bannerLoaded = false;
        ApplyBanner();
        StartCoroutine(After(30f, () => _banner?.LoadAd()));
    }

    private void ApplyBanner()
    {
        if (_banner == null) { _bannerShown = false; return; }
        bool show = _bannerWanted && _bannerLoaded && !_adsRemoved();
        if (show == _bannerShown) return;
        if (show) _banner.ShowAd(); else _banner.HideAd();
        _bannerShown = show;
    }

    private void DestroyBanner()
    {
        if (_banner == null) return;
        _banner.OnAdLoaded -= OnBannerLoaded;
        _banner.OnAdLoadFailed -= OnBannerLoadFailed;
        _banner.OnAdDisplayFailed -= OnBannerDisplayFailed;
        _banner.DestroyAd();
        _banner = null;
        _bannerLoaded = _bannerShown = false;
    }

    // ================= Helpers =================

    private void OnFullScreenDisplayed(LevelPlayAdInfo info) => AudioListener.pause = true;

    private static void ResumeAudio() => AudioListener.pause = false;

    /// <summary>2, 4, 8 … 64 seconds for the 1st, 2nd, 3rd … failure in a row.</summary>
    private static float Backoff(ref int failures)
    {
        failures++;
        return Mathf.Min(MaxRetrySeconds, Mathf.Pow(2f, failures));
    }

    private static IEnumerator After(float seconds, Action action)
    {
        yield return new WaitForSecondsRealtime(seconds);
        action();
    }
}
