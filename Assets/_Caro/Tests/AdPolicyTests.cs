using Caro.Monetization;
using NUnit.Framework;

namespace Caro.Tests
{
    public class AdPolicyTests
    {
        private double _now;
        private bool _adsRemoved;

        private AdPolicy NewPolicy()
        {
            _now = 0;
            _adsRemoved = false;
            return new AdPolicy(() => _now, () => _adsRemoved)
            {
                FreeUndosPerGame = 1,
                GamesBetweenInterstitials = 3,
                MinSecondsBetweenInterstitials = 180,
            };
        }

        private static void FinishGames(AdPolicy p, int n)
        {
            for (int i = 0; i < n; i++) p.OnGameFinished();
        }

        // ---------------- Undo ----------------

        [Test]
        public void FirstUndoOfAGameIsFree()
        {
            var p = NewPolicy();
            p.OnGameStarted();
            Assert.IsFalse(p.UndoNeedsAd());
        }

        [Test]
        public void UndoAfterTheFreeOnesNeedsAnAd()
        {
            var p = NewPolicy();
            p.OnGameStarted();
            p.OnUndoUsed();
            Assert.IsTrue(p.UndoNeedsAd());
        }

        [Test]
        public void NewGameGivesTheFreeUndoBack()
        {
            var p = NewPolicy();
            p.OnGameStarted();
            p.OnUndoUsed();
            p.OnGameStarted();
            Assert.IsFalse(p.UndoNeedsAd());
        }

        [Test]
        public void RemoveAdsMakesEveryUndoFree()
        {
            var p = NewPolicy();
            p.OnGameStarted();
            for (int i = 0; i < 5; i++) p.OnUndoUsed();
            _adsRemoved = true;
            Assert.IsFalse(p.UndoNeedsAd());
        }

        // ---------------- Interstitial ----------------

        [Test]
        public void NoInterstitialRightAfterLaunchEvenAfterManyGames()
        {
            var p = NewPolicy();
            FinishGames(p, 10);
            _now = 179;
            Assert.IsFalse(p.ShouldShowInterstitial());
        }

        [Test]
        public void InterstitialNeedsEnoughGames()
        {
            var p = NewPolicy();
            _now = 1000;
            FinishGames(p, 2);
            Assert.IsFalse(p.ShouldShowInterstitial());
            p.OnGameFinished();
            Assert.IsTrue(p.ShouldShowInterstitial());
        }

        [Test]
        public void ShowingAnInterstitialResetsGamesAndTime()
        {
            var p = NewPolicy();
            _now = 1000;
            FinishGames(p, 3);
            p.OnInterstitialShown();
            Assert.AreEqual(0, p.GamesSinceInterstitial);

            FinishGames(p, 3);
            _now = 1000 + 179; // enough games, not enough time
            Assert.IsFalse(p.ShouldShowInterstitial());
            _now = 1000 + 180;
            Assert.IsTrue(p.ShouldShowInterstitial());
        }

        [Test]
        public void RemoveAdsStopsInterstitials()
        {
            var p = NewPolicy();
            _now = 1000;
            FinishGames(p, 3);
            _adsRemoved = true;
            Assert.IsFalse(p.ShouldShowInterstitial());
        }

        // ---------------- Defaults ----------------

        [Test]
        public void NullServicesNeverShowAnything()
        {
            Services.Reset();
            bool? rewarded = null;
            bool closed = false;
            Services.Ads.ShowRewarded(Placements.Undo, r => rewarded = r);
            Services.Ads.ShowInterstitial(Placements.GameOver, () => closed = true);
            Assert.IsFalse(Services.Ads.IsRewardedReady);
            Assert.AreEqual(false, rewarded, "the callback still runs, with no reward");
            Assert.IsTrue(closed, "the interstitial callback still runs so the game moves on");
            Assert.AreEqual(0f, Services.Ads.BannerHeightPixels);
            Assert.IsFalse(Services.Purchases.OwnsRemoveAds);
        }
    }
}
