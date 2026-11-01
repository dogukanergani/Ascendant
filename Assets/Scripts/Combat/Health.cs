using System;
using UnityEngine;

// Shared health for the player and enemies, counted in parts: a heart is partsPerHeart parts.
// Enemies use one part per hit point (SetMaxParts); the HUD draws the player's parts as hearts.
public sealed class Health : MonoBehaviour, IDamageable
{
    [SerializeField] int maxHearts = 5;
    [SerializeField] int partsPerHeart = 2;
    [SerializeField] float invulnerabilityTime = 0.6f;

    int current;
    float invulnerableUntil = float.NegativeInfinity;

    public event Action<int, int> Changed;
    // Raised after a hit lands.
    public event Action<DamageInfo> Damaged;
    // Raised for every Health after a hit lands; lets world-space feedback (damage numbers) listen in one place.
    public static event Action<Health, DamageInfo> AnyDamaged;
    public event Action Died;
    // Lets a defensive skill (the shield) swallow a hit before it lands; return true to block it completely.
    public Func<DamageInfo, bool> Interceptor { get; set; }

    public int Current => current;
    public int Max => maxHearts * partsPerHeart;
    public int PartsPerHeart => partsPerHeart;
    public int MaxHearts => maxHearts;
    public bool IsDead => current <= 0;
    public bool IsFull => current >= Max;
    public bool IsInvulnerable => Time.time < invulnerableUntil;

    void Awake()
    {
        current = Max;
    }

    // Enemies count hit points one for one: maxParts hearts of one part each.
    public void SetMaxParts(int maxParts, float invulnerability)
    {
        maxHearts = Mathf.Max(1, maxParts);
        partsPerHeart = 1;
        invulnerabilityTime = invulnerability;
        current = Max;
    }

    public bool Damage(in DamageInfo info)
    {
        int amount = info.Amount;
        if (IsDead || amount <= 0 || IsInvulnerable)
        {
            return false;
        }

        if (Interceptor != null && Interceptor(info))
        {
            return false;
        }

        current = Mathf.Max(0, current - amount);
        invulnerableUntil = Time.time + invulnerabilityTime;
        Changed?.Invoke(current, Max);
        Damaged?.Invoke(info);
        AnyDamaged?.Invoke(this, info);
        if (current == 0)
        {
            Died?.Invoke();
        }

        return true;
    }

    public void Heal(int parts)
    {
        if (IsDead || parts <= 0 || IsFull)
        {
            return;
        }

        current = Mathf.Min(Max, current + parts);
        Changed?.Invoke(current, Max);
    }

    public void GrantInvulnerability(float duration)
    {
        invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + duration);
    }

    public void Revive()
    {
        current = Max;
        invulnerableUntil = float.NegativeInfinity;
        Changed?.Invoke(current, Max);
    }
}
