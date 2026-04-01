using UnityEngine;
using System;

public class PlayerCoins : MonoBehaviour
{
    [SerializeField] private int coins;
    public event Action<int> OnCoinsChanged;

    public int Coins => coins;

    public void Add(int amount)
    {
        if (amount <= 0) return;
        coins += amount;
        OnCoinsChanged?.Invoke(coins);
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0 || coins < amount) return false;
        coins -= amount;
        OnCoinsChanged?.Invoke(coins);
        return true;
    }

    public void SetCoins(int amount)
    {
        coins = Mathf.Max(0, amount);
        OnCoinsChanged?.Invoke(coins);
    }
}
