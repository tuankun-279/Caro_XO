/// <summary>
/// LevelPlay App Key and ad unit IDs, from the LevelPlay dashboard (platform.ironsrc.com → Apps / Ad units).
/// In the Unity Editor (build target Android) LevelPlay shows mock ads with any value, so the placeholders
/// below are enough to test; replace them with the real IDs before testing on a phone.
/// </summary>
public static class CaroAdIds
{
#if UNITY_IOS
    public const string AppKey = "IOS_APP_KEY";
    public const string Rewarded = "IOS_REWARDED_AD_UNIT_ID";
    public const string Interstitial = "IOS_INTERSTITIAL_AD_UNIT_ID";
    public const string Banner = "IOS_BANNER_AD_UNIT_ID";
#else
    public const string AppKey = "ANDROID_APP_KEY";
    public const string Rewarded = "ANDROID_REWARDED_AD_UNIT_ID";
    public const string Interstitial = "ANDROID_INTERSTITIAL_AD_UNIT_ID";
    public const string Banner = "ANDROID_BANNER_AD_UNIT_ID";
#endif

    /// <summary>Google Play / App Store product ID of "Remove Ads" (non-consumable).</summary>
    public const string RemoveAdsProduct = "remove_ads";
}
