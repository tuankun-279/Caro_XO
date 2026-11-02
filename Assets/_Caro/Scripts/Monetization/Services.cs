namespace Caro.Monetization
{
    /// <summary>
    /// Where the game finds its ad and purchase services. Defaults to the "none" implementations, so the game
    /// runs unchanged when no SDK registers itself (tests, editor without the packages, builds without ads).
    /// </summary>
    public static class Services
    {
        public static IAdService Ads { get; private set; } = NullAdService.Instance;
        public static IPurchaseService Purchases { get; private set; } = NullPurchaseService.Instance;

        public static void Register(IAdService ads) => Ads = ads ?? NullAdService.Instance;
        public static void Register(IPurchaseService purchases) => Purchases = purchases ?? NullPurchaseService.Instance;

        /// <summary>Back to "no ads, no store" (used by tests).</summary>
        public static void Reset()
        {
            Ads = NullAdService.Instance;
            Purchases = NullPurchaseService.Instance;
        }
    }
}
