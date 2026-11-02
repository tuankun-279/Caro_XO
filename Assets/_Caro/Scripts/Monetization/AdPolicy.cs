using System;

namespace Caro.Monetization
{
    /// <summary>
    /// When the game may show an ad. Pure C# with an injected clock, so every rule is unit-tested.
    ///
    /// Rules ("balanced" strategy):
    /// - Undo against the AI: the first <see cref="FreeUndosPerGame"/> undos of a game are free, later ones
    ///   ask for a rewarded ad. Owning "Remove Ads" makes every undo free.
    /// - Interstitial: only between games (after the result screen), at most once every
    ///   <see cref="GamesBetweenInterstitials"/> finished games and never within <see cref="MinSecondsBetweenInterstitials"/>
    ///   of the previous one or of the session start. Never with "Remove Ads".
    /// The numbers are properties so they can later be driven by Remote Config.
    /// </summary>
    public sealed class AdPolicy
    {
        public int FreeUndosPerGame { get; set; } = 1;
        public int GamesBetweenInterstitials { get; set; } = 3;
        public double MinSecondsBetweenInterstitials { get; set; } = 180;

        private readonly Func<double> _now;
        private readonly Func<bool> _adsRemoved;
        private int _undosThisGame;
        private int _gamesSinceInterstitial;
        private double _lastInterstitial;

        /// <param name="now">Current time in seconds (e.g. realtime since startup).</param>
        /// <param name="adsRemoved">True when the player owns "Remove Ads".</param>
        public AdPolicy(Func<double> now, Func<bool> adsRemoved)
        {
            _now = now ?? throw new ArgumentNullException(nameof(now));
            _adsRemoved = adsRemoved ?? (() => false);
            _lastInterstitial = _now(); // the session start counts as the last interstitial: no ad right after launch
        }

        public int UndosThisGame => _undosThisGame;
        public int GamesSinceInterstitial => _gamesSinceInterstitial;

        /// <summary>A new game starts: its free undos are available again.</summary>
        public void OnGameStarted() => _undosThisGame = 0;

        /// <summary>True when the next undo has to be paid for with a rewarded ad.</summary>
        public bool UndoNeedsAd() => !_adsRemoved() && _undosThisGame >= FreeUndosPerGame;

        public void OnUndoUsed() => _undosThisGame++;

        /// <summary>A game was played to its result screen.</summary>
        public void OnGameFinished() => _gamesSinceInterstitial++;

        public bool ShouldShowInterstitial() =>
            !_adsRemoved()
            && _gamesSinceInterstitial >= GamesBetweenInterstitials
            && _now() - _lastInterstitial >= MinSecondsBetweenInterstitials;

        public void OnInterstitialShown()
        {
            _gamesSinceInterstitial = 0;
            _lastInterstitial = _now();
        }
    }
}
