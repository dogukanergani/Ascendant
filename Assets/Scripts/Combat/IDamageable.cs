using UnityEngine;

public enum DamageSource { Enemy, Staff, Spell, Other }

// One hit: how much, which way it pushes, whether it rolled a critical and where it came from.
public readonly struct DamageInfo
{
    public readonly int Amount;
    public readonly Vector2 Knockback;
    public readonly bool IsCritical;
    public readonly DamageSource Source;
    // Where the hit came from and who dealt it; only melee hits on the player fill these (the shield uses them).
    public readonly Vector2 Origin;
    public readonly bool HasOrigin;
    public readonly EnemyAI Attacker;

    public DamageInfo(int amount, Vector2 knockback, bool isCritical = false, DamageSource source = DamageSource.Other)
        : this(amount, knockback, isCritical, source, default, false, null)
    {
    }

    public DamageInfo(int amount, Vector2 knockback, bool isCritical, DamageSource source, Vector2 origin, bool hasOrigin, EnemyAI attacker)
    {
        Amount = amount;
        Knockback = knockback;
        IsCritical = isCritical;
        Source = source;
        Origin = origin;
        HasOrigin = hasOrigin;
        Attacker = attacker;
    }

    public DamageInfo WithKnockback(Vector2 knockback)
    {
        return new DamageInfo(Amount, knockback, IsCritical, Source, Origin, HasOrigin, Attacker);
    }
}

public interface IDamageable
{
    // Returns false when the hit was ignored (invulnerable or already dead).
    bool Damage(in DamageInfo info);
}
