using System;
using System.Collections.Generic;
using System.Linq;
using Caro.Monetization;
using UnityEngine;
using UnityEngine.Purchasing;

/// <summary>
/// <see cref="Caro.Monetization.IPurchaseService"/> on top of Unity IAP 5 (StoreController): one non-consumable, "Remove Ads".
///
/// Flow: subscribe to every event → Connect() → on connected, FetchProducts + FetchPurchases.
/// Purchase is two-step: OnPurchasePending → grant and save → ConfirmPurchase. If the app dies between the
/// grant and the confirm, the store re-delivers the pending order on next launch; granting is idempotent
/// (a flag), so a re-delivered order is harmless. Owned purchases come back from FetchPurchases at startup,
/// which is also how a reinstall restores "Remove Ads" on Android.
///
/// Not done yet (needed before real money): receipt validation. On Google Play, generate GooglePlayTangle
/// with Services → In-App Purchasing → Receipt Validation Obfuscator and check order.Info.Receipt with
/// CrossPlatformValidator before granting; better, verify on a server.
/// </summary>
public sealed class UnityIapService : Caro.Monetization.IPurchaseService
{
    private const string OwnedKey = "caro.iap.removeAds";

    private StoreController _store;
    private Product _removeAds;

    public event Action Changed;

    public bool IsReady => _removeAds != null && _removeAds.availableToPurchase && !IsPurchasing;
    public bool IsPurchasing { get; private set; }
    public bool OwnsRemoveAds => PlayerPrefs.GetInt(OwnedKey, 0) == 1;
    public string RemoveAdsPrice => _removeAds?.metadata?.localizedPriceString;

    private static List<ProductDefinition> Catalog => new List<ProductDefinition>
    {
        new ProductDefinition(CaroAdIds.RemoveAdsProduct, ProductType.NonConsumable),
    };

    public async void Initialize()
    {
        _store = UnityIAPServices.StoreController();

        // Every event is subscribed before Connect(): pending orders from a previous session may arrive at once
        _store.OnStoreConnected += OnStoreConnected;
        _store.OnStoreDisconnected += OnStoreDisconnected;
        _store.OnProductsFetched += OnProductsFetched;
        _store.OnProductsFetchFailed += OnProductsFetchFailed;
        _store.OnPurchasesFetched += OnPurchasesFetched;
        _store.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
        _store.OnPurchasePending += OnPurchasePending;
        _store.OnPurchaseConfirmed += OnPurchaseConfirmed;
        _store.OnPurchaseFailed += OnPurchaseFailed;
        _store.OnPurchaseDeferred += OnPurchaseDeferred;

        try
        {
            await _store.Connect();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[IAP] Connect failed: {e.Message}");
        }
    }

    // ================= Store and catalog =================

    private void OnStoreConnected()
    {
        Debug.Log("[IAP] Store connected");
        _store.FetchProducts(Catalog);
        _store.FetchPurchases();
    }

    private void OnStoreDisconnected(StoreConnectionFailureDescription failure)
    {
        Debug.LogWarning($"[IAP] Store disconnected: {failure.Message}");
        Changed?.Invoke();
    }

    private void OnProductsFetched(List<Product> products)
    {
        _removeAds = _store.GetProductById(CaroAdIds.RemoveAdsProduct);
        Debug.Log($"[IAP] Products ready. Remove Ads: {(_removeAds != null ? RemoveAdsPrice : "missing")}");
        Changed?.Invoke();
    }

    private void OnProductsFetchFailed(ProductFetchFailed failure)
    {
        Debug.LogWarning($"[IAP] Product fetch failed: {failure.FailureReason}");
        Changed?.Invoke();
    }

    private void OnPurchasesFetched(Orders orders)
    {
        // Already confirmed purchases (earlier sessions, other devices, a reinstall)
        foreach (var order in orders.ConfirmedOrders)
            if (Contains(order, CaroAdIds.RemoveAdsProduct)) GrantRemoveAds();
        // Pending ones are re-delivered through OnPurchasePending
    }

    private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure) =>
        Debug.LogWarning($"[IAP] Purchase fetch failed: {failure.Message}");

    // ================= Buying =================

    public void BuyRemoveAds()
    {
        if (!IsReady || OwnsRemoveAds) return;
        IsPurchasing = true;
        Changed?.Invoke();
        _store.PurchaseProduct(_removeAds);
    }

    public void Restore()
    {
        if (_store == null) return;
        // Each restored purchase comes back through OnPurchasePending
        _store.RestoreTransactions((ok, error) =>
        {
            if (ok) Debug.Log("[IAP] Restore finished");
            else Debug.LogWarning($"[IAP] Restore failed: {error}");
        });
    }

    private void OnPurchasePending(PendingOrder order)
    {
        if (Contains(order, CaroAdIds.RemoveAdsProduct))
            GrantRemoveAds();   // saved before confirming; idempotent if the order is delivered again
        else
            Debug.LogWarning("[IAP] Pending order for an unknown product; confirming without a grant");

        _store.ConfirmPurchase(order);
    }

    private void OnPurchaseConfirmed(Order order)
    {
        switch (order)
        {
            case ConfirmedOrder _:
                Debug.Log("[IAP] Purchase confirmed");
                break;
            case FailedOrder failed:
                Debug.LogWarning($"[IAP] Confirmation failed: {failed.FailureReason} – {failed.Details}");
                break;
        }
        IsPurchasing = false;
        Changed?.Invoke();
    }

    private void OnPurchaseFailed(FailedOrder failed)
    {
        Debug.LogWarning($"[IAP] Purchase failed: {failed.FailureReason} – {failed.Details}");
        IsPurchasing = false;
        Changed?.Invoke();
    }

    private void OnPurchaseDeferred(DeferredOrder deferred)
    {
        // Waiting for approval (Ask to Buy, pending payment): nothing is granted until OnPurchasePending
        Debug.Log("[IAP] Purchase deferred, waiting for approval");
        IsPurchasing = false;
        Changed?.Invoke();
    }

    // ================= Helpers =================

    private void GrantRemoveAds()
    {
        if (OwnsRemoveAds) return;
        PlayerPrefs.SetInt(OwnedKey, 1);
        PlayerPrefs.Save();
        Debug.Log("[IAP] Remove Ads granted");
        Changed?.Invoke();
    }

    private static bool Contains(Order order, string productId) =>
        order?.CartOrdered?.Items()?.Any(i => i?.Product?.definition?.id == productId) == true;
}
