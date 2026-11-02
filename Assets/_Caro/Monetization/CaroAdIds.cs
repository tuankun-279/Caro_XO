/// <summary>
/// LevelPlay App Key and ad unit IDs, from the LevelPlay dashboard (platform.ironsrc.com → Apps / Ad units).
/// In the Unity Editor (build target Android) LevelPlay shows mock ads with any value, so the placeholders
/// below are enough to test; replace them with the real IDs before testing on a phone.
/// An empty ad unit ID turns that format off: the game currently uses only the rewarded ad (before an undo),
/// so Interstitial and Banner are left empty and no ad units are needed for them in the dashboard.
/// </summary>
public static class CaroAdIds
{
#if UNITY_IOS
    public const string AppKey = "IOS_APP_KEY";
    public const string Rewarded = "IOS_REWARDED_AD_UNIT_ID";
    public const string Interstitial = "IOS_INTERSTITIAL_AD_UNIT_ID";
    public const string Banner = "IOS_BANNER_AD_UNIT_ID";
#else
    public const string AppKey = "2930e0815";
    public const string Rewarded = "7txhi84ckwbyohoj";
    public const string Interstitial = ""; // off
    public const string Banner = "";       // off
#endif

    /// <summary>Google Play / App Store product ID of "Remove Ads" (non-consumable).</summary>
    public const string RemoveAdsProduct = "remove_ads";
}
