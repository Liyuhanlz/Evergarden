using UnityEngine;

// Tracks the player's gold. ShopUI and QuestNPC rewards read and modify this
// through the singleton Instance -- same pattern as FarmManager/InventoryManager.
//
// Unity setup:
//   1. Put this script on an empty "PlayerWallet" GameObject
//   2. Set Starting Gold to whatever amount the player should begin with
public class PlayerWallet : MonoBehaviour
{
    public static PlayerWallet Instance { get; private set; }

    [Header("Starting Balance")]
    public int startingGold = 100;

    public int Gold { get; private set; }

    public System.Action<int> OnGoldChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Gold = startingGold;
    }

    public bool CanAfford(int amount)
    {
        return Gold >= amount;
    }

    public void AddGold(int amount)
    {
        if (amount <= 0) return;

        Gold += amount;
        OnGoldChanged?.Invoke(Gold);
    }

    public bool SpendGold(int amount)
    {
        if (amount <= 0 || !CanAfford(amount)) return false;

        Gold -= amount;
        OnGoldChanged?.Invoke(Gold);
        return true;
    }
}
