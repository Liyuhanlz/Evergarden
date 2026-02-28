using UnityEngine;
using TMPro;

public class MoneyManager : MonoBehaviour
{
    public int money = 0;
    public TMP_Text moneyText;

    // Call this to add money
    public void AddMoney(int amount)
    {
        money += amount;
        UpdateMoneyUI();
    }

    // Call this to spend money
    public void SpendMoney(int amount)
    {
        money -= amount;
        if (money < 0) money = 0;
        UpdateMoneyUI();
    }

    void UpdateMoneyUI()
    {
        if (moneyText != null)
            moneyText.text = $"${money}";
    }
}
