using Caro.Monetization;
using UnityEngine;

/// <summary>
/// Starts ads and purchases before the first scene loads and registers them in <see cref="Services"/>.
/// Lives in the default assembly (Assembly-CSharp), which sees the LevelPlay and IAP packages, so the game
/// assemblies only depend on the small Caro.Monetization interfaces.
/// </summary>
public sealed class MonetizationBootstrap : MonoBehaviour
{
    private UnityIapService _iap;
    private LevelPlayAdService _ads;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("[Monetization]");
        DontDestroyOnLoad(go);
        go.AddComponent<MonetizationBootstrap>();
    }

    private void Awake()
    {
        _iap = new UnityIapService();
        Services.Register(_iap);
        _iap.Initialize();

        _ads = gameObject.AddComponent<LevelPlayAdService>();
        Services.Register(_ads);
        _ads.Init(() => _iap.OwnsRemoveAds);
        _iap.Changed += _ads.OnEntitlementsChanged;
    }

    private void OnDestroy()
    {
        if (_iap != null && _ads != null) _iap.Changed -= _ads.OnEntitlementsChanged;
    }
}
