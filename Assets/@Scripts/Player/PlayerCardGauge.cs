using System;
using UnityEngine;

public class PlayerCardGauge : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxCards = 5;
    [SerializeField, Min(0f)] private float normalAttackGain = 0.1f;
    [SerializeField] private float currentCards;

    public int MaxCards => maxCards;
    public float CurrentCards => currentCards;
    public event Action<float> GaugeChanged;

    private void Awake() => currentCards = 0f;

    public void ChargeFromNormalAttack() => AddCards(normalAttackGain);

    public void AddCards(float amount)
    {
        if (!IsPositiveFinite(amount)) return;
        SetCards(Mathf.Clamp(currentCards + amount, 0f, maxCards));
    }

    public bool HasCards(float amount) => IsPositiveFinite(amount) && currentCards >= amount;

    public bool TrySpendCards(float amount)
    {
        if (!HasCards(amount)) return false;
        SetCards(currentCards - amount);
        return true;
    }

    private void SetCards(float value)
    {
        if (currentCards == value) return;
        currentCards = value;
        GaugeChanged?.Invoke(currentCards);
    }

    private static bool IsPositiveFinite(float value)
        => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
}
