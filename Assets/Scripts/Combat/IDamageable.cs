using UnityEngine;

public enum DamageSource { Enemy, Staff, Spell, Other }

// One hit: how much, which way it pushes, whether it rolled a critical and where it came from.
public readonly struct DamageInfo
{
    public readonly int Amount;
    public readonly Vector2 Knockback;
    public readonly bool IsCritical;
    public readonly DamageSource Source;

    public DamageInfo(int amount, Vector2 knockback, bool isCritical = false, DamageSource source = DamageSource.Other)
    {
        Amount = amount;
        Knockback = knockback;
        IsCritical = isCritical;
        Source = source;
    }

    public DamageInfo WithKnockback(Vector2 knockback)
    {
        return new DamageInfo(Amount, knockback, IsCritical, Source);
    }
}

public interface IDamageable
{
    // Returns false when the hit was ignored (invulnerable or already dead).
    bool Damage(in DamageInfo info);
}
