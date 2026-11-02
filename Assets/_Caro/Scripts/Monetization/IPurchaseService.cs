using System;

namespace Caro.Monetization
{
    /// <summary>
    /// In-app purchases as the game sees them. The game sells one product, "Remove Ads" (non-consumable):
    /// it removes the interstitial and the banner, and makes undo free. Rewarded ads stay available because
    /// the player chooses to watch them.
    /// </summary>
    public interface IPurchaseService
    {
        /// <summary>The store is connected and "Remove Ads" can be bought.</summary>
        bool IsReady { get; }

        /// <summary>A purchase is in progress (store dialog open).</summary>
        bool IsPurchasing { get; }

        /// <summary>The player owns "Remove Ads".</summary>
        bool OwnsRemoveAds { get; }

        /// <summary>Localized price from the store (e.g. "25.000 ₫"); null until the store answers.</summary>
        string RemoveAdsPrice { get; }

        void BuyRemoveAds();

        /// <summary>Asks the store to re-deliver past purchases (required on iOS; Android restores at startup).</summary>
        void Restore();

        /// <summary>Raised on the main thread when anything above changes.</summary>
        event Action Changed;
    }

    /// <summary>No store: nothing to buy, nothing owned.</summary>
    public sealed class NullPurchaseService : IPurchaseService
    {
        public static readonly NullPurchaseService Instance = new NullPurchaseService();

        public bool IsReady => false;
        public bool IsPurchasing => false;
        public bool OwnsRemoveAds => false;
        public string RemoveAdsPrice => null;
        public void BuyRemoveAds() { }
        public void Restore() { }
        public event Action Changed { add { } remove { } }
    }
}
